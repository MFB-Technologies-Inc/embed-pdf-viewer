//! A push button's image: the drawing its widgets name as their icon
//! (`/MK /I`). Ports `services/src/features/forms/internal/buttonImage.ts`
//! (7d5691a5a); `readButtonHasImage` and `PageIndexLookup` are in
//! [`super::snapshot`], where the read uses them.
//!
//! The image is a drawing of the document ([`drawing_for_format`]), the same
//! one a stamp or a signature mark of the same picture places, so the
//! document holds it once however many fields show it.

use pdfium::{Annotation, Document, MkIcon};

use super::snapshot::{FormWidgetRef, PageIndexLookup};
use crate::error::{EngineError, EngineErrorCode};
use crate::features::annotation::write::stamp::{
    DrawingIndex, drawing_for_format, picture_format_of,
};

/// What a push button's image is called in a refusal (`pictureFormatOf`'s
/// `what`).
const WHAT: &str = "a push button's image";

/// The drawing `bytes` (a PNG, a JPEG or a one-page PDF) are in the
/// document, found again when the document holds it already, else added.
/// Bytes that are none of the three are refused with `InvalidArg` naming the
/// `image` resource, and a PDF that doesn't open with `MalformedPdf`.
pub fn image_drawing(
    document: &Document,
    drawings: &mut DrawingIndex,
    bytes: &[u8],
) -> Result<u32, EngineError> {
    let format = picture_format_of(document, bytes, WHAT, false, "image")?;
    drawing_for_format(document, drawings, bytes, format)
}

/// Check `bytes` as a push button's image before anything is written
/// (`pictureFormatOf`), without adding a drawing.
pub fn check_image(document: &Document, bytes: &[u8]) -> Result<(), EngineError> {
    picture_format_of(document, bytes, WHAT, false, "image").map(|_| ())
}

/// A widget opened from its page's `/Annots`, without loading the page
/// (`openAnnotRaw`). `NotFound` for a widget on no page, or one its page
/// doesn't list.
fn open_widget_raw<'d>(
    document: &'d Document,
    pages: &mut PageIndexLookup,
    widget: &FormWidgetRef,
) -> Result<Annotation<'d>, EngineError> {
    let not_found = || {
        EngineError::with_message(
            EngineErrorCode::NotFound,
            format!("no widget {} on its page", widget.annot_object_number),
        )
    };
    if widget.page_object_number == 0 {
        return Err(not_found());
    }
    let page_index = pages.index_of(document, widget.page_object_number).ok_or_else(not_found)?;
    let index = document
        .raw_annotation_index_by_object_number(page_index, widget.annot_object_number)
        .ok_or_else(not_found)?;
    document.raw_annotation(page_index, index).ok_or_else(not_found)
}

/// The drawing a push button's widget shows as its icon (`/MK /I`), by
/// object number; `None` for none (`readWidgetIcon`).
pub fn read_widget_icon(
    document: &Document,
    pages: &mut PageIndexLookup,
    widget: &FormWidgetRef,
) -> Result<Option<u32>, EngineError> {
    Ok(open_widget_raw(document, pages, widget)?.mk_icon(MkIcon::Normal))
}

/// Make a push button's widget show `drawing` as its icon (`/MK /I`) and
/// draw its look again from it, or, with `None`, take the icon out and draw
/// its box again (`setWidgetIcon`). A widget without an icon is left as it
/// is by `None`, and one that names `drawing` already by `drawing`.
pub fn set_widget_icon(
    document: &Document,
    pages: &mut PageIndexLookup,
    widget: &FormWidgetRef,
    drawing: Option<u32>,
) -> Result<(), EngineError> {
    let annotation = open_widget_raw(document, pages, widget)?;
    if annotation.mk_icon(MkIcon::Normal) == drawing {
        return Ok(());
    }
    let written = match drawing {
        None => annotation.clear_mk_icon(MkIcon::Normal),
        Some(drawing) => annotation.set_mk_icon(MkIcon::Normal, drawing),
    };
    if !written {
        return Err(EngineError::with_message(
            EngineErrorCode::Unknown,
            "the push button's icon (/MK /I) could not be written",
        ));
    }
    if !annotation.generate_form_field_appearance() {
        return Err(EngineError::with_message(
            EngineErrorCode::Unknown,
            "the push button's look could not be drawn",
        ));
    }
    Ok(())
}

/// The drawing a push button's image is: the first of its widgets' icons
/// (`imageOf`); `None` when none has one.
pub fn image_of(
    document: &Document,
    pages: &mut PageIndexLookup,
    widgets: &[FormWidgetRef],
) -> Result<Option<u32>, EngineError> {
    for widget in widgets.iter().filter(|widget| widget.page_object_number != 0) {
        if let Some(drawing) = read_widget_icon(document, pages, widget)? {
            return Ok(Some(drawing));
        }
    }
    Ok(None)
}
