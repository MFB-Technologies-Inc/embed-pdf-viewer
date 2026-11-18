//! `core/main/src/commands.ts` (`@embedpdf/core/commands`, `d6325f5ba`,
//! `2e65f29e8`): the commands of the documents capability, download and
//! print. Download hands the user a file, which is the host's part, so the
//! list is made with it. Print goes through the kernel, which has the
//! host's printer. The i18n plugin has their labels.
//!
//! The TypeScript ships them from `@embedpdf/core`; the kernel crate has no
//! command types, so they live here (CU-C5-D1).

use std::rc::Rc;

use epdf_kernel::print::PrintOptions;
use epdf_kernel::rt::Promise;
use epdf_kernel::{DOCUMENTS, undefined};
use serde_json::Value;

use crate::contract::{CommandDef, Shortcut};

/// `DocumentCommandsPlatform`: what the document's commands need from the
/// host.
pub trait DocumentCommandsPlatform {
    /// Give the user the bytes, as the kernel's download hands them, as a
    /// file of the given content type.
    fn save_file(&self, bytes: Value, file_name: String, content_type: String);
}

/// `createDocumentCommands(platform)`: `document:download` and
/// `document:print`, with the host's part from `platform`.
pub fn create_document_commands(platform: Rc<dyn DocumentCommandsPlatform>) -> Vec<CommandDef> {
    vec![
        CommandDef {
            id: "document:download".into(),
            label_key: Some("commands.document.download".into()),
            label: Some("Download".into()),
            icon: Some("download".into()),
            shortcut: Some(Shortcut::One("Mod+S".into())),
            categories: Some(vec!["document".into()]),
            allowed: Some(Rc::new(|context| {
                Ok(context.try_get(&DOCUMENTS).is_none_or(|documents| documents.can_download(None)))
            })),
            not_allowed_key: Some("commands.notAllowed.doc-download".into()),
            // An `async` run: what it throws rejects the promise it returns.
            run: Some(Rc::new(move |context| {
                let (context, platform) = (context.clone(), platform.clone());
                Ok(Some(Promise::spawn(async move {
                    let documents = context.get(&DOCUMENTS)?;
                    let id = context.document_id().map(str::to_string);
                    let bytes = documents
                        .save_with_signal(id.as_deref(), None, context.signal().cloned())
                        .await?;
                    let name = documents.get_document(id.as_deref()).and_then(|doc| doc.name);
                    platform.save_file(
                        bytes,
                        file_name_of(name.as_deref()),
                        "application/pdf".into(),
                    );
                    Ok(undefined())
                })))
            })),
            enabled: Some(Rc::new(|context| Ok(context.document_id().is_some()))),
            ..CommandDef::default()
        },
        CommandDef {
            id: "document:print".into(),
            label_key: Some("commands.document.print".into()),
            label: Some("Print".into()),
            icon: Some("print".into()),
            shortcut: Some(Shortcut::One("Mod+P".into())),
            categories: Some(vec!["document".into()]),
            allowed: Some(Rc::new(|context| {
                Ok(context.try_get(&DOCUMENTS).is_none_or(|documents| documents.can_print(None)))
            })),
            not_allowed_key: Some("commands.notAllowed.doc-print".into()),
            // The kernel prints the document's pages, and runs the document's own
            // print actions around the dialog when the actions plugin is there.
            run: Some(Rc::new(|context| {
                let context = context.clone();
                Ok(Some(Promise::spawn(async move {
                    let documents = context.get(&DOCUMENTS)?;
                    let options = PrintOptions {
                        signal: context.signal().cloned(),
                        ..PrintOptions::default()
                    };
                    documents.print(context.document_id(), options).await?;
                    Ok(undefined())
                })))
            })),
            enabled: Some(Rc::new(|context| Ok(context.document_id().is_some()))),
            ..CommandDef::default()
        },
    ]
}

/// The download's file name: the document's name, ending in `.pdf`.
fn file_name_of(name: Option<&str>) -> String {
    match name {
        None | Some("") => "document.pdf".into(),
        Some(name) if has_pdf_extension(name) => name.into(),
        Some(name) => format!("{name}.pdf"),
    }
}

/// `/\.pdf$/i`.
fn has_pdf_extension(name: &str) -> bool {
    name.len()
        .checked_sub(4)
        .is_some_and(|at| name.is_char_boundary(at) && name[at..].eq_ignore_ascii_case(".pdf"))
}

#[cfg(test)]
mod tests {
    use super::file_name_of;

    /// `fileNameOf`, as `core/main/test/commands.test.ts` saves `contract.pdf`.
    #[test]
    fn the_download_is_named_after_the_document_and_ends_in_pdf() {
        assert_eq!(file_name_of(Some("contract.pdf")), "contract.pdf");
        assert_eq!(file_name_of(Some("contract.PDF")), "contract.PDF");
        assert_eq!(file_name_of(Some("contract")), "contract.pdf");
        assert_eq!(file_name_of(Some("")), "document.pdf");
        assert_eq!(file_name_of(None), "document.pdf");
    }
}
