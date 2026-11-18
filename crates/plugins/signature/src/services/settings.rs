//! The signature settings in the kernel's settings store.
//!
//! The kernel keeps a registration's settings as one JSON value
//! (`epdf_kernel::settings`). `mode`, `documentTimestamp` and
//! `allowCertify` are plain data; the `key` (a signer port or a function),
//! the timestamp authority, the revocation source and the `trust` port are
//! not, so each is stored as a handle, `{"$signatureKey": n}`,
//! `{"$signatureAuthority": n}`, `{"$signatureRevocation": n}` or
//! `{"$signatureTrust": n}`, into a table the registration owns (one per
//! `signature_plugin(config)`, shared by its instances as the store is).
//! The same port reads back as the same value. All four are declared
//! `whole`, as `signature.plugin.ts` declares them: a change replaces them.

use std::cell::{Cell, RefCell};
use std::rc::Rc;

use epdf_kernel::settings::Settings;
use epdf_signature_core::{CmsSigner, RevocationSource, TrustPort};
use serde_json::{Map, Value, json};

use crate::contract::{SignatureConfig, SignatureMode, SignatureSettings, SigningKey};

const KEY_HANDLE: &str = "$signatureKey";
const TRUST_HANDLE: &str = "$signatureTrust";
const AUTHORITY_HANDLE: &str = "$signatureAuthority";
const REVOCATION_HANDLE: &str = "$signatureRevocation";

/// The registration's ports, by handle.
#[derive(Default)]
pub struct PortTable {
    keys: RefCell<Vec<(u64, SigningKey)>>,
    trusts: RefCell<Vec<(u64, Rc<dyn TrustPort>)>>,
    authorities: RefCell<Vec<(u64, CmsSigner)>>,
    revocations: RefCell<Vec<(u64, Rc<dyn RevocationSource>)>>,
    next: Cell<u64>,
}

/// One port kind's handles: find the port by identity, or mint a handle.
fn handle_of<T: Clone>(
    table: &RefCell<Vec<(u64, T)>>,
    port: &T,
    same: impl Fn(&T, &T) -> bool,
    mint: impl FnOnce() -> u64,
) -> u64 {
    let found = table.borrow().iter().find(|(_, p)| same(p, port)).map(|(n, _)| *n);
    found.unwrap_or_else(|| {
        let n = mint();
        table.borrow_mut().push((n, port.clone()));
        n
    })
}

fn port_at<T: Clone>(table: &RefCell<Vec<(u64, T)>>, value: &Value, handle: &str) -> Option<T> {
    let n = value.get(handle).and_then(Value::as_u64)?;
    table.borrow().iter().find(|(m, _)| *m == n).map(|(_, p)| p.clone())
}

impl PortTable {
    fn mint(&self) -> u64 {
        let n = self.next.get();
        self.next.set(n + 1);
        n
    }

    fn key_json(&self, key: &Option<SigningKey>) -> Value {
        let Some(key) = key else { return Value::Null };
        let found = self.keys.borrow().iter().find(|(_, k)| k.same(key)).map(|(n, _)| *n);
        let n = found.unwrap_or_else(|| {
            let n = self.mint();
            self.keys.borrow_mut().push((n, key.clone()));
            n
        });
        json!({ KEY_HANDLE: n })
    }

    fn trust_json(&self, trust: &Option<Rc<dyn TrustPort>>) -> Value {
        let Some(trust) = trust else { return Value::Null };
        let found = self
            .trusts
            .borrow()
            .iter()
            .find(|(_, t)| std::ptr::addr_eq(Rc::as_ptr(t), Rc::as_ptr(trust)))
            .map(|(n, _)| *n);
        let n = found.unwrap_or_else(|| {
            let n = self.mint();
            self.trusts.borrow_mut().push((n, trust.clone()));
            n
        });
        json!({ TRUST_HANDLE: n })
    }

    fn authority_json(&self, authority: &Option<CmsSigner>) -> Value {
        let Some(authority) = authority else { return Value::Null };
        let same = |a: &CmsSigner, b: &CmsSigner| Rc::ptr_eq(&a.sign, &b.sign) && a.hash == b.hash;
        json!({ AUTHORITY_HANDLE: handle_of(&self.authorities, authority, same, || self.mint()) })
    }

    fn revocation_json(&self, source: &Option<Rc<dyn RevocationSource>>) -> Value {
        let Some(source) = source else { return Value::Null };
        let same = |a: &Rc<dyn RevocationSource>, b: &Rc<dyn RevocationSource>| {
            std::ptr::addr_eq(Rc::as_ptr(a), Rc::as_ptr(b))
        };
        json!({ REVOCATION_HANDLE: handle_of(&self.revocations, source, same, || self.mint()) })
    }

