//! `commands.ts`: the snapshot's commands (`d6325f5ba`, `2e65f29e8`): the
//! snapshot tool, and copy, save and clear for the snapshot it holds. Copy
//! and save reach the clipboard and the file system, which plugins never
//! touch, so the list is made with the host's part. The i18n plugin has
//! their labels.

use std::rc::Rc;

use epdf_kernel::rt::Promise;
use epdf_kernel::{DOCUMENTS, undefined};
use epdf_plugin_commands::{CommandCtx, CommandDef, Shortcut};
use epdf_plugin_interaction::commands::tool_command;
use serde_json::Value;

use crate::{PageImage, RENDER, SNAPSHOT_TOOL, Snapshot};

/// `RenderCommandsPlatform`: what the snapshot's commands need from the
/// host.
pub trait RenderCommandsPlatform {
    /// Copy an image to the clipboard. Called in the press or key itself,
    /// before the image is there: a browser may take the clipboard only then.
    fn copy_image(&self, image: Promise<PageImage>) -> Promise<Value>;
    /// Give the user the image as a file of the given content type. The
    /// TypeScript hands over the image's `blob()`; the host reads the
    /// picture from the image here.
    fn save_image(&self, image: PageImage, file_name: String, content_type: String);
}

fn snapshot_of(context: &CommandCtx) -> Option<Snapshot> {
    context.try_get(&RENDER).and_then(|render| render.get_snapshot())
}

fn has_snapshot() -> epdf_plugin_commands::Derivation {
    Rc::new(|context| Ok(snapshot_of(context).is_some()))
}

/// `createRenderCommands(platform)`: `tool:snapshot` and `snapshot:*`, with
/// the host's part from `platform`.
pub fn create_render_commands(platform: Rc<dyn RenderCommandsPlatform>) -> Vec<CommandDef> {
    let copy = platform.clone();
    vec![
        // The snapshot tool, for a user who may take snapshots.
        CommandDef {
            id: "tool:snapshot".into(),
            allowed: Some(Rc::new(|context| {
                Ok(context.try_get(&RENDER).is_none_or(|render| render.can_snapshot()))
            })),
            not_allowed_key: Some("commands.notAllowed.doc-content-copy".into()),
            ..tool_command(SNAPSHOT_TOOL)
        },
        // ── the snapshot the snapshot tool holds ──
        CommandDef {
            id: "snapshot:copy".into(),
            label_key: Some("commands.snapshot.copy".into()),
            label: Some("Copy".into()),
            icon: Some("copy".into()),
            shortcut: Some(Shortcut::One("Mod+C".into())),
            categories: Some(vec!["snapshot".into()]),
            // Straight to the clipboard, in the press itself: the picture follows when it's there.
            run: Some(Rc::new(move |context| {
                Ok(snapshot_of(context).map(|snapshot| copy.copy_image(snapshot.image)))
            })),
            enabled: Some(has_snapshot()),
            ..CommandDef::default()
        },
        CommandDef {
            id: "snapshot:save".into(),
            label_key: Some("commands.snapshot.save".into()),
            label: Some("Save image".into()),
            icon: Some("download".into()),
            categories: Some(vec!["snapshot".into()]),
            // An `async` run: what it throws rejects the promise it returns.
            run: Some(Rc::new(move |context| {
                let (context, platform) = (context.clone(), platform.clone());
                Ok(Some(Promise::spawn(async move {
                    let Some(snapshot) = snapshot_of(&context) else { return Ok(undefined()) };
                    let documents = context.get(&DOCUMENTS)?;
                    let id = context.document_id().map(str::to_string);
                    let image = snapshot.image.clone().await?;
                    let pages = documents.list_pages(id.as_deref());
                    let index = pages.as_array().and_then(|pages| {
                        pages.iter().position(|page| {
                            page["ref"]["objectNumber"].as_u64()
                                == Some(u64::from(snapshot.page.page_object_number))
                        })
                    });
                    let number = index.map_or(0, |index| index + 1);
                    let name = documents.get_document(id.as_deref()).and_then(|doc| doc.name);
                    platform.save_image(
                        image,
                        snapshot_file_name_of(name.as_deref(), number),
                        "image/png".into(),
                    );
                    Ok(undefined())
                })))
            })),
            enabled: Some(has_snapshot()),
            ..CommandDef::default()
        },
        CommandDef {
            id: "snapshot:clear".into(),
            label_key: Some("commands.snapshot.clear".into()),
            label: Some("Clear snapshot".into()),
            shortcut: Some(Shortcut::One("Escape".into())),
            categories: Some(vec!["snapshot".into()]),
            run: Some(Rc::new(|context| {
                context.get(&RENDER)?.clear_snapshot();
                Ok(None)
            })),
            enabled: Some(has_snapshot()),
            ..CommandDef::default()
        },
    ]
}

/// A snapshot's file name: the document's name and the page's number, as a
/// PNG (`Report p3.png`).
fn snapshot_file_name_of(name: Option<&str>, page_number: usize) -> String {
    let name = name.unwrap_or("document");
    let base = match name.len().checked_sub(4) {
        Some(at) if name.is_char_boundary(at) && name[at..].eq_ignore_ascii_case(".pdf") => {
            &name[..at]
        }
        _ => name,
    };
    format!("{base} p{page_number}.png")
}

#[cfg(test)]
mod tests {
    use super::snapshot_file_name_of;

    /// `snapshotFileNameOf`, as `plugin/render/test/commands.test.ts` saves `Report p2.png`.
    #[test]
    fn a_snapshot_is_named_after_the_document_and_its_page() {
        assert_eq!(snapshot_file_name_of(Some("Report.pdf"), 2), "Report p2.png");
        assert_eq!(snapshot_file_name_of(Some("Report.Pdf"), 1), "Report p1.png");
        assert_eq!(snapshot_file_name_of(Some("Report"), 3), "Report p3.png");
        assert_eq!(snapshot_file_name_of(None, 0), "document p0.png");
    }
}
