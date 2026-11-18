//! `commands.ts`: the annotation plugin's command (`d6325f5ba`,
//! `2e65f29e8`): delete the selected annotations. The tools have their
//! commands from the interaction plugin (`tool:<id>`). The i18n plugin has
//! its label.

use std::rc::Rc;

use epdf_plugin_commands::{CommandDef, Shortcut};

use crate::ANNOTATION;

/// `createAnnotationCommands()`: `annotation:delete`.
pub fn create_annotation_commands() -> Vec<CommandDef> {
    vec![CommandDef {
        id: "annotation:delete".into(),
        label_key: Some("commands.annotation.delete".into()),
        label: Some("Delete".into()),
        icon: Some("delete".into()),
        shortcut: Some(Shortcut::Many(vec!["Delete".into(), "Backspace".into()])),
        categories: Some(vec!["annotation".into()]),
        run: Some(Rc::new(|context| {
            let annotation = context.get(&ANNOTATION)?;
            Ok(Some(annotation.selection_delete(context.signal().cloned())))
        })),
        enabled: Some(Rc::new(|context| {
            let Some(annotation) = context.try_get(&ANNOTATION) else { return Ok(false) };
            let selected = annotation.list_selected();
            // `is_editable`: the annotation plugin's `editable` setting lets people change it by hand.
            Ok(!selected.is_empty()
                && selected.iter().all(|entry| {
                    let reference = &entry["ref"];
                    annotation.can_delete(reference) && annotation.is_editable(reference)
                }))
        })),
        ..CommandDef::default()
    }]
}
