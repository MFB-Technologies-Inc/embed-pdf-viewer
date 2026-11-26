package com.embedpdf

import kotlinx.serialization.Serializable

/**
 * A tool's id in a document's interaction hub, which every plugin's tools
 * join: the annotation plugin's authoring tools, the form plugin's field
 * tools, and the hub's own. [value] is the wire string.
 *
 * The set is open, so this is a value class rather than an enum: the constants
 * are the built-in tools, and an app's own tool, registered through
 * [InteractionCapability.registerTool] or the annotation settings' tool table,
 * is `ToolId("laser")`. A tool a newer engine adds needs no new constant to
 * pass, and adding one breaks no caller's `when`.
 */
@Serializable
@JvmInline
public value class ToolId(public val value: String) {
    /** The wire string. */
    override fun toString(): String = value

    public companion object {
        // The hub's own.

        /**
         * `pointer`: selects text, edits annotations, fills forms and follows
         * links. The default tool.
         */
        public val POINTER: ToolId = ToolId("pointer")
        public val PAN: ToolId = ToolId("pan")
        public val ZOOM: ToolId = ToolId("zoom")

        // The render plugin's.
        public val SNAPSHOT: ToolId = ToolId("snapshot")

        // The form plugin's.
        public val FORM_FILL: ToolId = ToolId("form-fill")
        public val FORM_EDIT: ToolId = ToolId("form-edit")
        public val FORM_TEXT: ToolId = ToolId("form-text")
        public val FORM_CHECKBOX: ToolId = ToolId("form-checkbox")
        public val FORM_RADIO: ToolId = ToolId("form-radio")
        public val FORM_COMBOBOX: ToolId = ToolId("form-combobox")
        public val FORM_LISTBOX: ToolId = ToolId("form-listbox")
        public val FORM_BUTTON: ToolId = ToolId("form-button")
        public val FORM_SIGNATURE: ToolId = ToolId("form-signature")

        // The annotation plugin's.
        public val SQUARE: ToolId = ToolId("square")
        public val CIRCLE: ToolId = ToolId("circle")
        public val LINE: ToolId = ToolId("line")
        public val DISTANCE: ToolId = ToolId("distance")
        public val CALIBRATE: ToolId = ToolId("calibrate")
        public val POLYGON: ToolId = ToolId("polygon")
        public val POLYLINE: ToolId = ToolId("polyline")
        public val PERIMETER: ToolId = ToolId("perimeter")
        public val AREA: ToolId = ToolId("area")
        public val INK: ToolId = ToolId("ink")
        public val INK_HIGHLIGHT: ToolId = ToolId("ink-highlight")
        public val FREE_TEXT: ToolId = ToolId("free-text")
        public val FREE_TEXT_CALLOUT: ToolId = ToolId("free-text-callout")
        public val HIGHLIGHT: ToolId = ToolId("highlight")
        public val REDACT: ToolId = ToolId("redact")
        public val UNDERLINE: ToolId = ToolId("underline")
        public val STRIKEOUT: ToolId = ToolId("strikeout")
        public val SQUIGGLY: ToolId = ToolId("squiggly")
        public val INSERT_TEXT: ToolId = ToolId("insert-text")
        public val REPLACE_TEXT: ToolId = ToolId("replace-text")
        public val STAMP: ToolId = ToolId("stamp")
        public val LINK: ToolId = ToolId("link")
        public val NOTE: ToolId = ToolId("note")
        public val ATTACHMENT: ToolId = ToolId("attachment")

        /** Every constant this type names. */
        public val named: List<ToolId> = listOf(
            POINTER, PAN, ZOOM, SNAPSHOT, FORM_FILL, FORM_EDIT, FORM_TEXT, FORM_CHECKBOX, FORM_RADIO,
            FORM_COMBOBOX, FORM_LISTBOX, FORM_BUTTON, FORM_SIGNATURE, SQUARE, CIRCLE, LINE, DISTANCE,
            CALIBRATE, POLYGON, POLYLINE, PERIMETER, AREA, INK, INK_HIGHLIGHT, FREE_TEXT,
            FREE_TEXT_CALLOUT, HIGHLIGHT, REDACT, UNDERLINE, STRIKEOUT, SQUIGGLY, INSERT_TEXT,
            REPLACE_TEXT, STAMP, LINK, NOTE, ATTACHMENT,
        )
    }
}

/** [InteractionTool.id] as a [ToolId]. */
public val InteractionTool.toolId: ToolId get() = ToolId(id)
