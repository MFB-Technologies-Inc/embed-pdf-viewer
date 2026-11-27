//! A field anyone fills in (`core/src/forms/anyone.ts`, b45d916cb): every
//! signer may fill it in, not one group's. It is the third answer to "whose
//! field is this?", beside a group (one signer's) and no group (the
//! sender's).

use serde_json::{Map, Value};

use crate::dto::Patch;
use crate::error::{EngineError, EngineErrorCode};
use crate::features::forms::snapshot::FormFieldFamily;

fn invalid(message: &str) -> EngineError {
    EngineError::with_message(EngineErrorCode::InvalidArg, message)
        .with_details(serde_json::json!({ "field": "forAnyone" }))
}

/// `checkedForAnyone`: whether the field a write leaves is one anyone fills
/// in: what the write names, else what the field was (`was`, `false` for a
/// new field). `family` and `group_id` are the field as the write leaves it.
/// Refused with `InvalidArg`: a value that isn't `true` or `false`, and
/// `true` on a field in a group or on a signature field, which one signer
/// signs.
pub fn checked_for_anyone(
    named: &Patch<Value>,
    family: FormFieldFamily,
    group_id: Option<&str>,
    was: bool,
) -> Result<bool, EngineError> {
    let named = match named {
        Patch::Keep => None,
        Patch::Set(Value::Bool(value)) => Some(*value),
        Patch::Clear | Patch::Set(_) => {
            return Err(invalid("a field's forAnyone is true or false"));
        }
    };
    if !named.unwrap_or(was) {
        return Ok(false);
    }
    if family == FormFieldFamily::Signature {
        return Err(invalid(
            "a signature field is signed by one signer, so it is never for anyone",
        ));
    }
    if group_id.is_some() {
        return Err(invalid(
            "a field in a group is not also for anyone: send groupId: null with forAnyone: true, \
             or forAnyone: false with a groupId",
        ));
    }
    Ok(true)
}

/// `withForAnyone`: a field as something older than `forAnyone` wrote it,
/// made whole: no field was for anyone then. A field that has it comes back
/// as it is.
pub fn with_for_anyone(field: &mut Map<String, Value>) {
    field.entry("forAnyone").or_insert(Value::Bool(false));
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;

    fn set(value: Value) -> Patch<Value> {
        Patch::Set(value)
    }

    #[test]
    fn what_the_write_names_else_what_the_field_was() {
        use FormFieldFamily::{Pushbutton, Text};
        assert!(!checked_for_anyone(&Patch::Keep, Text, None, false).unwrap());
        assert!(checked_for_anyone(&set(json!(true)), Text, None, false).unwrap());
        assert!(!checked_for_anyone(&set(json!(false)), Text, None, true).unwrap());
        assert!(checked_for_anyone(&Patch::Keep, Text, None, true).unwrap());
        assert!(checked_for_anyone(&set(json!(true)), Pushbutton, None, false).unwrap());
    }

    #[test]
    fn refuses_a_field_in_a_group_a_signature_field_and_a_value_that_is_not_a_boolean() {
        use FormFieldFamily::{Signature, Text};
        for (named, family, group, was) in [
            (set(json!(true)), Text, Some("dana"), false),
            // A group named for a field that is for anyone, without `false`.
            (Patch::Keep, Text, Some("dana"), true),
            (set(json!(true)), Signature, None, false),
            (set(json!("yes")), Text, None, false),
            (Patch::Clear, Text, None, false),
        ] {
            let error = checked_for_anyone(&named, family, group, was).unwrap_err();
            assert_eq!(error.code, EngineErrorCode::InvalidArg);
            assert_eq!(error.details, Some(json!({ "field": "forAnyone" })));
        }
        // `false` is fine anywhere.
        assert!(!checked_for_anyone(&set(json!(false)), Signature, Some("dana"), false).unwrap());
    }

    #[test]
    fn gives_an_older_field_for_anyone_false_and_leaves_a_whole_one_as_it_is() {
        let mut older = json!({ "name": "a" }).as_object().unwrap().clone();
        with_for_anyone(&mut older);
        assert_eq!(Value::Object(older), json!({ "name": "a", "forAnyone": false }));
        let mut whole = json!({ "name": "a", "forAnyone": true }).as_object().unwrap().clone();
        with_for_anyone(&mut whole);
        assert_eq!(Value::Object(whole), json!({ "name": "a", "forAnyone": true }));
    }
}
