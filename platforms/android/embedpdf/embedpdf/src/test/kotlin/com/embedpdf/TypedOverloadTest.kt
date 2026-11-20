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
 * Each typed overload leaves the engine where its JSON twin does. The typed
 * drafts and patches, and the measurement and shell cases, are their waves'.
 */
class TypedOverloadTest {
    private val box = """{"subtype":"square","box":{"x":100,"y":120,"width":80,"height":40}}"""
    private val uriJson = """{"kind":"uri","uri":"https://example.com"}"""

    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val page: PageRef, val annotations: AnnotationCapability)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(interaction = true, link = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("report"))
        val page = viewer.pages(document)[0].page
        val annotations = viewer.annotations(document)
        annotations.refresh()
        return Opened(viewer, document, page, annotations)
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
