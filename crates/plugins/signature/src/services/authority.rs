//! `services/authority.ts`: the mode and the key, read from the settings
//! where they're used, so a change applies at once; and who may sign or
//! fill in which field, by the field's group (`ctx.allowsField`), as the
//! engine checks it.

use epdf_kernel::Error;
use epdf_kernel::error::{code, plugin_error};
use epdf_kernel::rt::Promise;
use std::rc::Rc;

use epdf_signature_core::{CmsSigner, SignerPort, TrustPort};
use serde_json::Value;

use crate::contract::{SignatureMode, SigningKey};
use crate::controller::SignatureCapability;
use crate::promise::adopt;

impl SignatureCapability {
    /// `mode()`: the `mode` setting, or `sign` with a key and `visual`
    /// without.
    pub fn get_mode(&self) -> SignatureMode {
        let settings = self.settings();
        settings.mode.unwrap_or(if settings.signing_key.is_some() {
            SignatureMode::Sign
        } else {
            SignatureMode::Visual
        })
    }

    /// `resolveKey(override)`: an `async` function. `invalid-input` without
    /// a key; a function key is called and its answer adopted.
    pub(crate) fn resolve_key(&self, override_: Option<SigningKey>) -> Promise<SignerPort> {
        let key = override_.or_else(|| self.settings().signing_key);
        Promise::spawn(async move {
            match key {
                None => Err(plugin_error(
                    code::INVALID_INPUT,
                    "signature",
                    "no key: set the `key` setting or pass one with the call",
                    None,
                    None,
                )),
                Some(SigningKey::Port(port)) => Ok((*port).clone()),
                Some(SigningKey::Thunk(thunk)) => adopt(thunk()).await,
            }
        })
    }

    /// `resolveTimestampAuthority(override)`: the timestamp authority of one
    /// sealing: the call's, else the setting's; `None` for none.
    pub(crate) fn resolve_timestamp_authority(
        &self,
        override_: Option<Option<CmsSigner>>,
    ) -> Option<CmsSigner> {
        match override_ {
            None => self.settings().timestamp_authority,
            Some(authority) => authority,
        }
    }

    /// `timestampTrust()`: who may timestamp what this plugin seals, the
    /// `trust` setting when there is one. An app that says who it trusts
    /// never seals a timestamp from someone else.
    pub(crate) fn timestamp_trust(&self) -> Option<Rc<dyn TrustPort>> {
        self.settings().trust
    }

    /// `allowsField(action, ref)`: whether the session may `action` this
    /// field; `false` for a field the form doesn't have.
    fn allows_field(&self, action: &str, reference: &Value) -> bool {
        self.form().get(reference).is_some_and(|field| self.cx().allows_field(action, &field))
    }

    /// `assertAllowedField(action, ref, operation)`: refuse a write to this
    /// field the session may not make, before anything starts. A field the
    /// form doesn't show yet is the engine's to answer (`not-found`).
    pub(crate) fn assert_allowed_field(
        &self,
        action: &str,
        reference: &Value,
        operation: &str,
    ) -> Result<(), Error> {
        match self.form().get(reference) {
            Some(field) => self.cx().assert_allowed_field(action, &field, operation),
            None => Ok(()),
        }
    }

    /// `canSign(field)`: `doc.sign`, or a `fields:sign` permission for its
    /// group. `false` for a field the form doesn't have.
    pub fn can_sign(&self, field: &Value) -> bool {
        self.allows_field("sign", field)
    }

    /// `canFill(field)`: a mark in a signature field is its signer's to
    /// place, so it takes what signing takes: `doc.sign`, or a `fields:sign`
    /// permission for its group.
    pub fn can_fill(&self, field: &Value) -> bool {
        self.allows_field("sign", field)
    }

    /// `canCertify()`: the `allowCertify` setting and `doc.sign.certify`.
    pub fn can_certify(&self) -> bool {
        self.settings().allow_certify && self.cx().allows("doc.sign.certify")
    }

    /// `canReadRevision()`: `doc.download`.
    pub fn can_read_revision(&self) -> bool {
        self.cx().allows("doc.download")
    }

    /// `canAddValidationInfo()`: someone to ask (the `revocation` setting),
    /// someone to trust (`trust`), and `doc.sign`.
    pub fn can_add_validation_info(&self) -> bool {
        let settings = self.settings();
        settings.revocation.is_some() && settings.trust.is_some() && self.cx().allows("doc.sign")
    }
}
