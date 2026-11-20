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
}
