//! `services/annotation-rows.ts`: the annotations an action can live on, of
//! both read families: a page's annotations (`doc.annotations`,
//! `doc.annotate.read`) and its widgets, which come with the form
//! (`doc.forms.read`). Each family is read only when the session may read
//! it, so a fill-only session still finds its widgets' actions.

use epdf_kernel::Error;
use epdf_kernel::geometry::PageRef;
use epdf_kernel::rt::Promise;
use epdf_kernel::signal::AbortSignal;
use serde_json::Value;

use super::{all2, annotation_key, get};
use crate::ActionsCapability;

#[cfg(feature = "harness")]
thread_local! {
    static ROW_COPIES: std::cell::Cell<usize> = const { std::cell::Cell::new(0) };
}

/// How many widget rows the reads here have copied out of the form, on this
/// thread.
#[cfg(feature = "harness")]
pub fn widget_row_copies() -> usize {
    ROW_COPIES.with(std::cell::Cell::get)
}

/// A widget row the read keeps, copied out of the form.
fn kept(row: &Value) -> Value {
    #[cfg(feature = "harness")]
    ROW_COPIES.with(|copies| copies.set(copies.get() + 1));
    row.clone()
}

/// The `annotations` list of a `page.annotations.list` answer, moved out.
fn annotations_of(listed: Value) -> Vec<Value> {
    match listed {
        Value::Object(mut listed) => match listed.remove("annotations") {
            Some(Value::Array(annotations)) => annotations,
            _ => Vec::new(),
        },
        _ => Vec::new(),
    }
}

/// The form's widget rows, in place.
fn widgets_of(snapshot: &Value) -> &[Value] {
    get(snapshot, "widgets").and_then(Value::as_array).map_or(&[], Vec::as_slice)
}

fn page_number_of(annotation: &Value) -> Option<f64> {
    get(annotation, "page").and_then(|p| get(p, "objectNumber")).and_then(Value::as_f64)
}

impl ActionsCapability {
    fn list_page_annotations(&self, pon: u32) -> Promise<Value> {
        match self.doc() {
            Ok(doc) => doc.call("page.annotations.list", Some(pon), Vec::new()),
            Err(error) => Promise::rejected(error),
        }
    }

    /// `readPageAnnotations(ctx, form, page, read)`: every annotation of a
    /// page the session may read, its annotations then its widgets.
    pub(crate) async fn read_page_annotations(
        self,
        page: PageRef,
        signal: Option<AbortSignal>,
    ) -> Result<Vec<Value>, Error> {
        let annotations = if self.allows("doc.annotate.read") {
            let list =
                self.read(signal.as_ref(), self.list_page_annotations(page.page_object_number));
            list.map(|listed| Ok(annotations_of(listed)))
        } else {
            Promise::resolved(Vec::new())
        };
        let widgets = if self.allows("doc.forms.read") {
            let pon = f64::from(page.page_object_number);
            self.read_form(signal.as_ref()).map(move |snapshot| {
                Ok(widgets_of(&snapshot)
                    .iter()
                    .filter(|widget| page_number_of(widget) == Some(pon))
                    .map(kept)
                    .collect::<Vec<_>>())
            })
        } else {
            Promise::resolved(Vec::new())
        };
        let (mut annotations, widgets) = all2(annotations, widgets).await?;
        annotations.extend(widgets);
        Ok(annotations)
    }

    /// `readAnnotation(ctx, form, ref, read)`: the annotation `ref` names,
    /// of either family, or `None` when the session can't see it: the
    /// form's widgets first, then the page's annotations.
    pub(crate) async fn read_annotation(
        self,
        reference: Value,
        signal: Option<AbortSignal>,
    ) -> Result<Option<Value>, Error> {
        let key = annotation_key(&reference);
        let matches = |candidate: &Value| {
            annotation_key(get(candidate, "ref").unwrap_or(&Value::Null)) == key
        };
        if self.allows("doc.forms.read") {
            let snapshot = self.read_form(signal.as_ref()).await?;
            if let Some(widget) = widgets_of(&snapshot).iter().find(|w| matches(w)) {
                return Ok(Some(kept(widget)));
            }
        }
        if !self.allows("doc.annotate.read") {
            return Ok(None);
        }
        // `ctx.doc.page(ref.page)`: an unreadable page ref still reaches the
        // engine, which refuses it.
        let pon = get(&reference, "page")
            .and_then(|p| get(p, "objectNumber"))
            .and_then(Value::as_f64)
            .map_or(0, |n| n as u32);
        let listed = self.read(signal.as_ref(), self.list_page_annotations(pon)).await?;
        Ok(annotations_of(listed).into_iter().find(|a| matches(a)))
    }
}
