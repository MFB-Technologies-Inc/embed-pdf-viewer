//! `services/groups.ts`: the form's groups as people see them. A field's
//! group is an id in the PDF (`groupId`); who the group is and the color its
//! fields show in are the app's, told to this document with `setGroups()`,
//! and never written into the PDF. Every group shows in a color: its own
//! when the app gives one, else one of eight by its place in the list, so no
//! two of the first eight share one. How strongly it shows is the
//! `fields.tint` setting; at `0` no field shows its group at all.

use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::error::plugin_error;
use serde_json::{Value, json};

use crate::controller::CAPABILITY;

/// `GROUP_COLORS`: the colors of the groups the app gives none. The first
/// group in the list gets the first, and the ninth the first again.
pub const GROUP_COLORS: [&str; 8] =
    ["#ea580c", "#16a34a", "#9333ea", "#2563eb", "#db2777", "#0d9488", "#ca8a04", "#65a30d"];

const MODIFY: &str = "doc.forms.modify";

/// `GroupLook`: a field's group as its boxes show it: who it is, and the
/// accent its fields show in (`RecordAccent`, `{ color, strength }`), `null`
/// while `fields.tint` is `0`.
#[derive(Clone, Debug, PartialEq)]
pub struct GroupLook {
    pub label: Option<String>,
    pub accent: Value,
}

/// `list()`'s memo: the list it made, and the named groups and used ids it
/// was made from.
#[derive(Default)]
pub(crate) struct GroupMemo {
    list: Option<(Value, String, Rc<Vec<Value>>)>,
}

impl crate::controller::Core {
    /// The ids of the groups the form's fields are in, in the fields' order,
    /// as one key (`used()`).
    fn used_groups(&self) -> String {
        let mut ids: Vec<&str> = Vec::new();
        let index = self.view();
        for field in index.fields() {
            if let Some(id) = field.get("groupId").and_then(Value::as_str)
                && !ids.contains(&id)
            {
                ids.push(id);
            }
        }
        serde_json::to_string(&ids).unwrap_or_default()
    }

    /// `groups.list()`, `listGroups()`: the groups the app named, in its
    /// order, then the other groups the form's fields are in. The same list
    /// while nothing changes.
    pub fn list_groups(&self) -> Rc<Vec<Value>> {
        let named = self.cx.state().get("groups").cloned().unwrap_or_else(|| json!([]));
        let used = self.used_groups();
        if let Some((seen_named, seen_used, list)) = &self.group_memo.borrow().list
            && *seen_named == named
            && *seen_used == used
        {
            return list.clone();
        }
        let named_list = named.as_array().cloned().unwrap_or_default();
        let known: Vec<&str> = named_list.iter().filter_map(|g| g["id"].as_str()).collect();
        let others: Vec<Value> = serde_json::from_str::<Vec<String>>(&used)
            .unwrap_or_default()
            .into_iter()
            .filter(|id| !known.contains(&id.as_str()))
            .map(|id| json!({ "id": id, "label": null, "color": null }))
            .collect();
        let list: Vec<Value> = named_list
            .iter()
            .chain(others.iter())
            .enumerate()
            .map(|(place, group)| {
                let color = match group.get("color") {
                    Some(Value::String(color)) => json!(color),
                    _ => json!(GROUP_COLORS[place % GROUP_COLORS.len()]),
                };
                json!({
                    "id": group["id"],
                    "label": group.get("label").cloned().unwrap_or(Value::Null),
                    "color": color,
                })
            })
            .collect();
        let list = Rc::new(list);
        self.group_memo.borrow_mut().list = Some((named, used, list.clone()));
        list
    }

    /// `groups.lookOf(groupId)`: how a group's fields show; `None` for no
    /// group, and for one the list doesn't have.
    pub(crate) fn group_look_of(&self, group_id: Option<&str>) -> Option<GroupLook> {
        let group_id = group_id?;
        let strength = self
            .settings_now()
            .get("fields")
            .and_then(|f| f.get("tint"))
            .and_then(Value::as_f64)
            .unwrap_or(f64::NAN);
        let list = self.list_groups();
        let group = list.iter().find(|g| g["id"].as_str() == Some(group_id))?;
        Some(GroupLook {
            label: group["label"].as_str().map(str::to_string),
            accent: if strength > 0.0 {
                json!({ "color": group["color"], "strength": crate::value::number(strength) })
            } else {
                Value::Null
            },
        })
    }

    /// `maySetGroup(groupId)`: the engine's own answer.
    fn may_set_group(&self, group_id: &str) -> bool {
        self.cx.allows_field("set-group", &json!({ "groupId": group_id }))
    }

    /// `groups.maySet(groupId)`, `canSetGroup()`: the session may design the
    /// form, and the group is its own or one a `fields:set-group`
    /// permission names.
    pub fn can_set_group(&self, group_id: &str) -> bool {
        self.allows(MODIFY) && self.may_set_group(group_id)
    }

    /// `groups.assertMaySet(groupId, operation)`.
    pub(crate) fn assert_may_set_group(
        &self,
        group_id: &str,
        operation: &str,
    ) -> Result<(), Error> {
        self.assert_allowed(MODIFY, operation)?;
        if self.may_set_group(group_id) {
            return Ok(());
        }
        let permission = format!("fields:set-group:group={group_id}");
        Err(plugin_error(
            "permission-denied",
            CAPABILITY,
            format!("{operation} requires {permission}"),
            None,
            None,
        )
        .with("permission", permission))
    }

    /// The design group's and the groups' events, derived from each
    /// committed state change (`createEvents`, `createGroups`).
    pub(crate) fn wire_groups(&self) -> Result<(), Error> {
        let weak = self.weak();
        self.cx.on_state_change(move |next, previous| {
            let Some(core) = weak.upgrade() else { return Ok(()) };
            if next.get("designGroup") != previous.get("designGroup") {
                let group = next.get("designGroup").cloned().unwrap_or(Value::Null);
                core.events.design_group_changed.emit(&json!({ "groupId": group }));
            }
            if next.get("groups") != previous.get("groups") {
                let groups = core.list_groups();
                core.events.groups_changed.emit(&json!({ "groups": *groups }));
            }
            Ok(())
        });
        Ok(())
    }
}
