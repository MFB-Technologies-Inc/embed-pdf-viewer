use std::sync::Arc;

use epdf_ffi::{AnnotationRef, CancelToken, PageRef, ViewerError, signalled_cancellable};
use serde_json::{Value, json};

use super::{
    AnnotationHandle, AnnotationRecord, object_of, open, page_json, records_out, ref_out, refs_json,
};

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationRefMapping {
    pub from: AnnotationRef,
    pub to: AnnotationRef,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationImportResult {
    pub annotations: Vec<AnnotationRecord>,
    pub ref_map: Vec<AnnotationRefMapping>,
    /// The engine's open dropped-item diagnostics.
    pub dropped_json: String,
}

#[uniffi::export]
impl AnnotationHandle {
    /// A self-contained AnnotationBundle: resource bytes use the existing `$bytes` encoding.
    /// Omitting both filters selects all. Supplied pages and refs contribute their union;
    /// an empty list contributes no members and does not override the other filter.
    pub async fn export(
        &self,
        refs: Option<Vec<AnnotationRef>>,
        pages: Option<Vec<PageRef>>,
        cancel: Arc<CancelToken>,
    ) -> Result<String, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let mut selection = json!({});
                    if let Some(refs) = refs {
                        selection["refs"] = Value::Array(refs_json(&refs)?);
                    }
                    if let Some(pages) = pages {
                        selection["pages"] = pages.into_iter().map(page_json).collect();
                    }
                    signalled_cancellable("annotation", |signal| {
                        Ok(open(c)?.export(&selection, Some(signal)))
                    })
                },
                |bundle| bundle.to_string(),
            )
            .await
    }

    /// Copy an AnnotationBundle using the engine's AnnotationImportOptions vocabulary.
    pub async fn import(
        &self,
        bundle_json: String,
        options_json: String,
        cancel: Arc<CancelToken>,
    ) -> Result<AnnotationImportResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let c = open(c)?.clone();
                    let bundle = Value::Object(object_of(&bundle_json)?);
                    let options = Value::Object(object_of(&options_json)?);
                    signalled_cancellable("annotation", |signal| {
                        Ok(c.import(bundle, options, Some(signal)).convert(move |result| {
                            let annotations =
                                result["annotations"].as_array().ok_or_else(|| {
                                    super::invalid("an import result has no annotations")
                                })?;
                            let pairs = result["refMap"]
                                .as_array()
                                .ok_or_else(|| super::invalid("an import result has no refMap"))?;
                            Ok(AnnotationImportResult {
                                annotations: records_out(&c, annotations)?,
                                ref_map: pairs
                                    .iter()
                                    .map(|p| {
                                        Ok(AnnotationRefMapping {
                                            from: ref_out(&p["from"])?,
                                            to: ref_out(&p["to"])?,
                                        })
                                    })
                                    .collect::<Result<_, epdf_kernel::Error>>()?,
                                dropped_json: result["dropped"].to_string(),
                            })
                        }))
                    })
                },
                |result| result,
            )
            .await
    }
}
