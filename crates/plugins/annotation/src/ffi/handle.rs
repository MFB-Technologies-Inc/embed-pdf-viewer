use std::sync::Arc;

use epdf_ffi::{
    AnnotationRef, CancelToken, KernelBound, KernelPost, PageRef, PluginErrorInfo, Point,
    ResourceStatus, Subscription, ViewerError,
};
use epdf_kernel::Unsubscribe;
use serde_json::{Value, json};

use super::{
    AnnotationBatchFailure, AnnotationChangedEvent, AnnotationDeletedEvent, AnnotationEvent,
    AnnotationEventListener, AnnotationFilter, AnnotationRecord, AnnotationResyncedPages,
    AnnotationThreadChange, draft_out, geometry_point, invalid, object_of, offset_json, open,
    page_json, page_out, plugin_error_info_of, record_out, records_out, ref_json, ref_out,
    refs_json, refs_out,
};
use crate::AnnotationCapability;
use crate::write::crud::{CreateOptions, DuplicateOptions};

/// How `duplicate` copies (`AnnotationDuplicateOptions`).
#[derive(Clone, Debug, Default, PartialEq, uniffi::Record)]
pub struct AnnotationDuplicateOptions {
    /// The page the copies go on; `None`: each on its original's page.
    #[uniffi(default = None)]
    pub page: Option<PageRef>,
    /// How far each copy moves, in page points: `x` to the right, `y` down.
    #[uniffi(default = None)]
    pub offset: Option<Point>,
    /// Select the copies as they show.
    #[uniffi(default = false)]
    pub select: bool,
}

/// What a duplicate made (`AnnotationDuplicateResult`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationDuplicateResult {
    /// The copies, in the order their originals were named.
    pub annotations: Vec<AnnotationRecord>,
    /// What was left out, and why, as the engine's JSON.
    pub dropped_json: String,
}

/// Why `stage_remeasure` left a measurement as it was.
#[derive(Clone, Copy, Debug, PartialEq, Eq, uniffi::Enum)]
pub enum AnnotationRemeasureSkipReason {
    /// The session may not change it.
    NoAuthority,
    /// It is locked.
    Locked,
    /// Its scale is not a rectilinear one.
    ForeignMeasure,
    /// It has no readout at the new scale.
    Unavailable,
}

#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationRemeasureSkip {
    pub annotation_ref: AnnotationRef,
    pub reason: AnnotationRemeasureSkipReason,
}

/// What `stage_remeasure` staged (`RecalibrationReport`).
#[derive(Clone, Debug, PartialEq, uniffi::Record)]
pub struct AnnotationRemeasureReport {
    pub page: PageRef,
    /// The measurements whose update it staged.
    pub updated: Vec<AnnotationRef>,
    pub skipped: Vec<AnnotationRemeasureSkip>,
    pub failed: Vec<AnnotationBatchFailure>,
    /// Set, with nothing staged, when the annotations aren't loaded: the
    /// engine's serialized error.
    pub error: Option<PluginErrorInfo>,
}

fn duplicate_out(
    cap: &AnnotationCapability,
    result: &Value,
) -> Result<AnnotationDuplicateResult, epdf_kernel::Error> {
    let annotations = result
        .get("annotations")
        .and_then(Value::as_array)
        .ok_or_else(|| invalid("a duplicate result has no annotations"))?;
    Ok(AnnotationDuplicateResult {
        annotations: records_out(cap, annotations)?,
        dropped_json: result.get("dropped").cloned().unwrap_or_else(|| json!([])).to_string(),
    })
}

/// Duplicate options as the plugin reads them, the call's signal aside.
fn duplicate_options(
    options: &AnnotationDuplicateOptions,
) -> Result<DuplicateOptions, epdf_kernel::Error> {
    Ok(DuplicateOptions {
        page: options.page.map(page_json),
        offset: options.offset.map(offset_json).transpose()?,
        select: options.select,
        signal: None,
    })
}

