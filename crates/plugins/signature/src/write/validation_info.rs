//! `write/validation-info.ts`: keeping a signature checkable with no
//! network. The validation data the `revocation` setting asks for, and the
//! document timestamp after it. One routine serves two doors.
//! `addValidationInfo()` rejects when a signature can't be covered. The
//! follow-up to a seal never does: the signature is already in the
//! document, so its result carries the failure.

use std::cell::RefCell;
use std::rc::Rc;

use epdf_kernel::Error;
use epdf_kernel::error::{code, plugin_error, to_plugin_error};
use epdf_kernel::rt::Promise;
use epdf_kernel::signal::AbortSignal;
use epdf_signature_core::{
    AddValidationInfoOptions, CmsSigner, RevocationSource, SignerPort, TimestampOptions, TrustPort,
    add_validation_info, sign_with,
};
use serde_json::{Map, Value, json};

use crate::contract::OperationOptions;
use crate::controller::SignatureCapability;
use crate::js::truthy;
use crate::model::same_field_ref;
use crate::promise::adopt;
use crate::services::errors::verb;
use crate::sync::validation::import_module;

/// The settings both steps need.
struct Needed {
    revocation: Rc<dyn RevocationSource>,
    trust: Rc<dyn TrustPort>,
    stamp_with: Option<CmsSigner>,
}

/// `{ added, documentTimestamp }`, `ValidationInfoAdded`.
fn added_json(added: &Value, document_timestamp: Value) -> Value {
    json!({ "added": added, "documentTimestamp": document_timestamp })
}

/// A count of `added`, as JavaScript adds it.
fn count(added: &Value, key: &str) -> f64 {
    added.get(key).and_then(Value::as_f64).unwrap_or(0.0)
}

impl SignatureCapability {
    /// `requireSettings()`: the settings both steps need, or the reason
    /// nothing can be added.
    fn require_validation_settings(&self) -> Result<Needed, Error> {
        self.cx().assert_allowed("doc.sign", "signature.addValidationInfo")?;
        let settings = self.settings();
        let (Some(revocation), Some(trust)) = (settings.revocation, settings.trust) else {
            return Err(plugin_error(
                code::INVALID_INPUT,
                "signature",
                "adding validation data needs the `revocation` and `trust` settings",
                None,
                None,
            ));
        };
        if settings.document_timestamp && settings.timestamp_authority.is_none() {
            return Err(plugin_error(
                code::INVALID_INPUT,
                "signature",
                "the `documentTimestamp` setting needs the `timestampAuthority` setting",
                None,
                None,
            ));
        }
        let stamp_with =
            if settings.document_timestamp { settings.timestamp_authority } else { None };
        Ok(Needed { revocation, trust, stamp_with })
    }

    /// `add(fields, signal, stored)`: the two steps, in order. Ask for the
    /// answers `fields` lack (every signature's without `fields`) and store
    /// them, then timestamp the document when that is set and the answers
    /// aren't covered by a timestamp. `stored` hears of the first step when
    /// it is done, so a caller knows what is in the file when the second one
    /// fails.
    fn add_validation_data(
        &self,
        fields: Option<Vec<Value>>,
        signal: Option<AbortSignal>,
        stored: Option<Rc<RefCell<Value>>>,
    ) -> Promise<Value> {
        let this = self.clone();
        Promise::spawn(async move {
            let Needed { revocation, trust, stamp_with } = this.require_validation_settings()?;
            // The cryptography loads the first time it is needed.
            this.cancellable(
                signal.clone(),
                Promise::spawn(async {
                    import_module().await;
                    Ok(())
                }),
            )
            .await?;
            let doc = this.cx().doc()?;
            let options = AddValidationInfoOptions {
                trust: Some(trust.clone()),
                revocation: Some(revocation),
                fields: fields.clone(),
                ..Default::default()
            };
            let adding = Promise::spawn(add_validation_info(doc.clone(), options));
            let result = this.cancellable(signal.clone(), adding).await?;
            let added = result.get("added").cloned().unwrap_or(Value::Null);
            if let Some(stored) = &stored {
                *stored.borrow_mut() = added_json(&added, Value::Null);
            }
            let Some(stamp_with) = stamp_with else { return Ok(added_json(&added, Value::Null)) };

            // The signatures the answers are for: each one named, or every
            // one that isn't itself a document timestamp.
            let verdicts = result.get("verdicts").and_then(Value::as_array).cloned();
            let covered: Vec<Value> = verdicts
                .unwrap_or_default()
                .into_iter()
                .filter(|verdict| {
                    let signature = verdict.get("signature").unwrap_or(&Value::Null);
                    match &fields {
                        Some(fields) => fields.iter().any(|field| {
                            if field.get("kind").and_then(Value::as_str) == Some("fqn") {
                                signature.get("fieldName") == field.get("name")
                            } else {
                                same_field_ref(
                                    signature.get("field").unwrap_or(&Value::Null),
                                    field,
                                )
                            }
                        }),
                        None => signature.get("kind").and_then(Value::as_str) == Some("signature"),
                    }
                })
                .collect();
            // New answers have no timestamp over them yet, and neither has a
            // signature that never had one. Otherwise the last timestamp
            // still covers everything, and another proves no more.
            let uncovered = count(&added, "certificates")
                + count(&added, "ocspResponses")
                + count(&added, "crls")
                > 0.0
                || covered.iter().any(|verdict| {
                    verdict
                        .get("longTerm")
                        .and_then(|l| l.get("documentTimestampedAt"))
                        .is_none_or(Value::is_null)
                });
            if !uncovered {
                return Ok(added_json(&added, Value::Null));
            }
            let mut input = Map::new();
            input.insert("kind".into(), json!("timestamp"));
            let stamping = Promise::spawn(sign_with(
                doc,
                input,
                SignerPort::Cms(stamp_with),
                TimestampOptions { timestamp_authority: None, timestamp_trust: Some(trust) },
            ));
            let stamped = this.cancellable(signal, stamping).await?;
            let field = stamped
                .get("signature")
                .and_then(|s| s.get("field"))
                .cloned()
                .unwrap_or_else(epdf_kernel::undefined);
            Ok(added_json(&added, field))
        })
    }