    /// Drop the ports neither the registered settings nor the current ones
    /// name. The TypeScript store holds the port objects themselves, so a
    /// port a change replaced is garbage once nothing else holds it; here
    /// the table would keep every port for the registration's life.
    /// Handles are never reused, so a port set again gets a new one.
    pub fn release_unnamed(&self, settings: &Settings) {
        let keys = settings.named_handles(KEY_HANDLE);
        let trusts = settings.named_handles(TRUST_HANDLE);
        let authorities = settings.named_handles(AUTHORITY_HANDLE);
        let revocations = settings.named_handles(REVOCATION_HANDLE);
        self.keys.borrow_mut().retain(|(n, _)| keys.contains(n));
        self.trusts.borrow_mut().retain(|(n, _)| trusts.contains(n));
        self.authorities.borrow_mut().retain(|(n, _)| authorities.contains(n));
        self.revocations.borrow_mut().retain(|(n, _)| revocations.contains(n));
    }

    /// How many ports the table holds (the retention guard's count).
    #[doc(hidden)]
    pub fn retained(&self) -> usize {
        self.keys.borrow().len()
            + self.trusts.borrow().len()
            + self.authorities.borrow().len()
            + self.revocations.borrow().len()
    }

    fn key_of(&self, value: &Value) -> Option<SigningKey> {
        let n = value.get(KEY_HANDLE).and_then(Value::as_u64)?;
        self.keys.borrow().iter().find(|(m, _)| *m == n).map(|(_, k)| k.clone())
    }

    fn trust_of(&self, value: &Value) -> Option<Rc<dyn TrustPort>> {
        let n = value.get(TRUST_HANDLE).and_then(Value::as_u64)?;
        self.trusts.borrow().iter().find(|(m, _)| *m == n).map(|(_, t)| t.clone())
    }

    /// The JSON a `Partial<SignatureSettings>` change is in the store: only
    /// the members it names.
    pub fn encode(&self, config: &SignatureConfig) -> Value {
        let mut out = Map::new();
        if let Some(key) = &config.signing_key {
            out.insert("signingKey".into(), self.key_json(key));
        }
        if let Some(mode) = &config.mode {
            out.insert("mode".into(), mode.map_or(Value::Null, |m| json!(m.as_str())));
        }
        if let Some(authority) = &config.timestamp_authority {
            out.insert("timestampAuthority".into(), self.authority_json(authority));
        }
        if let Some(source) = &config.revocation {
            out.insert("revocation".into(), self.revocation_json(source));
        }
        if let Some(stamp) = config.document_timestamp {
            out.insert("documentTimestamp".into(), json!(stamp));
        }
        if let Some(trust) = &config.trust {
            out.insert("trust".into(), self.trust_json(trust));
        }
        if let Some(allow) = config.allow_certify {
            out.insert("allowCertify".into(), json!(allow));
        }
        Value::Object(out)
    }

    /// The settings a stored JSON value holds.
    pub fn decode(&self, value: &Value) -> SignatureSettings {
        SignatureSettings {
            signing_key: value.get("signingKey").and_then(|v| self.key_of(v)),
            mode: value.get("mode").and_then(Value::as_str).and_then(SignatureMode::from_name),
            timestamp_authority: value
                .get("timestampAuthority")
                .and_then(|v| port_at(&self.authorities, v, AUTHORITY_HANDLE)),
            revocation: value
                .get("revocation")
                .and_then(|v| port_at(&self.revocations, v, REVOCATION_HANDLE)),
            document_timestamp: value.get("documentTimestamp").is_some_and(crate::js::truthy),
            trust: value.get("trust").and_then(|v| self.trust_of(v)),
            allow_certify: value.get("allowCertify").is_some_and(crate::js::truthy),
        }
    }
}

/// `SIGNATURE_DEFAULTS` in the store.
pub fn defaults_json() -> Value {
    json!({
        "signingKey": null,
        "mode": null,
        "timestampAuthority": null,
        "revocation": null,
        "documentTimestamp": false,
        "trust": null,
        "allowCertify": false,
    })
}

/// The registration's table and its registered settings.
pub fn register(config: &SignatureConfig) -> (Rc<PortTable>, Value) {
    let table = Rc::new(PortTable::default());
    let registered = table.encode(config);
    (table, registered)
}
