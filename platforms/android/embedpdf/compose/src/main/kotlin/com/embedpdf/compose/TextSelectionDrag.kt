package com.embedpdf.compose

/**
 * One selection drag's begin, extensions and single end, however the
 * pointer events arrive. [TextSelectionOverlay] hands it every gesture event
 * and does what it answers, so a drag begins once and ends exactly once.
 */
internal class TextSelectionDrag {
    /** What the gesture reported. */
    enum class Event {
        /** The drag has a new point. */
        CHANGED,

        /** The pointer lifted. */
        ENDED,

        /** The gesture was cancelled: another handler took the pointer, or the pointer input restarted. */
        CANCELLED,

        /** The overlay left the composition. */
        DISAPPEARED,
    }

    /** What the overlay does with the model. */
    enum class Command {
        /** Begin a selection at the event's point. */
        BEGIN,

        /** Extend the selection to the event's point. */
        EXTEND,

        /** End the selection. */
        END,
    }

    /** Whether a drag has begun and not yet ended. */
    var isOpen: Boolean = false
        private set

    /** The command [event] calls for, or `null` for none: an end, a cancel or a disappearance with no drag open. */
    fun handle(event: Event): Command? = when (event) {
        Event.CHANGED -> if (isOpen) {
            Command.EXTEND
        } else {
            isOpen = true
            Command.BEGIN
        }
        Event.ENDED, Event.CANCELLED, Event.DISAPPEARED -> if (isOpen) {
            isOpen = false
            Command.END
        } else {
            null
        }
    }
}
