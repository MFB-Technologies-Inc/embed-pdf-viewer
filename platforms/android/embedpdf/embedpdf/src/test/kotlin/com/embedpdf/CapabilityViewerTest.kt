package com.embedpdf

import org.junit.Assert.assertSame
import org.junit.Test

/** The capabilities a model follows hand back the viewer they came from. */
class CapabilityViewerTest {
    @Test
    fun annotationInteractionAndSearchNameTheirViewer() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", search = SearchConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        assertSame(viewer, viewer.annotations(document).viewer)
        assertSame(viewer, viewer.interaction(document).viewer)
        assertSame(viewer, viewer.search(document).viewer)
        viewer.destroy()
    }
}
