//! The standard commands (#1091, #1089): the seven lists their owners ship,
//! joined in the order upstream's framework `standardCommands` joins them
//! (CU-C5-D2: the join lives here, above the owners). The host's part of
//! the lists, the clipboard and a file for the user, is a
//! [`StandardCommandsPlatform`] the app sets on the viewer
//! (`ViewerHost::set_commands_platform`), read when a command runs.

use std::rc::Rc;
use std::sync::{Arc, Mutex};

use epdf_ffi::{ForeignSettle, HostFailure, KernelPost};
use epdf_kernel::bytes::Bytes;
use epdf_kernel::error::{code, plugin_error};
use epdf_kernel::rt::Promise;
use epdf_kernel::{Error, undefined};
use epdf_plugin_commands::{CommandItem, DocumentCommandsPlatform, create_document_commands};
use epdf_plugin_render::PageImage;
use epdf_plugin_render::commands::{RenderCommandsPlatform, create_render_commands};
use epdf_plugin_selection::SelectionCapability;
use epdf_plugin_selection::commands::{SelectionCommandsPlatform, create_selection_commands};
use serde_json::Value;

use crate::host_table::HostTable;

/// One owner's list.
#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum StandardCommandList {
    /// Zoom, the page moves and view rotation.
    Stage,
    /// `tool:pointer`, `tool:pan`, `tool:zoom`, and `tool:<id>` for every
    /// other tool.
    Interaction,
    /// `selection:copy`.
    Selection,
    /// `annotation:delete`.
    Annotation,
    /// `history:undo` and `history:redo`.
    History,
    /// The snapshot tool, and copy, save and clear for the snapshot.
    Render,
    /// `document:download` and `document:print`.
    Document,
}

impl StandardCommandList {
    /// Upstream's join order.
    pub const ALL: [StandardCommandList; 7] = [
        StandardCommandList::Stage,
        StandardCommandList::Interaction,
        StandardCommandList::Selection,
        StandardCommandList::Annotation,
        StandardCommandList::History,
        StandardCommandList::Render,
        StandardCommandList::Document,
    ];
}

/// What the standard commands need from the app: the clipboard and a file
/// for the user. Called on the kernel thread. `copy_text` and `copy_image`
/// return at once and answer through `completion`, from any thread.
#[uniffi::export(with_foreign)]
pub trait StandardCommandsPlatform: Send + Sync {
    /// `document:download` and `snapshot:save`: give the user `bytes` as a
    /// file.
    fn save_file(&self, bytes: Vec<u8>, file_name: String, content_type: String);
    /// `selection:copy`: put the selected text on the clipboard.
    fn copy_text(&self, text: String, completion: Arc<StandardCommandCompletion>);
    /// `snapshot:copy`: put the snapshot, a PNG, on the clipboard.
    fn copy_image(&self, png: Vec<u8>, completion: Arc<StandardCommandCompletion>);
}

/// The answer to one copy. The first call wins; dropping it unanswered
/// fails the command with `operation-failed`.
#[derive(uniffi::Object)]
pub struct StandardCommandCompletion {
    settle: ForeignSettle<Value>,
}

#[uniffi::export]
impl StandardCommandCompletion {
    pub fn finish(&self) {
        self.settle.settle(|| Ok(undefined()));
    }

    pub fn fail(&self, detail: String) {
        self.settle.settle(move || Err(Error::new(detail)));
    }

    /// Fail with the thrown error's name and code.
    pub fn fail_with(&self, failure: HostFailure) {
        self.settle.fail(failure, "commands");
    }
}

/// The platform a host has set.
pub type StandardCommandsSlot = Arc<Mutex<Option<Arc<dyn StandardCommandsPlatform>>>>;

/// The owners' platform traits over the host's slot.
struct NativePlatform {
    slot: StandardCommandsSlot,
    post: KernelPost,
}

impl NativePlatform {
    fn platform(&self) -> Result<Arc<dyn StandardCommandsPlatform>, Error> {
        self.slot.lock().unwrap().clone().ok_or_else(|| {
            plugin_error(
                code::INVALID_INPUT,
                "commands",
                "no commands platform: set one on the viewer first",
                None,
                None,
            )
        })
    }

    /// A copy through the platform, answered by its completion.
    fn copy(
        &self,
        send: impl FnOnce(&dyn StandardCommandsPlatform, Arc<StandardCommandCompletion>),
    ) -> Promise<Value> {
        let platform = match self.platform() {
            Ok(platform) => platform,
            Err(error) => return Promise::rejected(error),
        };
        let (promise, resolver) = Promise::pending();
        let completion = Arc::new(StandardCommandCompletion {
            settle: ForeignSettle::new(
                resolver,
                self.post.clone(),
                "a StandardCommandsPlatform completion",
            ),
        });
        send(&*platform, completion);
        promise
    }