pub(super) fn duplicate_answer(
    cap: AnnotationCapability,
    start: impl FnOnce(
        &AnnotationCapability,
        epdf_kernel::signal::AbortSignal,
    ) -> epdf_kernel::Promise<Value>,
) -> Result<epdf_kernel::Promise<AnnotationDuplicateResult>, epdf_kernel::Error> {
    epdf_ffi::signalled_cancellable("annotation", |signal| {
        Ok(start(&cap, signal).convert(move |result| duplicate_out(&cap, result)))
    })
}

fn remeasure_out(report: &Value) -> Result<AnnotationRemeasureReport, epdf_kernel::Error> {
    let list = |key: &str| report.get(key).and_then(Value::as_array).cloned().unwrap_or_default();
    let skipped = list("skipped")
        .iter()
        .map(|skip| {
            let reason = match skip.get("reason").and_then(Value::as_str) {
                Some("no-authority") => AnnotationRemeasureSkipReason::NoAuthority,
                Some("locked") => AnnotationRemeasureSkipReason::Locked,
                Some("foreign-measure") => AnnotationRemeasureSkipReason::ForeignMeasure,
                Some("unavailable") => AnnotationRemeasureSkipReason::Unavailable,
                other => return Err(invalid(format!("an unknown remeasure skip: {other:?}"))),
            };
            Ok(AnnotationRemeasureSkip {
                annotation_ref: ref_out(skip.get("ref").unwrap_or(&Value::Null))?,
                reason,
            })
        })
        .collect::<Result<_, epdf_kernel::Error>>()?;
    let failed = list("failed")
        .iter()
        .map(|failure| {
            Ok(AnnotationBatchFailure {
                annotation_ref: ref_out(failure.get("ref").unwrap_or(&Value::Null))?,
                error: plugin_error_info_of(failure.get("error").unwrap_or(&Value::Null)),
            })
        })
        .collect::<Result<_, epdf_kernel::Error>>()?;
    Ok(AnnotationRemeasureReport {
        page: page_out(report.get("page").unwrap_or(&Value::Null))?,
        updated: refs_out(report.get("updated").unwrap_or(&Value::Null))?,
        skipped,
        failed,
        error: report.get("error").filter(|e| !e.is_null()).map(plugin_error_info_of),
    })
}

#[derive(uniffi::Object)]
pub struct AnnotationHandle {
    pub(crate) cap: KernelBound<AnnotationCapability>,
    pub(crate) post: KernelPost,
}

impl AnnotationHandle {
    /// On the kernel thread.
    pub fn new(cap: AnnotationCapability, post: KernelPost) -> Arc<Self> {
        Arc::new(AnnotationHandle { cap: KernelBound::new(cap, post.clone()), post })
    }
}

#[uniffi::export]
impl AnnotationHandle {
    /// The open annotation settings data, including per-tool engine defaults.
    pub async fn settings(&self) -> Result<String, ViewerError> {
        self.cap.call(|cap| Ok(open(cap)?.get_settings().to_string())).await
    }

    /// Merge a settings object using the kernel's shared settings store.
    pub async fn update_settings(&self, changes_json: String) -> Result<(), ViewerError> {
        self.cap
            .call(move |cap| {
                let cap = open(cap)?;
                let changes = Value::Object(object_of(&changes_json)?);
                cap.update_settings(&changes);
                Ok(())
            })
            .await
    }

    pub async fn reset_settings(&self) -> Result<(), ViewerError> {
        self.cap
            .call(|cap| {
                open(cap)?.reset_settings();
                Ok(())
            })
            .await
    }

    /// Install the host's file picker, or `None` to remove it. Without one,
    /// a tool that prompts for a file declines the click.
    pub async fn set_file_picker(
        &self,
        picker: Option<Arc<dyn super::AnnotationFilePicker>>,
    ) -> Result<(), ViewerError> {
        let post = self.post.clone();
        self.cap
            .call(move |c| {
                let provider = picker.map(|p| super::picker::provider(p, post));
                // The remover is dropped: the slot is last-write-wins, and
                // `None` clears it.
                let _ = open(c)?.set_file_picker_provider(provider);
                Ok(())
            })
            .await
    }

