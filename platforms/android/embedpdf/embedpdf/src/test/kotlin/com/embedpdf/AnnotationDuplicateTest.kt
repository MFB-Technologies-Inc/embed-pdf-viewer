package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.long
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Duplication, movability and remeasuring, against the engine. */
class AnnotationDuplicateTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val annotations: AnnotationCapability, val page: PageRef)

    private companion object {
        const val SQUARE = """{"subtype": "square", "box": {"x": 72, "y": 72, "width": 100, "height": 50}, "contents": "Original"}"""

        suspend fun opened(history: Boolean = false): Opened {
            val viewer = started(ViewerConfig(interaction = true, annotation = "", history = history))
            val document = viewer.open(Fixture.bytes("report"))
            val annotations = viewer.annotations(document)
            annotations.refresh()
            return Opened(viewer, document, annotations, viewer.pages(document)[0].page)
        }

        fun feetPerInch(feet: Double) = PdfMeasurement(
            subtype = "rectilinear",
            ratio = "1 in = ${feet.toInt()} ft",
            x = listOf(PdfNumberFormat(unit = "ft", conversion = feet / 72, precision = 100.0)),
            distance = listOf(PdfNumberFormat(unit = "ft", conversion = 1.0, precision = 100.0)),
            area = listOf(PdfNumberFormat(unit = "sq ft", conversion = 1.0, precision = 100.0)),
        )
    }

    @Test
    fun duplicateCopiesFieldsDrawingFileAndLinkWithNewRefsAsOneUndoStep() = viewerTest {
        val (viewer, document, annotations, page) = opened(history = true)
        val drafts = annotations.drafts
        val threads = annotations.threads
        val square = annotations.create(SQUARE, page)
        threads.setLink(AnnotationLinkTarget.Uri("https://example.com/a"), square)
        val ink = annotations.create(InkAnnotationDraft(listOf(listOf(Point(100.0, 300.0), Point(150.0, 320.0), Point(200.0, 300.0)))), page)
        val bytes = byteArrayOf(1, 2, 3, 4, 5)
        val file = drafts.createAttachment(AnnotationAttachmentFile(bytes, "data.bin"), Point(300.0, 300.0), page)
        val history = viewer.history(document)
        val originals = listOf(square, ink, file)

        val result = annotations.duplicate(originals, AnnotationDuplicateOptions(offset = Point(20.0, 10.0)))

        assertEquals(3, result.annotations.size)
        assertEquals("[]", result.droppedJson)
        val copies = result.annotations.map { it.annotationRef }
        assertTrue(copies.intersect(originals.toSet()).isEmpty())
        assertEquals(3, copies.distinct().size)
        for ((original, copy) in originals.zip(result.annotations)) {
            assertEquals(annotations.get(original)!!.subtype.value, copy.subtype)
        }
        val squareCopy = result.annotations[0]
        val squareRect = annotations.get(square)!!.rect
        assertEquals("Original", squareCopy.contents)
        assertEquals(squareRect.x + 20, squareCopy.rect.x, 1e-3)
        assertEquals(AnnotationLinkTarget.Uri("https://example.com/a"), threads.linkTarget(squareCopy.annotationRef))
        assertArrayEquals(bytes, drafts.readAttachment(copies[2]))

        history.undo()
        for (copy in copies) assertNull(annotations.get(copy))
        for (original in originals) assertNotNull(annotations.get(original))
        viewer.destroy()
    }

    @Test
    fun duplicatingNothingCopiesNothing() = viewerTest {
        val (viewer, _, annotations, _) = opened()
        assertTrue(annotations.duplicate(emptyList()).annotations.isEmpty())
        viewer.destroy()
    }

    @Test
    fun duplicatingAMissingAnnotationIsNotFound() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val before = annotations.list().size
        expectCode(PdfException.Code.NOT_FOUND) { annotations.duplicate(listOf(AnnotationRefObjectNumber(page, 99999u))) }
        assertEquals(before, annotations.list().size)
        viewer.destroy()
    }

    @Test
    fun aLockedAnnotationCannotBeDuplicatedByHand() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val locked = annotations.create(SQUARE, page)
        annotations.update(locked, """{"locked": true}""")
        assertFalse(annotations.canDuplicate(listOf(locked)))
        val selection = annotations.selection
        selection.select(listOf(locked))
        assertFalse(selection.canDuplicate())
        viewer.destroy()
    }

    @Test
    fun canDuplicateTruthTable() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val square = annotations.create(SQUARE, page)
        val missing = AnnotationRefObjectNumber(page, 99999u)
        assertFalse(annotations.canDuplicate(emptyList()))
        assertTrue(annotations.canDuplicate(listOf(square)))
        assertFalse(annotations.canDuplicate(listOf(square, missing)))
        assertFalse(annotations.canDuplicate(listOf(missing)))
        viewer.destroy()
    }

    @Test
    fun aSessionThatMayNotCreateCannotDuplicate() = viewerTest {
        val viewer = started(ViewerConfig(scope = listOf("doc.open"), interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("annotation_kinds"))
        val annotations = viewer.annotations(document)
        val page = viewer.pages(document)[0].page
        val any = AnnotationRefObjectNumber(page, 1u)
        assertFalse(annotations.canDuplicate(listOf(any)))
        expectCode(PdfException.Code.PERMISSION_DENIED) { annotations.duplicate(listOf(any)) }
        assertFalse(annotations.selection.canDuplicate())
        viewer.destroy()
    }

    @Test
    fun selectionDuplicateCopiesTheSelectionAndSelectsTheCopies() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val square = annotations.create(SQUARE, page)
        val selection = annotations.selection
        selection.select(listOf(square))
        assertTrue(selection.canDuplicate())
        val result = selection.duplicate(Point(0.0, 40.0))
        val copy = result.annotations.single()
        assertNotEquals(square, copy.annotationRef)
        assertEquals(annotations.get(square)!!.rect.y + 40, copy.rect.y, 1e-3)
        assertEquals(listOf(copy.annotationRef), selection.refs())
        viewer.destroy()
    }

    @Test
    fun selectionDuplicateWithNothingSelectedIsEmpty() = viewerTest {
        val (viewer, _, annotations, _) = opened()
        val selection = annotations.selection
        assertFalse(selection.canDuplicate())
        assertTrue(selection.duplicate().annotations.isEmpty())
        viewer.destroy()
    }

    @Test
    fun aHighlightIsNotMovableAndASquareIs() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val square = annotations.create(SQUARE, page)
        val quad = Quad(Point(72.0, 100.0), Point(172.0, 100.0), Point(72.0, 120.0), Point(172.0, 120.0))
        val highlight = annotations.create(HighlightAnnotationDraft(listOf(quad)), page)
        assertTrue(annotations.isMovable(square))
        assertFalse(annotations.isMovable(highlight))
        assertFalse(annotations.isMovable(AnnotationRefObjectNumber(page, 99999u)))
        viewer.destroy()
    }

    @Test
    fun remeasuringAPageRelabelsItsMeasurements() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", measurement = MeasurementConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val measurement = viewer.measurement(document)
        measurement.ensureLoaded(page)
        val json = measurement.createMeasurement(
            CreateMeasurementInput(kind = MeasurementKind.DISTANCE, page = page, points = listOf(Point(10.0, 10.0), Point(82.0, 10.0))),
        )
        val line = AnnotationRefObjectNumber(page, Json.parseToJsonElement(json).jsonObject.getValue("objectNumber").jsonPrimitive.long.toUInt())
        val annotations = viewer.annotations(document)
        val before = (measurement.readout(line) as MeasurementReadingValue).readout

        annotations.whenRemeasurable()
        val report = annotations.stageRemeasure(page, feetPerInch(2.0))

        assertNull(report.error)
        assertEquals(page, report.page)
        assertTrue(line in report.updated)
        val after = (measurement.readout(line) as MeasurementReadingValue).readout
        assertNotEquals(before.label, after.label)
        assertTrue(after.label, "ft" in after.label)
        assertEquals(2.0, after.value, 1e-3)
        viewer.destroy()
    }

    @Test
    fun aRemeasureScaleJsonCannotHoldIsInvalidInput() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) {
            annotations.stageRemeasure(page, PdfMeasurement(subtype = "rectilinear", cyx = Double.NaN))
        }
        viewer.destroy()
    }

    @Test
    fun anOffsetThatIsNotFiniteIsInvalidInput() = viewerTest {
        val (viewer, _, annotations, page) = opened()
        val square = annotations.create(SQUARE, page)
        val before = annotations.list().size
        expectCode(PdfException.Code.INVALID_INPUT) {
            annotations.duplicate(listOf(square), AnnotationDuplicateOptions(offset = Point(Double.NaN, 0.0)))
        }
        val selection = annotations.selection
        selection.select(listOf(square))
        expectCode(PdfException.Code.INVALID_INPUT) { selection.duplicate(Point(0.0, Double.POSITIVE_INFINITY)) }
        assertEquals(before, annotations.list().size)
        viewer.destroy()
    }
}
