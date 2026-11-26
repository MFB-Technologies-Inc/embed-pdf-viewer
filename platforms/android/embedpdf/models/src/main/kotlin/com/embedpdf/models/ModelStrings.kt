package com.embedpdf.models

import java.text.NumberFormat

/** Why the last undo or redo skipped some of its parts. */
public sealed interface HistoryNotice {
    /** An undo skipped [count] of its changes, which could not be undone. */
    public data class SkippedUndo(val count: Long) : HistoryNotice

    /** A redo skipped [count] of its changes, which could not be redone. */
    public data class SkippedRedo(val count: Long) : HistoryNotice
}

/**
 * The words [HistoryModel] shows. Every member has an English default, so an
 * app overrides only what it words another way; a Compose layer passes one
 * backed by string resources.
 */
public interface HistoryStrings {
    /** The undo control's title when there is no change to name: "Undo". */
    public val undo: String get() = "Undo"

    /** The redo control's title when there is no change to name: "Redo". */
    public val redo: String get() = "Redo"

    /** The change's description when the translator has none: "change". */
    public val change: String get() = "change"

    /** The undo title naming the change [label] describes: "Undo Rotate pages". */
    public fun undoTitle(label: String): String = "$undo $label"

    /** The redo title naming the change [label] describes: "Redo Rotate pages". */
    public fun redoTitle(label: String): String = "$redo $label"

    /** The text of [notice]: "Some changes could not be undone." */
    public fun notice(notice: HistoryNotice): String = when (notice) {
        is HistoryNotice.SkippedUndo -> "Some changes could not be undone."
        is HistoryNotice.SkippedRedo -> "Some changes could not be redone."
    }

    public companion object {
        /** Every default, in English. */
        public val English: HistoryStrings = object : HistoryStrings {}
    }
}

/**
 * The words [SearchModel] shows. Every member has an English default; a
 * Compose layer passes one backed by string resources.
 */
public interface SearchStrings {
    /**
     * The active hit's place among the hits: [index] is one-based and [count]
     * is the hits found so far, with [searching] true while the search still
     * runs. The default groups digits as the locale does: "3 of 12", and
     * "3 of 12+" while searching.
     */
    public fun searchSummary(index: Int, count: Int, searching: Boolean): String {
        val numbers = NumberFormat.getIntegerInstance()
        val text = "${numbers.format(index)} of ${numbers.format(count)}"
        return if (searching) "$text+" else text
    }

    public companion object {
        /** Every default, in English. */
        public val English: SearchStrings = object : SearchStrings {}
    }
}