    pub fn drafts(&self) -> Arc<super::AnnotationDraftsHandle> {
        Arc::new(super::AnnotationDraftsHandle { cap: self.cap.clone() })
    }

    pub fn threads(&self) -> Arc<super::AnnotationThreadsHandle> {
        Arc::new(super::AnnotationThreadsHandle { cap: self.cap.clone() })
    }

    pub fn selection(&self) -> Arc<super::AnnotationSelectionHandle> {
        Arc::new(super::AnnotationSelectionHandle { cap: self.cap.clone() })
    }

    pub fn tools(&self) -> Arc<super::AnnotationToolsHandle> {
        Arc::new(super::AnnotationToolsHandle { cap: self.cap.clone() })
    }

    pub async fn get(
        &self,
        annotation_ref: AnnotationRef,
    ) -> Result<Option<AnnotationRecord>, ViewerError> {
        self.cap
            .call(move |c| {
                let c = open(c)?;
                c.get(&ref_json(&annotation_ref)?).map(|dto| record_out(c, &dto)).transpose()
            })
            .await
    }

    pub async fn list(
        &self,
        filter: Option<AnnotationFilter>,
    ) -> Result<Vec<AnnotationRecord>, ViewerError> {
        self.cap
            .call(move |c| {
                let c = open(c)?;
                let filter = filter.map(crate::read::annotations::AnnotationFilter::from);
                records_out(c, &c.list(filter.as_ref()))
            })
            .await
    }

    pub async fn selected(&self) -> Result<Vec<AnnotationRecord>, ViewerError> {
        self.cap
            .call(|c| {
                let c = open(c)?;
                records_out(c, &c.list_selected())
            })
            .await
    }

    /// `getAt(page, point)`: the topmost annotation under a page-space point.
    pub async fn hit_test_at(
        &self,
        page: PageRef,
        point: Point,
    ) -> Result<Option<AnnotationRecord>, ViewerError> {
        self.cap
            .call(move |c| {
                let c = open(c)?;
                c.annotation_at(&page_json(page), geometry_point(point))
                    .map(|dto| record_out(c, &dto))
                    .transpose()
            })
            .await
    }

