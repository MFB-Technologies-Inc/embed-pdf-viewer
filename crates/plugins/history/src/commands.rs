//! `commands.ts`: undo and redo (`4c668404f`, `2e65f29e8`). Without the
//! history plugin, both stay disabled. Focus in a text field keeps its own
//! undo: shortcuts skip text fields. The i18n plugin has their labels.

use std::rc::Rc;

use epdf_plugin_commands::{CommandCtx, CommandDef, Shortcut};

use crate::contract::HISTORY;
use crate::controller::HistoryCapability;

fn history_of(context: &CommandCtx) -> Option<HistoryCapability> {
    context.try_get(&HISTORY)
}

/// `createHistoryCommands()`: `history:undo` and `history:redo`.
pub fn create_history_commands() -> Vec<CommandDef> {
    vec![
        CommandDef {
            id: "history:undo".into(),
            label_key: Some("commands.history.undo".into()),
            label: Some("Undo".into()),
            icon: Some("undo".into()),
            shortcut: Some(Shortcut::One("Mod+Z".into())),
            categories: Some(vec!["history".into()]),
            run: Some(Rc::new(|context| {
                if let Some(history) = history_of(context) {
                    history.undo()?;
                }
                Ok(None)
            })),
            enabled: Some(Rc::new(|context| Ok(history_of(context).is_some_and(|h| h.can_undo())))),
            ..CommandDef::default()
        },
        CommandDef {
            id: "history:redo".into(),
            label_key: Some("commands.history.redo".into()),
            label: Some("Redo".into()),
            icon: Some("redo".into()),
            shortcut: Some(Shortcut::Many(vec!["Mod+Shift+Z".into(), "Mod+Y".into()])),
            categories: Some(vec!["history".into()]),
            run: Some(Rc::new(|context| {
                if let Some(history) = history_of(context) {
                    history.redo()?;
                }
                Ok(None)
            })),
            enabled: Some(Rc::new(|context| Ok(history_of(context).is_some_and(|h| h.can_redo())))),
            ..CommandDef::default()
        },
    ]
}
