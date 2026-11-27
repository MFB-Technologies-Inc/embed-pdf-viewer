//! `write/groups.ts`: the form's groups, as the app sets them: who this
//! document's groups are (`setGroups()`), and who new fields are placed for
//! (the design group). Both are session state a toolbar sets and reads;
//! neither is written into the PDF. A field placed with a tool goes into the
//! design group (`tools/handlers.rs`); `null`, where it starts, names none,
//! so the field is in no group.

use epdf_kernel::Error;
use serde_json::{Value, json};

/// `namedGroupsOf(groups)`: the app's list as the state holds it, each id
/// once (the first entry of an id is the group). Each entry is a
/// `FormGroupInput`: `{ id, label?, color? }`.
fn named_groups_of(groups: &[Value]) -> Vec<Value> {
    let mut named: Vec<Value> = Vec::new();
    for group in groups {
        let id = group.get("id").cloned().unwrap_or(Value::Null);
        if named.iter().any(|g| g["id"] == id) {
            continue;
        }
        let or_null = |key: &str| match group.get(key) {
            Some(value) if !value.is_null() && !epdf_kernel::is_undefined(value) => value.clone(),
            _ => Value::Null,
        };
        named.push(json!({ "id": id, "label": or_null("label"), "color": or_null("color") }));
    }
    named
}

impl crate::controller::Core {
    /// `setGroups(groups)`: say who this document's groups are, the whole
    /// list; an empty one clears it. Fires `onGroupsChanged`; the same list
    /// again changes nothing.
    pub fn set_groups(&self, groups: &[Value]) {
        let named = named_groups_of(groups);
        self.cx.update_state(|state| crate::model::set_groups(state, named));
    }

    /// `getDesignGroup()`: the group fields placed with a tool go in, or
    /// `None`, where it starts.
    pub fn get_design_group(&self) -> Option<String> {
        self.cx.state().get("designGroup").and_then(Value::as_str).map(str::to_string)
    }

    /// `setDesignGroup(groupId)`: fields placed with a tool go into this
    /// group; `None` names none. Fires `onDesignGroupChanged`. Throws
    /// `permission-denied` for a group the session may not put a field in.
    pub fn set_design_group(&self, group_id: Option<&str>) -> Result<(), Error> {
        if let Some(group) = group_id {
            self.assert_may_set_group(group, "form.setDesignGroup")?;
        }
        self.cx.update_state(|state| crate::model::set_design_group(state, group_id));
        Ok(())
    }
}
