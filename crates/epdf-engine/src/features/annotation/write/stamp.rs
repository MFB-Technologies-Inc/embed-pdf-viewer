//! A stamp's drawing: found or added once per artwork, and placed.
//!
//! G11. Ports `services/src/features/annotations/internal/write/
//! stampDrawing.ts` (`drawingFor`), the create half of
//! `writeStampAnnotation.ts` (`preflightStampDraft`, `applyStampDraft`,
//! `authorStampAppearance`) and `document-session/DrawingIndex.ts`.
//!
//! A stamp's appearance is a wrapper of its own that places a DRAWING: a
//! form the document holds once, whatever number of stamps show it. The
//! bytes a caller sends (PNG, JPEG or a one-page PDF) become their
//! canonical drawing in a document of their own, which is hashed; a drawing
//! of the document with that content is placed again, else the canonical
//! one is added. So a hundred placements of one stamp add one drawing, and
//! a PNG and the drawing exported from it are the same drawing.

use std::collections::HashMap;

use pdfium::{Annotation, Document};

use super::prepare::StampFit;
use super::primitives::{
    EMBD_METADATA_SCHEMA_VERSION, set_embed_string, write_annotation_box, written,
};
use crate::error::{EngineError, EngineErrorCode};
use crate::features::binary::{BinaryFormat, sniff};
use crate::geom::PdfRect;

/// An open document's stamp drawings, known by content (`DrawingIndex`).
/// It lives in memory as long as its document; nothing of it is written,
/// and a reopen builds it again. A rollback that removes objects forgets it.
#[derive(Debug, Default)]
pub struct DrawingIndex {
    /// Content id (the SHA-256, in hex, of a drawing's canonical bytes) to
    /// the drawing's object number. `None` until the first lookup builds it
    /// from the drawings the document's stamps place.
    by_content: Option<HashMap<String, u32>>,
    /// SHA-256 (hex) of bytes as a caller gave them to the drawing made from
    /// them, so placing the same bytes again skips making the drawing.
    by_source: HashMap<String, u32>,
}

impl DrawingIndex {
    /// Forget everything: the drawings may be gone (`forget`).
    pub fn forget(&mut self) {
        self.by_content = None;
        self.by_source.clear();
    }
}

fn unknown(message: impl Into<String>) -> EngineError {
    EngineError::with_message(EngineErrorCode::Unknown, message)
}

fn hex(digest: [u8; 32]) -> String {
    digest.iter().map(|b| format!("{b:02x}")).collect()
}

fn sha256_hex(bytes: &[u8]) -> Result<String, EngineError> {
    pdfium::sha256(bytes).map(hex).ok_or_else(|| unknown("EPDF_DigestBuffer returned false"))
}

/// SHA-256 of a drawing's full save: its content id (`savedDigest`).
fn saved_digest(drawing: &Document) -> Result<String, EngineError> {
    let saved = drawing
        .save_to_buffer(pdfium::PdfSaveMode::Full)
        .map_err(|_| unknown("failed to save a stamp drawing"))?;
    sha256_hex(&saved)
}

/// `requireStampContent`: an `appearance` that is PNG, JPEG or a one-page
/// PDF, checked before the first write.
pub fn require_stamp_content(
    document: &Document,
    appearance: Option<&[u8]>,
) -> Result<(), EngineError> {
    let Some(bytes) = appearance else {
        return Err(EngineError::with_message(
            EngineErrorCode::InvalidArg,
            "creating a stamp annotation needs its 'appearance' resource",
        ));
    };
    match sniff(bytes) {
        None => Err(EngineError::with_message(
            EngineErrorCode::InvalidArg,
            "the appearance resource must be PNG, JPEG or one-page PDF bytes",
        )),
        Some(BinaryFormat::Pdf) => match document.open_sibling(bytes) {
            Err(_) => Err(EngineError::with_message(
                EngineErrorCode::MalformedPdf,
                "the stamp's appearance PDF could not be opened",
            )),
            Ok(source) if source.page_count() != 1 => Err(EngineError::with_message(
                EngineErrorCode::InvalidArg,
                format!(
                    "the appearance PDF has {} pages; a stamp takes a one-page PDF",
                    source.page_count()
                ),
            )),
            Ok(_) => Ok(()),
        },
        Some(_) => Ok(()),
    }
}

/// What a picture a field shows is, checked before anything is written
/// (`pictureFormatOf`, 7d5691a5a): a PNG, a JPEG or a one-page PDF (a mark
/// in a signature field, a push button's image). `declared_pdf` reads the
/// bytes as a PDF and nothing else, as `{ pdf }` asks. Bytes that are none
/// of the three are refused with `InvalidArg`, a PDF that doesn't open with
/// `MalformedPdf`, and one of another page count with `InvalidArg`.
/// `resource` names the bytes in each refusal as the caller passed them (a
/// mark's `appearance`, a button's `image`), and `what` names the picture in
/// the last.
pub fn picture_format_of(
    document: &Document,
    bytes: &[u8],
    what: &str,
    declared_pdf: bool,
    resource: &str,
) -> Result<BinaryFormat, EngineError> {
    let format = if declared_pdf { Some(BinaryFormat::Pdf) } else { sniff(bytes) };
    let Some(format) = format else {
        return Err(EngineError::with_message(
            EngineErrorCode::InvalidArg,
            format!("the {resource} resource must be PNG, JPEG or one-page PDF bytes"),
        ));
    };
    if format != BinaryFormat::Pdf {
        return Ok(format);
    }
    let pages = match document.open_sibling(bytes) {
        Ok(source) => source.page_count(),
        Err(_) => {
            return Err(EngineError::with_message(
                EngineErrorCode::MalformedPdf,
                format!("the {resource} PDF could not be opened"),
            ));
        }
    };
    if pages != 1 {
        return Err(EngineError::with_message(
            EngineErrorCode::InvalidArg,
            format!("the {resource} PDF has {pages} pages; {what} takes a one-page PDF"),
        ));
    }
    Ok(format)
}

