//! `commands.ts`: the selection's command, copy (`d6325f5ba`,
//! `2e65f29e8`). It reaches the clipboard, which plugins never touch, so the
//! list is made with the host's part. The i18n plugin has its label.

use std::rc::Rc;

use epdf_kernel::rt::Promise;
use epdf_plugin_commands::{CommandDef, Shortcut};
use serde_json::Value;

use crate::{SELECTION, SelectionCapability};

/// `SelectionCommandsPlatform`: what the selection's commands need from the
/// host.
pub trait SelectionCommandsPlatform {
    /// Copy the selected text to the clipboard.
    fn copy_selection(&self, selection: &SelectionCapability) -> Promise<Value>;
}

/// `createSelectionCommands(platform)`: `selection:copy`, with the host's
/// part from `platform`.
pub fn create_selection_commands(platform: Rc<dyn SelectionCommandsPlatform>) -> Vec<CommandDef> {
    vec![CommandDef {
        id: "selection:copy".into(),
        label_key: Some("commands.selection.copy".into()),
        label: Some("Copy".into()),
        icon: Some("copy".into()),
        shortcut: Some(Shortcut::One("Mod+C".into())),
        categories: Some(vec!["selection".into()]),
        run: Some(Rc::new(move |context| {
            Ok(Some(platform.copy_selection(&context.get(&SELECTION)?)))
        })),
        // `can_copy()` in `enabled`, not `allowed`: a refused command keeps its keys, and Mod+C
        // with nothing selected in the viewer belongs to the page. The clipboard wiring says
        // when a selection may not be copied.
        enabled: Some(Rc::new(|context| {
            Ok(context
                .try_get(&SELECTION)
                .is_some_and(|selection| selection.has_selection() && selection.can_copy()))
        })),
        ..CommandDef::default()
    }]
}
