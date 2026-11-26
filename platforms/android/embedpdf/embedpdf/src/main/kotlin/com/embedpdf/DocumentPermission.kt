package com.embedpdf

/**
 * A permission [PdfViewer.allows] asks the document's scope about. [value] is
 * the wire string.
 *
 * The set is open, so this is a value class rather than an enum: the constants
 * are the engine's document capabilities (`doc.*`) and `annotations:create`, a
 * plugin's own permission is `DocumentPermission("protected-display.view")`,
 * and a newer engine can add permissions without breaking a caller's `when`.
 */
@JvmInline
public value class DocumentPermission(public val value: String) {
    /** The wire string. */
    override fun toString(): String = value

    public companion object {
        public val OPEN: DocumentPermission = DocumentPermission("doc.open")
        public val RENDER: DocumentPermission = DocumentPermission("doc.render")

        /** The thumbnail pictures only; [RENDER] implies it. */
        public val RENDER_THUMBNAIL: DocumentPermission = DocumentPermission("doc.render.thumbnail")
        public val TEXT_SELECT: DocumentPermission = DocumentPermission("doc.text.select")
        public val TEXT_COPY: DocumentPermission = DocumentPermission("doc.text.copy")
        public val TEXT_SEARCH: DocumentPermission = DocumentPermission("doc.text.search")
        public val CONTENT_COPY: DocumentPermission = DocumentPermission("doc.content.copy")
        public val DOWNLOAD: DocumentPermission = DocumentPermission("doc.download")
        public val DOWNLOAD_FLATTENED: DocumentPermission = DocumentPermission("doc.download.flattened")
        public val PRINT: DocumentPermission = DocumentPermission("doc.print")
        public val PRINT_HIGH: DocumentPermission = DocumentPermission("doc.print.high")
        public val PAGES_MODIFY: DocumentPermission = DocumentPermission("doc.pages.modify")
        public val PAGES_ASSEMBLE: DocumentPermission = DocumentPermission("doc.pages.assemble")
        public val FORMS_READ: DocumentPermission = DocumentPermission("doc.forms.read")
        public val FORMS_FILL: DocumentPermission = DocumentPermission("doc.forms.fill")
        public val FORMS_MODIFY: DocumentPermission = DocumentPermission("doc.forms.modify")
        public val FORMS_IMPORT: DocumentPermission = DocumentPermission("doc.forms.import")
        public val FORMS_SCRIPT: DocumentPermission = DocumentPermission("doc.forms.script")
        public val FORMS_SUBMIT: DocumentPermission = DocumentPermission("doc.forms.submit")
        public val ANNOTATE_READ: DocumentPermission = DocumentPermission("doc.annotate.read")
        public val ANNOTATE_MODIFY: DocumentPermission = DocumentPermission("doc.annotate.modify")
        public val ANNOTATE_IMPORT: DocumentPermission = DocumentPermission("doc.annotate.import")
        public val METADATA_MODIFY: DocumentPermission = DocumentPermission("doc.metadata.modify")
        public val ATTACHMENTS_MODIFY: DocumentPermission = DocumentPermission("doc.attachments.modify")
        public val OUTLINE_MODIFY: DocumentPermission = DocumentPermission("doc.outline.modify")
        public val REDACT: DocumentPermission = DocumentPermission("doc.redact")
        public val SIGN: DocumentPermission = DocumentPermission("doc.sign")
        public val SIGN_CERTIFY: DocumentPermission = DocumentPermission("doc.sign.certify")

        /** `annotations:create`: whether this caller may create annotations of their own. */
        public val ANNOTATIONS_CREATE: DocumentPermission = DocumentPermission("annotations:create")

        /** Every constant this type names, the `doc.*` ones in the engine's order. */
        public val named: List<DocumentPermission> = listOf(
            OPEN, RENDER, RENDER_THUMBNAIL, TEXT_SELECT, TEXT_COPY, TEXT_SEARCH, CONTENT_COPY, DOWNLOAD,
            DOWNLOAD_FLATTENED, PRINT, PRINT_HIGH, PAGES_MODIFY, PAGES_ASSEMBLE, FORMS_READ, FORMS_FILL,
            FORMS_MODIFY, FORMS_IMPORT, FORMS_SCRIPT, FORMS_SUBMIT, ANNOTATE_READ, ANNOTATE_MODIFY,
            ANNOTATE_IMPORT, METADATA_MODIFY, ATTACHMENTS_MODIFY, OUTLINE_MODIFY, REDACT, SIGN,
            SIGN_CERTIFY, ANNOTATIONS_CREATE,
        )
    }
}