/// The drawing a stamp places for `bytes`, by object number: one the
/// document already has, when the content is the same, or a new one
/// (`drawingFor`). The format is what the bytes' first bytes say.
pub fn drawing_for(
    document: &Document,
    index: &mut DrawingIndex,
    bytes: &[u8],
) -> Result<u32, EngineError> {
    let format = sniff(bytes).ok_or_else(|| unknown("stamp content preflight invariant broken"))?;
    drawing_for_format(document, index, bytes, format)
}

/// [`drawing_for`] for bytes whose format the caller checked
/// ([`picture_format_of`]): a mark passed as `{ pdf }` is a PDF whatever its
/// first bytes say. A signature mark, a push button's image and a stamp's
/// picture share the document's drawings, so each is stored once however
/// many of them show it (7d5691a5a).
pub fn drawing_for_format(
    document: &Document,
    index: &mut DrawingIndex,
    bytes: &[u8],
    format: BinaryFormat,
) -> Result<u32, EngineError> {
    let source = sha256_hex(bytes)?;
    if let Some(placed) = index.by_source.get(&source) {
        return Ok(*placed);
    }
    let canonical = match format {
        BinaryFormat::Pdf => document.canonical_drawing_of_pdf(bytes).map_err(|_| {
            EngineError::with_message(
                EngineErrorCode::MalformedPdf,
                "the stamp's appearance PDF could not be opened",
            )
        })?,
        BinaryFormat::Png { width, height } | BinaryFormat::Jpeg { width, height } => {
            let png = matches!(format, BinaryFormat::Png { .. });
            document
                .canonical_drawing_of_image(bytes, png, width, height)
                .map_err(|_| unknown("EPDFDoc_CanonicalDrawing returned NULL"))?
                .ok_or_else(|| {
                    EngineError::with_message(
                        EngineErrorCode::InvalidArg,
                        if png {
                            "EPDFImageObj_SetPng rejected the image data"
                        } else {
                            "EPDFImageObj_SetJpeg rejected the image data"
                        },
                    )
                })?
        }
    };
    let content = saved_digest(&canonical)?;
    let by_content = content_index(document, index)?;
    let drawing = match by_content.get(&content) {
        Some(drawing) => *drawing,
        None => {
            let drawing = document
                .import_drawing(&canonical)
                .ok_or_else(|| unknown("EPDFDoc_ImportDrawing returned 0"))?;
            by_content.insert(content, drawing);
            drawing
        }
    };
    index.by_source.insert(source, drawing);
    Ok(drawing)
}

/// Content id to drawing for the drawings the document's annotations show (a
/// stamp's, a mark's in a widget, a push button's icons), built on first
/// use: each exported and hashed once (`contentIndex`).
fn content_index<'i>(
    document: &Document,
    index: &'i mut DrawingIndex,
) -> Result<&'i mut HashMap<String, u32>, EngineError> {
    if index.by_content.is_none() {
        let mut by_content = HashMap::new();
        for drawing in document.drawings() {
            let Some(exported) = document.export_drawing(drawing) else { continue };
            by_content.entry(saved_digest(&exported)?).or_insert(drawing);
        }
        index.by_content = Some(by_content);
    }
    Ok(index.by_content.get_or_insert_with(HashMap::new))
}

/// `/EMBD_Metadata/AppearanceFit`: how the drawing fills the box
/// (`writeStampFit`).
pub fn write_stamp_fit(annotation: &Annotation<'_>, fit: StampFit) -> Result<(), EngineError> {
    if !annotation.has_embed_metadata() {
        written(
            annotation.set_embed_metadata_number("SchemaVersion", EMBD_METADATA_SCHEMA_VERSION),
            "EPDFAnnot_SetEmbedMetadataNumber",
        )?;
    }
    set_embed_string(annotation, "AppearanceFit", fit.as_str())
}

/// Author a stamp's appearance from its drawing, correct under a turn
/// (`authorStampAppearance`).
///
/// The appearance is built in the unturned frame and turned by an `/AP
/// /Matrix` afterwards. Fitting the drawing into the turned `/Rect` instead
/// bakes the letterbox bands into the appearance, and the closing re-fit
/// shrinks the picture by the aspect ratio a second time. So a turned stamp
/// is authored upright into its box with no turn recorded, then the turn is
/// recorded (`/Rect` becomes the upright box around the turned one) and the
/// appearance re-fit once.
pub fn author_stamp_appearance(
    document: &Document,
    index: &mut DrawingIndex,
    annotation: &Annotation<'_>,
    bytes: &[u8],
    fit: StampFit,
    content_box: PdfRect,
    rotation: Option<f64>,
) -> Result<(), EngineError> {
    write_annotation_box(annotation, content_box, None)?;
    // A new wrapper places the drawing: the old appearance is replaced
    // whole, and a drawing other stamps place is never changed.
    let drawing = drawing_for(document, index, bytes)?;
    if !annotation.set_drawing(drawing, fit.native()) {
        return Err(unknown("EPDFAnnot_SetDrawing returned false"));
    }
    if super::primitives::turn_of(rotation).is_none() {
        return Ok(());
    }
    write_annotation_box(annotation, content_box, rotation)?;
    if !annotation.update_appearance_to_rect(fit.native()) {
        return Err(unknown("EPDFAnnot_UpdateAppearanceToRect returned false"));
    }
    Ok(())
}
