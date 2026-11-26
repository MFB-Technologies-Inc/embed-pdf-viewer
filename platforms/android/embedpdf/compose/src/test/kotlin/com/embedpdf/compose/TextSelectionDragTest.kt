package com.embedpdf.compose

import com.embedpdf.compose.TextSelectionDrag.Command
import com.embedpdf.compose.TextSelectionDrag.Event
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/** `TextSelectionDragTests`. */
class TextSelectionDragTest {
    @Test
    fun theFirstChangeBeginsAndLaterChangesExtend() {
        val drag = TextSelectionDrag()
        assertEquals(Command.BEGIN, drag.handle(Event.CHANGED))
        assertEquals(Command.EXTEND, drag.handle(Event.CHANGED))
        assertEquals(Command.EXTEND, drag.handle(Event.CHANGED))
    }

    @Test
    fun anEndEndsOnce() {
        val drag = TextSelectionDrag()
        drag.handle(Event.CHANGED)
        assertEquals(Command.END, drag.handle(Event.ENDED))
        assertNull(drag.handle(Event.CANCELLED))
        assertNull(drag.handle(Event.DISAPPEARED))
    }

    @Test
    fun aCancelEndsAnOpenDrag() {
        val drag = TextSelectionDrag()
        drag.handle(Event.CHANGED)
        drag.handle(Event.CHANGED)
        assertEquals(Command.END, drag.handle(Event.CANCELLED))
        assertNull(drag.handle(Event.ENDED))
    }

    @Test
    fun disappearingMidDragEndsIt() {
        val drag = TextSelectionDrag()
        drag.handle(Event.CHANGED)
        assertEquals(Command.END, drag.handle(Event.DISAPPEARED))
        assertNull(drag.handle(Event.CANCELLED))
    }

    @Test
    fun nothingEndsWithNoDragOpen() {
        for (event in listOf(Event.ENDED, Event.CANCELLED, Event.DISAPPEARED)) {
            assertNull("$event", TextSelectionDrag().handle(event))
        }
    }

    @Test
    fun aChangeAfterAnEndBeginsANewDrag() {
        val drag = TextSelectionDrag()
        drag.handle(Event.CHANGED)
        drag.handle(Event.ENDED)
        assertEquals(Command.BEGIN, drag.handle(Event.CHANGED))
        assertEquals(Command.EXTEND, drag.handle(Event.CHANGED))
        assertEquals(Command.END, drag.handle(Event.ENDED))
    }
}
