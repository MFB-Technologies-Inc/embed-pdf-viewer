//! `commands.ts`: a command for every tool (`d6325f5ba`, `2e65f29e8`): the
//! built-in tools with Acrobat's single keys, and `tool:<id>` for every
//! other tool the document has. [`tool_command`] is for a plugin whose tool
//! gets a command of its own, with a shortcut or a permission. The i18n
//! plugin has their labels.

use std::cell::RefCell;
use std::rc::{Rc, Weak};

use epdf_plugin_commands::{CommandCtx, CommandDef, CommandFamily, CommandItem, Shortcut};

use crate::INTERACTION;
use crate::contract::Tool;
use crate::controller::InteractionCapability;

fn interaction_of(context: &CommandCtx) -> Option<InteractionCapability> {
    context.try_get(&INTERACTION)
}

/// `toolCommand(toolId)`: switching to a tool, active while it's the tool,
/// there only when the document has it. The caller sets `id` and
/// `shortcut` (`Omit<CommandDefinition, 'id' | 'shortcut'>`).
pub fn tool_command(tool_id: &str) -> CommandDef {
    let id = tool_id.to_string();
    let (run_id, active_id, visible_id, enabled_id) =
        (id.clone(), id.clone(), id.clone(), id.clone());
    CommandDef {
        label_key: Some(format!("commands.tool.{tool_id}")),
        label: Some(id.clone()),
        icon: Some(id),
        categories: Some(vec!["tool".into()]),
        run: Some(Rc::new(move |context| {
            if let Some(interaction) = interaction_of(context) {
                interaction.activate_tool(&run_id)?;
            }
            Ok(None)
        })),
        active: Some(Rc::new(move |context| {
            Ok(interaction_of(context).is_some_and(|i| i.get_active_tool_id() == active_id))
        })),
        visible: Some(Rc::new(move |context| {
            Ok(interaction_of(context).is_some_and(|i| i.has_tool(&visible_id)))
        })),
        // The tool says whether the user may use it: one that draws annotations needs permission to.
        enabled: Some(Rc::new(move |context| {
            Ok(interaction_of(context).is_none_or(|i| i.can_use_tool(&enabled_id)))
        })),
        ..CommandDef::default()
    }
}

/// The ids of a tool list, made once per list, which keeps its identity
/// while the tools don't change (the TypeScript's `WeakMap`).
type ToolIds = RefCell<Vec<(Weak<[Rc<Tool>]>, Rc<Vec<String>>)>>;

fn ids_of(cache: &ToolIds, tools: &Rc<[Rc<Tool>]>) -> Rc<Vec<String>> {
    let mut cache = cache.borrow_mut();
    cache.retain(|(list, _)| list.strong_count() > 0);
    if let Some((_, ids)) =
        cache.iter().find(|(list, _)| std::ptr::eq(list.as_ptr(), Rc::as_ptr(tools)))
    {
        return ids.clone();
    }
    let ids = Rc::new(tools.iter().map(|tool| tool.id.clone()).collect::<Vec<_>>());
    cache.push((Rc::downgrade(tools), ids.clone()));
    ids
}

/// `tool:<id>` for every tool the document has.
fn tool_commands() -> CommandFamily {
    let cache: Rc<ToolIds> = Rc::default();
    CommandFamily {
        prefix: "tool:".into(),
        names: Rc::new(move |context| {
            Ok(match interaction_of(context) {
                Some(interaction) => ids_of(&cache, &interaction.list_tools()),
                None => Rc::new(Vec::new()),
            })
        }),
        command: Rc::new(tool_command),
    }
}

/// `createInteractionCommands()`: `tool:pointer`, `tool:pan`, `tool:zoom`,
/// and `tool:<id>` for every other tool.
pub fn create_interaction_commands() -> Vec<CommandItem> {
    let builtin = |tool_id: &str, shortcut: &str| CommandDef {
        id: format!("tool:{tool_id}"),
        shortcut: Some(Shortcut::One(shortcut.into())),
        ..tool_command(tool_id)
    };
    vec![
        // The built-in tools have Acrobat's single keys; the family covers every other.
        builtin("pointer", "V").into(),
        builtin("pan", "H").into(),
        builtin("zoom", "Z").into(),
        tool_commands().into(),
    ]
}