    /// `addValidationInfo(field, options)`: keep a signed field checkable
    /// with no network. Ask, through the `revocation` setting, whether its
    /// certificates were good at its timestamp, and store the answers in the
    /// file; without `field`, for every signature. With `documentTimestamp`,
    /// a document timestamp follows when the answers aren't covered by one
    /// yet. All or nothing for the answers. Resolves `ValidationInfoAdded`
    /// (`{ added, documentTimestamp }`).
    pub fn add_validation_info(
        &self,
        field: Option<Value>,
        options: OperationOptions,
    ) -> Promise<Value> {
        let this = self.clone();
        verb(Promise::spawn(async move {
            let mut fields = None;
            if let Some(field) = field {
                let signature = this.find_signature(&field);
                let Some(signature) = signature.filter(|s| s.get("signed").is_some_and(truthy))
                else {
                    return Err(plugin_error(
                        code::NOT_FOUND,
                        "signature",
                        "no signed field at that address",
                        None,
                        None,
                    ));
                };
                fields = Some(vec![signature.get("field").cloned().unwrap_or(Value::Null)]);
            }
            let busy = this.clone();
            adopt(this.with_busy(move || {
                Promise::spawn(async move {
                    let result = busy.add_validation_data(fields, options.signal, None).await;
                    // Whatever was stored changes what the verdicts say: read
                    // them again at once.
                    busy.validate_now();
                    result
                })
            }))
            .await
        }))
    }

    /// `afterSeal(result, signal)`: what follows a seal. The validation data
    /// for the signature just made, when the settings ask for it and the
    /// session may add it. It never rejects: the answer is the seal's
    /// result with `validationInfo` and `validationInfoError`
    /// (`SignFieldResult`).
    pub(crate) async fn after_seal(&self, result: Value, signal: Option<AbortSignal>) -> Value {
        let with = |validation_info: Value, error: Value| {
            let mut out = match &result {
                Value::Object(o) => o.clone(),
                _ => Map::new(),
            };
            out.insert("validationInfo".into(), validation_info);
            out.insert("validationInfoError".into(), error);
            Value::Object(out)
        };
        if self.settings().revocation.is_none() || !self.cx().allows("doc.sign") {
            return with(Value::Null, Value::Null);
        }
        let field = result
            .get("signature")
            .and_then(|s| s.get("field"))
            .cloned()
            .unwrap_or_else(epdf_kernel::undefined);
        let stored = Rc::new(RefCell::new(Value::Null));
        let outcome =
            self.add_validation_data(Some(vec![field]), signal, Some(stored.clone())).await;
        let answer = match outcome {
            Ok(added) => with(added, Value::Null),
            Err(error) => {
                let stored = stored.borrow().clone();
                with(stored, to_plugin_error("signature", &error).to_canonical())
            }
        };
        self.validate_now();
        answer
    }
}
