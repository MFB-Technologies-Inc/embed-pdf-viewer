package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Test

class PluginSettingsTest {
    @Test
    fun searchHighlightsAndNullableRevealMergeIndependently() = viewerTest {
        val viewer = started(ViewerConfig(search = SearchConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val search = viewer.search(document)
        val registered = search.settings()
        search.updateSettings(
            SearchSettingsPatch(
                reveal = SearchRevealPatch(clearAnchor = true),
                highlight = SearchHighlightPatch(color = "red", blendMode = SearchBlendMode.SCREEN),
            ),
        )
        val changed = search.settings()
        assertEquals(null, changed.reveal.anchor)
        assertEquals(registered.reveal.behavior, changed.reveal.behavior)
        assertEquals(registered.highlight.activeColor, changed.highlight.activeColor)
        search.updateSettings(SearchSettingsPatch(highlight = SearchHighlightPatch(activeColor = "blue")))
        assertEquals("red", search.settings().highlight.color)
        search.resetSettings()
        assertEquals(registered, search.settings())
        viewer.destroy()
    }

    @Test
    fun renderTileAndSnapshotPatchesPreserveOtherSettings() = viewerTest {
        val viewer = started(ViewerConfig(render = RenderConfig(fullPage = RenderFullPageOptions(maxWidth = 480.0))))
        val document = viewer.open(Fixture.bytes("report"))
        val render = viewer.render(document)
        val registered = render.settings()
        render.updateSettings(
            RenderSettingsPatch(
                tiles = RenderTilesPatchOff,
                slowAfterMs = 800.0,
                snapshot = RenderSnapshotPatch(scale = 3.0, boxLook = RenderSnapshotBox(fill = "red", stroke = "blue")),
            ),
        )
        render.updateSettings(RenderSettingsPatch(snapshot = RenderSnapshotPatch(boxLook = RenderSnapshotBox(fill = "green"))))
        val changed = render.settings()
        assertEquals(480.0, changed.fullPage.maxWidth)
        assertEquals(RenderTilesSettingOff, changed.tiles)
        assertEquals(3.0, changed.snapshot.scale, 0.0)
        assertEquals(null, changed.snapshot.boxLook.stroke)
        render.resetSettings()
        assertEquals(registered, render.settings())
        viewer.destroy()
    }
}