    /// The bytes as the kernel hands them: the download's `$hostBytes`,
    /// taken out of the table, or `$bytes`. Without a platform nothing is
    /// saved, as a browser without a download path saves nothing.
    fn save(&self, bytes: Value, file_name: String, content_type: String) {
        let bytes = match bytes.get("$hostBytes").and_then(Value::as_u64) {
            Some(id) => HostTable::take(id),
            None => Bytes::from_json(&bytes).map(|b| b.0),
        };
        if let (Some(bytes), Ok(platform)) = (bytes, self.platform()) {
            platform.save_file(bytes, file_name, content_type);
        }
    }
}

impl DocumentCommandsPlatform for NativePlatform {
    fn save_file(&self, bytes: Value, file_name: String, content_type: String) {
        self.save(bytes, file_name, content_type);
    }
}

impl SelectionCommandsPlatform for NativePlatform {
    fn copy_selection(&self, selection: &SelectionCapability) -> Promise<Value> {
        let read = selection.read_text(Default::default());
        let this = NativePlatform { slot: self.slot.clone(), post: self.post.clone() };
        Promise::spawn(async move {
            let text = read.await?;
            this.copy(|platform, completion| platform.copy_text(text, completion)).await
        })
    }
}

impl RenderCommandsPlatform for NativePlatform {
    fn copy_image(&self, image: Promise<PageImage>) -> Promise<Value> {
        let this = NativePlatform { slot: self.slot.clone(), post: self.post.clone() };
        Promise::spawn(async move {
            let png = encode_png(&image.await?)?;
            this.copy(|platform, completion| platform.copy_image(png, completion)).await
        })
    }

    fn image_bytes(&self, image: PageImage) -> Promise<Value> {
        match encode_png(&image) {
            Ok(png) => Promise::resolved(Bytes(png).to_json()),
            Err(error) => Promise::rejected(error),
        }
    }

    fn save_file(&self, bytes: Value, file_name: String, content_type: String) {
        self.save(bytes, file_name, content_type);
    }
}

/// A rendered picture's pixels, out of the `HostTable`, as a PNG.
pub fn encode_png(image: &PageImage) -> Result<Vec<u8>, Error> {
    let json = image.json();
    let whole = |key: &str| {
        json.get(key).and_then(Value::as_u64).and_then(|n| u32::try_from(n).ok()).ok_or_else(|| {
            Error::new(format!("[epdf-viewer-host] a picture's {key} is not a u32: {json}"))
        })
    };
    if json.get("color").and_then(Value::as_str) != Some("rgba8")
        || json.get("premultipliedAlpha").and_then(Value::as_bool) != Some(false)
    {
        return Err(Error::new(format!(
            "[epdf-viewer-host] a picture this host cannot encode: {json}"
        )));
    }
    let (width, height, stride) = (whole("width")?, whole("height")?, whole("stride")?);
    let pixels =
        json.get("$host").and_then(Value::as_u64).and_then(HostTable::get).ok_or_else(|| {
            Error::new(format!("[epdf-viewer-host] the host holds no pixels for {json}"))
        })?;
    let row = width as usize * 4;
    let mut packed = Vec::with_capacity(row * height as usize);
    for y in 0..height as usize {
        let start = y * stride as usize;
        let line = pixels.get(start..start + row).ok_or_else(|| {
            Error::new(format!("[epdf-viewer-host] row {y} is past the end of the picture"))
        })?;
        packed.extend_from_slice(line);
    }
    let mut png = Vec::new();
    {
        let mut encoder = png::Encoder::new(&mut png, width, height);
        encoder.set_color(png::ColorType::Rgba);
        encoder.set_depth(png::BitDepth::Eight);
        let mut writer = encoder.write_header().map_err(|e| Error::new(e.to_string()))?;
        writer.write_image_data(&packed).map_err(|e| Error::new(e.to_string()))?;
    }
    Ok(png)
}

/// The lists named, joined in upstream's order whatever order they are
/// named in. On the kernel thread.
pub fn standard_commands(
    lists: &[StandardCommandList],
    slot: StandardCommandsSlot,
    post: KernelPost,
) -> Vec<CommandItem> {
    let platform = Rc::new(NativePlatform { slot, post });
    let mut out: Vec<CommandItem> = Vec::new();
    for list in StandardCommandList::ALL.into_iter().filter(|l| lists.contains(l)) {
        match list {
            StandardCommandList::Stage => out.extend(
                epdf_plugin_stage::commands::create_stage_commands().into_iter().map(Into::into),
            ),
            StandardCommandList::Interaction => {
                out.extend(epdf_plugin_interaction::commands::create_interaction_commands())
            }
            StandardCommandList::Selection => {
                out.extend(create_selection_commands(platform.clone()).into_iter().map(Into::into))
            }
            StandardCommandList::Annotation => out.extend(
                epdf_plugin_annotation::commands::create_annotation_commands()
                    .into_iter()
                    .map(Into::into),
            ),
            StandardCommandList::History => out.extend(
                epdf_plugin_history::commands::create_history_commands()
                    .into_iter()
                    .map(Into::into),
            ),
            StandardCommandList::Render => {
                out.extend(create_render_commands(platform.clone()).into_iter().map(Into::into))
            }
            StandardCommandList::Document => {
                out.extend(create_document_commands(platform.clone()).into_iter().map(Into::into))
            }
        }
    }
    out
}
