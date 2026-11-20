package com.embedpdf

import kotlinx.coroutines.delay
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Test

class LinkTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val link: LinkCapability, val page: PageRef)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(interaction = true, link = true))
        val document = viewer.open(Fixture.bytes("features"))
        val page = viewer.pages(document)[0].page
        val link = viewer.link(document)
        link.ensureLoaded(page)
        return Opened(viewer, document, link, page)
    }

    @Test
    fun aPagesLinksAreListed() = viewerTest {
        val (viewer, _, link, page) = opened()
        assertEquals(2, link.links(page).size)
        viewer.destroy()
    }

    @Test
    fun aBareTargetIsLabelled() = viewerTest {
        val (viewer, _, link, page) = opened()
        assertEquals("https://example.invalid/x", link.label(targetJson = """{"kind": "uri", "uri": "https://example.invalid/x"}"""))
        assertEquals("NextPage", link.label(targetJson = """{"kind": "named", "name": "NextPage"}"""))
        // The same label a listed link gets for its own target.
        val listed = link.links(page).first()
        assertEquals(link.label(listed), link.label(listed.targetJson))
        viewer.destroy()
    }

    @Test
    fun aUriLinkIsReportedToTheApp() = viewerTest {
        val (viewer, _, link, page) = opened()
        val events = subscribed(link.observeEvents())
        val activation = link.activate(Point(100.0, 145.0), page)
        assertEquals(LinkActivationUri("https://example.invalid/target"), activation)
        val activated = events.nextMapped { it as? LinkEventActivated }
        assertEquals(LinkActivationUri("https://example.invalid/target"), activated.event.activation)
        viewer.destroy()
    }

    @Test
    fun aGotoLinkWithoutAStageReportsItsDestination() = viewerTest {
        val (viewer, _, link, page) = opened()
        val activation = link.activate(Point(100.0, 85.0), page)
        val destination = (activation as? LinkActivationDestination)?.destinationJson
            ?: error("expected a destination, got $activation")
        // The page-space `PageDestination` record.
        assertEquals("xyz", Json.parseToJsonElement(destination).jsonObject["kind"]?.jsonPrimitive?.content)
        viewer.destroy()
    }

    @Test
    fun aTapOnALinkActivatesItOnceTheTapHandlerIsRegistered() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, link = true))
        val document = viewer.open(Fixture.bytes("features"))
        val page = viewer.pages(document)[0].page
        val link = viewer.link(document)
        val interaction = viewer.interaction(document)
        link.ensureLoaded(page)
        val registration = link.registerTapHandler()
        val events = subscribed(link.observeEvents())
        // Inside the external link: x 40...360, y 130...160.
        val at = Point(100.0, 145.0)
        for (phase in listOf(PointerPhase.DOWN, PointerPhase.UP)) {
            interaction.dispatchPointer(
                PointerSample(
                    phase = phase,
                    viewport = at,
                    page = PointerPageHit(page = page, point = at),
                    modifiers = Modifiers(),
                    pointerType = PointerKind.TOUCH,
                ),
            )
        }
        val activated = events.nextMapped { it as? LinkEventActivated }
        assertEquals(LinkActivationUri("https://example.invalid/target"), activated.event.activation)
        registration.unregister()
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, link, page) = opened()
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { link.links(page) }
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, link, page) = opened()
        val collector = collecting(link.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        link.activate(Point(100.0, 145.0), page)
        viewer.destroy()
    }
}
