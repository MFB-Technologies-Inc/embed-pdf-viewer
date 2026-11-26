package com.embedpdf

/**
 * Whether a call includes a part the session may not be allowed to read: a
 * search hit's snippet, or a page picture's annotations or form fields.
 *
 * The engine's default is [WHEN_PERMITTED], which no `Boolean` can say: what it
 * includes follows the session's scope, so the same call draws annotations in
 * one viewer and leaves them out in another.
 */
public enum class ContentInclusion {
    /** Include the part when the session may read it, and leave it out otherwise. The engine's default. */
    WHEN_PERMITTED,

    /** Include the part. A call that produces it throws `permission-denied` when the session may not read it. */
    ALWAYS,

    /** Leave the part out. */
    NEVER,
    ;

    /** The engine's spelling: `null` is [WHEN_PERMITTED]. */
    internal val engineValue: Boolean?
        get() = when (this) {
            WHEN_PERMITTED -> null
            ALWAYS -> true
            NEVER -> false
        }
}
