package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.double
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Each typed overload leaves the engine where its JSON twin does. The
 * measurement and shell cases are their waves'.
 */
class TypedOverloadTest {
    private val box = SquareAnnotationDraft(Rect(100.0, 120.0, 80.0, 40.0))
    private val uriJson = """{"kind":"uri","uri":"https://example.com"}"""

    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val page: PageRef, val annotations: AnnotationCapability)

    private suspend fun opened(fixture: String = "report"): Opened {
        val viewer = started(ViewerConfig(interaction = true, link = true, annotation = ""))
        val document = viewer.open(Fixture.bytes(fixture))
        val page = viewer.pages(document)[0].page
        val annotations = viewer.annotations(document)
        annotations.refresh()
        return Opened(viewer, document, page, annotations)
    }

    @Test
    fun aSelectionTakesATypedPatch() = viewerTest {
        val (viewer, _, page, annotations) = opened("annotation_kinds")
        // The fixture has one highlight; the second is made here.
        val fixture = annotations.list().first { it.subtype == AnnotationSubtype.HIGHLIGHT }
        val created = annotations.create(AnnotationDraftTest.fullHighlight, page)
        val highlights = listOf(fixture.annotationRef, created)
        annotations.selection.select(highlights)

        val typed = annotations.selection.update(HighlightAnnotationPatch(opacity = 0.4))
        assertEquals(highlights.toSet(), typed.applied.toSet())
        assertEquals(emptyList<AnnotationBatchFailure>(), typed.failed)
        for (highlight in highlights) assertEquals(0.4, annotations.get(highlight)?.highlight?.opacity)

        val json = annotations.selection.update("""{"opacity":0.4}""")
        assertEquals(typed, json)
        for (highlight in highlights) assertEquals(0.4, annotations.get(highlight)?.highlight?.opacity)
        viewer.destroy()
    }

    @Test
    fun aDraftedGotoKeepsItsCoordinates() = viewerTest {
        val (viewer, _, page, annotations) = opened()
        val target = AnnotationLinkTarget.Goto(PageDestinationXyz(page, 30.0, 40.0, null))
        val ref = annotations.create(LinkAnnotationDraft(Rect(100.0, 120.0, 80.0, 40.0), target), page)
        assertEquals(target, annotations.get(ref)?.link?.target)
        val area = AnnotationLinkTarget.Goto(PageDestinationFitR(page, 10.0, 20.0, 100.0, 50.0))
        annotations.update(ref, LinkAnnotationPatch(target = value(area)))
        assertEquals(area, annotations.get(ref)?.link?.target)
        viewer.destroy()
    }

    @Test
    fun anAttachedLinkIsSetAndReadTyped() = viewerTest {
        val (viewer, _, page, annotations) = opened()
        val square = annotations.create(box, page)
        assertNull(annotations.threads.linkTarget(square))

        annotations.threads.setLink(AnnotationLinkTarget.Uri("https://example.com"), square)
        assertEquals(AnnotationLinkTarget.Uri("https://example.com"), annotations.threads.linkTarget(square))
        val typed = annotations.threads.link(square)

        annotations.threads.clearLink(square)
        annotations.threads.setLink(uriJson, square)
        assertEquals(typed, annotations.threads.link(square))
        viewer.destroy()
    }

    @Test
    fun anAttachedGotoKeepsItsCoordinates() = viewerTest {
        val (viewer, _, page, annotations) = opened()
        val square = annotations.create(box, page)
        val target = AnnotationLinkTarget.Goto(PageDestinationXyz(page, 30.0, 40.0, 2.0))
        annotations.threads.setLink(target, square)
        assertEquals(target, annotations.threads.linkTarget(square))
        viewer.destroy()
    }

    @Test
    fun aLinkTargetResolvesActivatesAndLabelsTyped() = viewerTest {
        val (viewer, document, page, _) = opened()
        val link = viewer.link(document)
        val uri = AnnotationLinkTarget.Uri("https://example.com")

        val resolution = link.resolve(uri)
        assertEquals(LinkResolutionUri("https://example.com"), resolution)
        assertEquals(resolution, link.resolve(uriJson))

        val activation = link.activate(uri)
        assertEquals(LinkActivationUri("https://example.com"), activation)
        assertEquals(activation, link.activate(uriJson))

        val label = link.label(uri)
        assertEquals(label, link.label(uriJson))
        assertTrue(label.contains("https://example.com"))

        val place = link.resolve(AnnotationLinkTarget.Goto(PageDestinationXyz(page, 30.0, 40.0, null)))
        val destination = Json.parseToJsonElement((place as LinkResolutionDestination).destinationJson).jsonObject
        assertEquals("xyz", destination.getValue("kind").jsonPrimitive.content)
        assertEquals(30.0, destination.getValue("x").jsonPrimitive.double, 0.0)
        assertEquals(40.0, destination.getValue("y").jsonPrimitive.double, 0.0)
        viewer.destroy()
    }
}
