//! Scalar signature settings; signer and trust ports remain registered resources.
use super::*;
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct SignatureSettings {
    pub mode: Option<SignatureMode>,
    pub allow_certify: bool,
    /// A timestamp authority is set.
    pub has_timestamp_authority: bool,
    /// A revocation source is set.
    pub has_revocation: bool,
    pub document_timestamp: bool,
}
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct SignatureSettingsPatch {
    #[uniffi(default = None)]
    pub mode: Option<SignatureMode>,
    #[uniffi(default = false)]
    pub clear_mode: bool,
    #[uniffi(default = None)]
    pub allow_certify: Option<bool>,
    /// A new timestamp authority, through the host's transport.
    #[uniffi(default = None)]
    pub timestamp_authority: Option<SignatureTimestampAuthority>,
    #[uniffi(default = false)]
    pub clear_timestamp_authority: bool,
    /// A new revocation route, through the host's transport.
    #[uniffi(default = None)]
    pub revocation: Option<SignatureRevocationRoute>,
    #[uniffi(default = false)]
    pub clear_revocation: bool,
    #[uniffi(default = None)]
    pub document_timestamp: Option<bool>,
}
#[uniffi::export]
impl SignatureHandle {
    /// Current settings shared by this plugin registration.
    pub async fn settings(&self) -> Result<SignatureSettings, ViewerError> {
        self.cap
            .call(|c| {
                let settings = open(c)?.get_settings();
                Ok(SignatureSettings {
                    mode: settings.mode.map(Into::into),
                    allow_certify: settings.allow_certify,
                    has_timestamp_authority: settings.timestamp_authority.is_some(),
                    has_revocation: settings.revocation.is_some(),
                    document_timestamp: settings.document_timestamp,
                })
            })
            .await
    }
    /// Merge only the named fields, preserving registration resources.
    pub async fn update_settings(&self, patch: SignatureSettingsPatch) -> Result<(), ViewerError> {
        let (slot, post) = (self.transport.clone(), self.post.clone());
        self.cap
            .call(move |c| {
                let http: std::rc::Rc<dyn epdf_signature_core::transport::HttpTransport> =
                    std::rc::Rc::new(SlotTransport { slot, post });
                open(c)?.update_settings(
                    &(crate::SignatureConfig {
                        mode: if patch.clear_mode {
                            Some(None)
                        } else {
                            patch.mode.map(|mode| Some(mode.into()))
                        },
                        allow_certify: patch.allow_certify,
                        timestamp_authority: if patch.clear_timestamp_authority {
                            Some(None)
                        } else {
                            patch.timestamp_authority.map(|a| Some(a.signer(http.clone())))
                        },
                        revocation: if patch.clear_revocation {
                            Some(None)
                        } else {
                            patch.revocation.map(|r| Some(r.source(http)))
                        },
                        document_timestamp: patch.document_timestamp,
                        ..Default::default()
                    }),
                );
                Ok(())
            })
            .await
    }
    /// Restore the values supplied when the plugin was registered.
    pub async fn reset_settings(&self) -> Result<(), ViewerError> {
        self.cap
            .call(|c| {
                open(c)?.reset_settings();
                Ok(())
            })
            .await
    }
}
