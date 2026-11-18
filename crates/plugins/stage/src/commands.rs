//! `commands.ts`: the Stage's commands (`d6325f5ba`, `2e65f29e8`): zoom,
//! moving through the pages and turning the view, with their shortcuts. The
//! i18n plugin has their labels.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_plugin_commands::{CommandCtx, CommandDef, Derivation, RunFn, Shortcut};
use epdf_stage_core::ZoomMode;

use crate::{STAGE, StageCapability, StageZoomMode};

fn stage_of(context: &CommandCtx) -> Option<StageCapability> {
    context.try_get(&STAGE)
}

/// `hasStage`: enabled while the document has a Stage.
fn has_stage() -> Derivation {
    Rc::new(|context| Ok(stage_of(context).is_some()))
}

/// `run: (context) => stageOf(context)?.verb()`.
fn on_stage(verb: impl Fn(&StageCapability) -> Result<(), Error> + 'static) -> Option<RunFn> {
    Some(Rc::new(move |context| {
        if let Some(stage) = stage_of(context) {
            verb(&stage)?;
        }
        Ok(None)
    }))
}

/// `enabled: (context) => stageOf(context)?.can() ?? false`.
fn when_stage(can: impl Fn(&StageCapability) -> bool + 'static) -> Option<Derivation> {
    Some(Rc::new(move |context| Ok(stage_of(context).is_some_and(|stage| can(&stage)))))
}

/// `active: (context) => stageOf(context)?.getZoomMode() === mode`.
fn zoom_mode_is(mode: ZoomMode) -> Option<Derivation> {
    Some(Rc::new(move |context| {
        Ok(stage_of(context)
            .is_some_and(|stage| stage.get_zoom_mode() == StageZoomMode::Mode(mode)))
    }))
}

fn command(id: &str, label_key: &str, label: &str, icon: &str, category: &str) -> CommandDef {
    CommandDef {
        id: id.into(),
        label_key: Some(label_key.into()),
        label: Some(label.into()),
        icon: Some(icon.into()),
        categories: Some(vec![category.into()]),
        ..CommandDef::default()
    }
}

fn one(key: &str) -> Option<Shortcut> {
    Some(Shortcut::One(key.into()))
}

fn many(keys: &[&str]) -> Option<Shortcut> {
    Some(Shortcut::Many(keys.iter().map(|k| k.to_string()).collect()))
}

/// `createStageCommands()`: `zoom:*`, `page:*` and `view:rotate-*`.
pub fn create_stage_commands() -> Vec<CommandDef> {
    vec![
        // ── zoom ──
        CommandDef {
            shortcut: many(&["Mod+=", "Mod+NumpadAdd"]),
            run: on_stage(|stage| stage.zoom_in(None)),
            enabled: when_stage(StageCapability::can_zoom_in),
            ..command("zoom:in", "commands.zoom.in", "Zoom in", "zoom-in", "zoom")
        },
        CommandDef {
            shortcut: many(&["Mod+-", "Mod+NumpadSubtract"]),
            run: on_stage(|stage| stage.zoom_out(None)),
            enabled: when_stage(StageCapability::can_zoom_out),
            ..command("zoom:out", "commands.zoom.out", "Zoom out", "zoom-out", "zoom")
        },
        CommandDef {
            run: on_stage(StageCapability::fit_width),
            active: zoom_mode_is(ZoomMode::FitWidth),
            enabled: Some(has_stage()),
            ..command("zoom:fit-width", "commands.zoom.fitWidth", "Fit width", "fit-width", "zoom")
        },
        CommandDef {
            run: on_stage(StageCapability::fit_page),
            active: zoom_mode_is(ZoomMode::FitPage),
            enabled: Some(has_stage()),
            ..command("zoom:fit-page", "commands.zoom.fitPage", "Fit page", "fit-page", "zoom")
        },
        // ── pages ──
        CommandDef {
            shortcut: one("ArrowRight"),
            run: on_stage(|stage| {
                stage.next_page(None);
                Ok(())
            }),
            enabled: when_stage(StageCapability::can_go_next),
            ..command("page:next", "commands.page.next", "Next page", "next-page", "page")
        },
        CommandDef {
            shortcut: one("ArrowLeft"),
            run: on_stage(|stage| {
                stage.previous_page(None);
                Ok(())
            }),
            enabled: when_stage(StageCapability::can_go_previous),
            ..command(
                "page:previous",
                "commands.page.previous",
                "Previous page",
                "previous-page",
                "page",
            )
        },
        CommandDef {
            shortcut: one("Home"),
            run: on_stage(|stage| {
                stage.go_to_first_page(None);
                Ok(())
            }),
            enabled: when_stage(StageCapability::can_go_previous),
            ..command("page:first", "commands.page.first", "First page", "first-page", "page")
        },
        CommandDef {
            shortcut: one("End"),
            run: on_stage(|stage| {
                stage.go_to_last_page(None);
                Ok(())
            }),
            enabled: when_stage(StageCapability::can_go_next),
            ..command("page:last", "commands.page.last", "Last page", "last-page", "page")
        },
        // ── view rotation: how the pages show, nothing written to the PDF ──
        CommandDef {
            run: on_stage(|stage| stage.rotate_view_by(90.0)),
            enabled: Some(has_stage()),
            ..command(
                "view:rotate-clockwise",
                "commands.view.rotateClockwise",
                "Rotate clockwise",
                "rotate-clockwise",
                "view",
            )
        },
        CommandDef {
            run: on_stage(|stage| stage.rotate_view_by(-90.0)),
            enabled: Some(has_stage()),
            ..command(
                "view:rotate-counterclockwise",
                "commands.view.rotateCounterclockwise",
                "Rotate counterclockwise",
                "rotate-counterclockwise",
                "view",
            )
        },
    ]
}
