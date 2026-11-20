package com.embedpdf

import kotlinx.serialization.Serializable

/**
 * An annotation's subtype, the PDF's own `/Subtype` as the engine spells it.
 * [value] is the wire string.
 *
 * The set is open, so this is a value class rather than an enum: the constants
 * are the engine's subtype catalog, but the annotation plugin takes any kind
 * name, and a newer engine can add subtypes without breaking a caller's
 * `when`. Any other subtype is `AnnotationSubtype("sound")`.
 */
@Serializable
@JvmInline
public value class AnnotationSubtype(public val value: String) {
    /** The wire string. */
    override fun toString(): String = value

    public companion object {
        public val TEXT: AnnotationSubtype = AnnotationSubtype("text")
        public val LINK: AnnotationSubtype = AnnotationSubtype("link")
        public val FREE_TEXT: AnnotationSubtype = AnnotationSubtype("free-text")
        public val LINE: AnnotationSubtype = AnnotationSubtype("line")
        public val SQUARE: AnnotationSubtype = AnnotationSubtype("square")
        public val CIRCLE: AnnotationSubtype = AnnotationSubtype("circle")
        public val POLYGON: AnnotationSubtype = AnnotationSubtype("polygon")
        public val POLYLINE: AnnotationSubtype = AnnotationSubtype("polyline")
        public val HIGHLIGHT: AnnotationSubtype = AnnotationSubtype("highlight")
        public val UNDERLINE: AnnotationSubtype = AnnotationSubtype("underline")
        public val SQUIGGLY: AnnotationSubtype = AnnotationSubtype("squiggly")
        public val STRIKEOUT: AnnotationSubtype = AnnotationSubtype("strikeout")
        public val STAMP: AnnotationSubtype = AnnotationSubtype("stamp")
        public val CARET: AnnotationSubtype = AnnotationSubtype("caret")
        public val INK: AnnotationSubtype = AnnotationSubtype("ink")
        public val POPUP: AnnotationSubtype = AnnotationSubtype("popup")
        public val FILE_ATTACHMENT: AnnotationSubtype = AnnotationSubtype("file-attachment")
        public val REDACT: AnnotationSubtype = AnnotationSubtype("redact")
        public val WIDGET: AnnotationSubtype = AnnotationSubtype("widget")

        /** An annotation whose PDF subtype the engine has no reader for. */
        public val UNSUPPORTED: AnnotationSubtype = AnnotationSubtype("unsupported")

        /** Every constant this type names, in the engine catalog's order. */
        public val named: List<AnnotationSubtype> = listOf(
            TEXT, LINK, FREE_TEXT, LINE, SQUARE, CIRCLE, POLYGON, POLYLINE, HIGHLIGHT, UNDERLINE,
            SQUIGGLY, STRIKEOUT, STAMP, CARET, INK, POPUP, FILE_ATTACHMENT, REDACT, WIDGET, UNSUPPORTED,
        )
    }
}

/**
 * An annotation's `/IT` as a read reports it, in the engine's spelling.
 * [value] is the wire string.
 *
 * The set is open, so this is a value class rather than an enum: the constants
 * are the intents the engine names
 * (`crates/epdf-engine/src/features/annotation/intent.rs`), and a newer engine
 * can name more without breaking a caller's `when`. Each kind reads only its
 * own intents.
 */
@Serializable
@JvmInline
public value class AnnotationIntent(public val value: String) {
    /** The wire string. */
    override fun toString(): String = value

    public companion object {
        /** An ink's: a freehand highlighter. */
        public val INK_HIGHLIGHT: AnnotationIntent = AnnotationIntent("ink-highlight")

        /** A strikeout's: text marked for deletion. */
        public val STRIKEOUT_TEXT_EDIT: AnnotationIntent = AnnotationIntent("strikeout-text-edit")

        /** A line's. */
        public val LINE_ARROW: AnnotationIntent = AnnotationIntent("line-arrow")

        /** A line's: a distance measurement. */
        public val LINE_DIMENSION: AnnotationIntent = AnnotationIntent("line-dimension")

        /** A polygon's. */
        public val POLYGON_CLOUD: AnnotationIntent = AnnotationIntent("polygon-cloud")

        /** A polygon's: an area measurement. */
        public val POLYGON_DIMENSION: AnnotationIntent = AnnotationIntent("polygon-dimension")

        /** A polyline's: a perimeter measurement. */
        public val POLYLINE_DIMENSION: AnnotationIntent = AnnotationIntent("polyline-dimension")

        /** A free text's: a text box. */
        public val FREE_TEXT: AnnotationIntent = AnnotationIntent("free-text")

        /** A free text's: a text box with a leader line to what it calls out. */
        public val FREE_TEXT_CALLOUT: AnnotationIntent = AnnotationIntent("free-text-callout")

        /** A free text's: typewriter text. A read reports it; no draft writes it. */
        public val FREE_TEXT_TYPEWRITER: AnnotationIntent = AnnotationIntent("free-text-typewriter")

        /** A caret's: the caret of a replacement, paired with a strikeout. */
        public val REPLACE: AnnotationIntent = AnnotationIntent("replace")
    }
}

/** [AnnotationTool.id] as a [ToolId]. */
public val AnnotationTool.toolId: ToolId get() = ToolId(id)