    /// Whether a change of this annotation is still on its way to the engine.
    pub async fn is_pending(&self, annotation_ref: AnnotationRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.is_pending(&ref_json(&annotation_ref)?))).await
    }

    /// `getStatus()`: the records mirror's status, as metadata, form and
    /// signature report it.
    pub async fn status(&self) -> Result<ResourceStatus, ViewerError> {
        self.cap
            .call(|c| {
                Ok(match open(c)?.records.get_status().as_str() {
                    "ready" => ResourceStatus::Ready,
                    "forbidden" => ResourceStatus::Forbidden,
                    "error" => ResourceStatus::Error,
                    "idle" => ResourceStatus::Idle,
                    _ => ResourceStatus::Loading,
                })
            })
            .await
    }

    /// Re-read every annotation from the engine and wait for it.
    pub async fn refresh(&self, cancel: Arc<CancelToken>) -> Result<(), ViewerError> {
        self.cap.call_promise_cancellable(cancel, |c| Ok(open(c)?.records.refresh()), |()| ()).await
    }

    /// `whenSynced()`: every load and page re-read in flight has settled.
    pub async fn when_synced(&self, cancel: Arc<CancelToken>) -> Result<(), ViewerError> {
        self.cap.call_promise_cancellable(cancel, |c| Ok(open(c)?.records.settled()), |()| ()).await
    }

    /// `create(page, fields, resources, { tool, select })`: resolves with the
    /// created annotation's ref once the engine confirmed it. `fields_json`
    /// is the engine's field bag for the subtype (`{"subtype": "square",
    /// "box": …}`).
    pub async fn create(
        &self,
        page: PageRef,
        fields_json: String,
        tool: Option<String>,
        select: Option<bool>,
        cancel: Arc<CancelToken>,
    ) -> Result<AnnotationRef, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let fields = object_of(&fields_json)?;
                    let options =
                        CreateOptions { signal: None, tool, select: select.unwrap_or(false) };
                    Ok(open(c)?.create(&page_json(page), &fields, None, options))
                },
                |created: Value| ref_out(&created["annotation"]["ref"]).map_err(ViewerError::from),
            )
            .await?
    }

    /// `update(ref, patch)`: `patch_json` is the engine's field bag.
    pub async fn update(
        &self,
        annotation_ref: AnnotationRef,
        patch_json: String,
        cancel: Arc<CancelToken>,
    ) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let patch = object_of(&patch_json)?;
                    Ok(open(c)?.update(&ref_json(&annotation_ref)?, &patch, None, None))
                },
                |_: Value| (),
            )
            .await
    }

    pub async fn delete(
        &self,
        annotation_ref: AnnotationRef,
        cancel: Arc<CancelToken>,
    ) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| Ok(open(c)?.delete(&ref_json(&annotation_ref)?, None)),
                |()| (),
            )
            .await
    }

    /// `reorder(refs, position)`: `position_json` is `"front"`, `"back"`,
    /// `"forward"`, `"backward"` or `{ "above": ref }` / `{ "below": ref }`.
    pub async fn reorder(
        &self,
        refs: Vec<AnnotationRef>,
        position_json: String,
        cancel: Arc<CancelToken>,
    ) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let position: Value = serde_json::from_str(&position_json)
                        .map_err(|e| super::invalid(format!("a position is not JSON: {e}")))?;
                    Ok(open(c)?.reorder(&refs_json(&refs)?, &position, None))
                },
                |()| (),
            )
            .await
    }

    pub async fn can_read(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_read())).await
    }

    pub async fn can_create(&self) -> Result<bool, ViewerError> {
        self.cap.call(|c| Ok(open(c)?.can_create())).await
    }

    pub async fn can_update(&self, annotation_ref: AnnotationRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.can_update(&ref_json(&annotation_ref)?))).await
    }

    pub async fn can_delete(&self, annotation_ref: AnnotationRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.can_delete(&ref_json(&annotation_ref)?))).await
    }

    /// `duplicate(refs, options)`: copy annotations as one change, one step
    /// to undo. A copy carries its original's fields, drawing, file and link,
    /// under its own ref and name. Rejects `permission-denied` without
    /// `annotations:create`, `not-found` for an annotation or page the
    /// document doesn't have, `invalid-input` for another plugin's record.
    pub async fn duplicate(
        &self,
        refs: Vec<AnnotationRef>,
        options: AnnotationDuplicateOptions,
        cancel: Arc<CancelToken>,
    ) -> Result<AnnotationDuplicateResult, ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                move |c| {
                    let refs = refs_json(&refs)?;
                    let options = duplicate_options(&options)?;
                    duplicate_answer(open(c)?.clone(), move |c, signal| {
                        c.duplicate(
                            refs,
                            DuplicateOptions { signal: Some(signal), ..options },
                            Vec::new(),
                        )
                    })
                },
                |result| result,
            )
            .await
    }

    /// `canDuplicate(refs)`: the session may create annotations, and each is
    /// here, unlocked and copyable. `false` for an empty list.
    pub async fn can_duplicate(&self, refs: Vec<AnnotationRef>) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.can_duplicate(&refs_json(&refs)?))).await
    }

    /// `isMovable(ref)`: whether people move it; `false` for one that sits on
    /// text, and for one the document doesn't have.
    pub async fn is_movable(&self, annotation_ref: AnnotationRef) -> Result<bool, ViewerError> {
        self.cap.call(move |c| Ok(open(c)?.is_movable(&ref_json(&annotation_ref)?))).await
    }

    /// `whenRemeasurable()`: resolves once the annotations loaded (a failed
    /// load retried) and every pending load settled.
    pub async fn when_remeasurable(&self, cancel: Arc<CancelToken>) -> Result<(), ViewerError> {
        self.cap
            .call_promise_cancellable(
                cancel,
                |c| {
                    epdf_ffi::signalled_cancellable("annotation", |_| {
                        Ok(open(c)?.when_remeasurable())
                    })
                },
                |_| (),
            )
            .await
    }

    /// `stageRemeasure(page, scale)`: update every measurement on `page` to
    /// `scale_json` (the engine's `PdfMeasure`), at once. Before the
    /// annotations load it stages nothing and sets `error`; wait on
    /// `when_remeasurable` first. A scale that isn't an object is `invalid-input`.
    pub async fn stage_remeasure(
        &self,
        page: PageRef,
        scale_json: String,
    ) -> Result<AnnotationRemeasureReport, ViewerError> {
        self.cap
            .call(move |c| {
                let scale = Value::Object(object_of(&scale_json)?);
                remeasure_out(&open(c)?.stage_remeasure(&page_json(page), scale)?)
            })
            .await
    }

    /// Every hook in one registration. An event that does not decode is
    /// reported through the kernel's report sink, not delivered.
    pub fn subscribe(&self, listener: Arc<dyn AnnotationEventListener>) -> Arc<Subscription> {
        let (subscription, slot) = Subscription::pending(self.post.clone());
        self.cap.fire(move |c| {
            let ending = listener.clone();
            let cap = c.clone();
            slot.fill_until_end(
                c.lifetime(),
                || {
                    let events = &cap.events;
                    let l = listener.clone();
                    let resynced = events.resynced.on(move |e| {
                        let pages = match e.get("pages") {
                            Some(Value::Array(pages)) => AnnotationResyncedPages::Pages {
                                pages: pages.iter().map(page_out).collect::<Result<_, _>>()?,
                            },
                            _ => AnnotationResyncedPages::All,
                        };
                        l.on_event(AnnotationEvent::Resynced { pages });
                        Ok(())
                    });
                    let (l, k) = (listener.clone(), cap.clone());
                    let created = events.created.on(move |e| {
                        let annotation =
                            record_out(&k, e.get("annotation").unwrap_or(&Value::Null))?;
                        l.on_event(AnnotationEvent::Created {
                            event: AnnotationChangedEvent { annotation },
                        });
                        Ok(())
                    });
                    let (l, k) = (listener.clone(), cap.clone());
                    let updated = events.updated.on(move |e| {
                        let annotation =
                            record_out(&k, e.get("annotation").unwrap_or(&Value::Null))?;
                        l.on_event(AnnotationEvent::Updated {
                            event: AnnotationChangedEvent { annotation },
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let deleted = events.deleted.on(move |e| {
                        l.on_event(AnnotationEvent::Deleted {
                            event: AnnotationDeletedEvent {
                                refs: refs_out(e.get("refs").unwrap_or(&Value::Null))?,
                                page: page_out(e.get("page").unwrap_or(&Value::Null))?,
                            },
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let reordered = events.reordered.on(move |e| {
                        l.on_event(AnnotationEvent::Reordered {
                            page: page_out(e.get("page").unwrap_or(&Value::Null))?,
                            order: refs_out(e.get("order").unwrap_or(&Value::Null))?,
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let selection = events.selection_changed.on(move |e| {
                        l.on_event(AnnotationEvent::SelectionChanged {
                            refs: refs_out(e.get("refs").unwrap_or(&Value::Null))?,
                            previous_refs: refs_out(e.get("previousRefs").unwrap_or(&Value::Null))?,
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let draft = events.draft_changed.on(move |e| {
                        l.on_event(AnnotationEvent::DraftChanged {
                            draft: draft_out(e.get("draft").unwrap_or(&Value::Null))?,
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let editing = events.editing_changed.on(move |e| {
                        let annotation_ref = match e.get("ref") {
                            None | Some(Value::Null) => None,
                            Some(r) => Some(ref_out(r)?),
                        };
                        l.on_event(AnnotationEvent::EditingChanged { annotation_ref });
                        Ok(())
                    });
                    let l = listener.clone();
                    let hover = events.hover_changed.on(move |e| {
                        let annotation_ref = match e.get("ref") {
                            None | Some(Value::Null) => None,
                            Some(r) => Some(ref_out(r)?),
                        };
                        l.on_event(AnnotationEvent::HoverChanged { annotation_ref });
                        Ok(())
                    });
                    let l = listener.clone();
                    let thread = events.thread_changed.on(move |e| {
                        let change = match e.get("type").and_then(Value::as_str) {
                            Some("reply") => AnnotationThreadChange::Reply,
                            Some("text") => AnnotationThreadChange::Text,
                            Some("status") => AnnotationThreadChange::Status,
                            Some("marked") => AnnotationThreadChange::Marked,
                            _ => AnnotationThreadChange::Deleted,
                        };
                        l.on_event(AnnotationEvent::ThreadChanged {
                            root_ref: ref_out(e.get("rootRef").unwrap_or(&Value::Null))?,
                            change,
                        });
                        Ok(())
                    });
                    let l = listener.clone();
                    let failed = events.write_failed.on(move |e| {
                        l.on_event(AnnotationEvent::WriteFailed {
                            refs: refs_out(e.get("refs").unwrap_or(&Value::Null))?,
                            error: plugin_error_info_of(e.get("error").unwrap_or(&Value::Null)),
                        });
                        Ok(())
                    });
                    let offs = [
                        resynced, created, updated, deleted, reordered, selection, draft, editing,
                        hover, thread, failed,
                    ];
                    Unsubscribe::new(move || offs.iter().for_each(Unsubscribe::call))
                },
                move || ending.on_end(),
            );
        });
        subscription
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn reference(n: u32) -> Value {
        json!({ "kind": "objectNumber", "objectNumber": n,
            "page": { "kind": "objectNumber", "objectNumber": 1 } })
    }

    fn annotation(n: u32) -> AnnotationRef {
        AnnotationRef::ObjectNumber {
            page: PageRef { page_object_number: 1 },
            annot_object_number: n,
        }
    }

    // The plugin never fills `failed` today; the mapping still has to hold
    // for a report that does.
    #[test]
    fn a_report_maps_every_member() {
        let report = remeasure_out(&json!({
            "page": { "kind": "objectNumber", "objectNumber": 1 },
            "scale": {},
            "updated": [reference(4)],
            "skipped": [
                { "ref": reference(5), "reason": "no-authority" },
                { "ref": reference(6), "reason": "locked" },
                { "ref": reference(7), "reason": "foreign-measure" },
                { "ref": reference(8), "reason": "unavailable" },
            ],
            "failed": [{ "ref": reference(9),
                "error": { "code": "operation-failed", "message": "refused" } }],
            "error": { "name": "Error", "code": "Unknown", "message": "not loaded" },
        }))
        .unwrap();
        assert_eq!(report.page, PageRef { page_object_number: 1 });
        assert_eq!(report.updated, vec![annotation(4)]);
        let reasons: Vec<_> = report.skipped.iter().map(|s| s.reason).collect();
        assert_eq!(
            reasons,
            vec![
                AnnotationRemeasureSkipReason::NoAuthority,
                AnnotationRemeasureSkipReason::Locked,
                AnnotationRemeasureSkipReason::ForeignMeasure,
                AnnotationRemeasureSkipReason::Unavailable,
            ]
        );
        assert_eq!(report.failed.len(), 1);
        assert_eq!(report.failed[0].annotation_ref, annotation(9));
        assert_eq!(report.failed[0].error.code, "operation-failed");
        assert_eq!(report.failed[0].error.message, "refused");
        assert_eq!(report.failed[0].error.capability, "annotation");
        let error = report.error.expect("the report's error");
        assert_eq!((error.code.as_str(), error.message.as_str()), ("Unknown", "not loaded"));
    }

    #[test]
    fn an_unknown_skip_reason_is_refused() {
        let refused = remeasure_out(&json!({
            "page": { "kind": "objectNumber", "objectNumber": 1 },
            "updated": [], "failed": [],
            "skipped": [{ "ref": reference(5), "reason": "sideways" }],
        }));
        assert!(refused.is_err());
    }
}
