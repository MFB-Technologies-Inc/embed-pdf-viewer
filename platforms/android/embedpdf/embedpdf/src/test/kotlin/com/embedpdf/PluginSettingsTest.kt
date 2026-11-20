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

    @Test
    fun interactionDefaultAppliesToNewDocuments() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true))
        val first = viewer.open(Fixture.bytes("report"))
        val interaction = viewer.interaction(first)
        val registered = interaction.settings()
        interaction.updateSettings(InteractionSettingsPatch(defaultTool = "pan"))
        val second = viewer.open(Fixture.bytes("report"))
        val other = viewer.interaction(second)
        assertEquals(ToolId.PAN, other.activeToolId())
        other.resetSettings()
        assertEquals(registered, interaction.settings())
        viewer.destroy()
    }

    @Test
    fun localeSettingsPreserveLazyLoaderAcrossUpdateAndReset() = viewerTest {
        val (viewer, i18n) = I18nTest.started()
        val registered = i18n.settings()
        i18n.updateSettings(I18nSettingsPatch(fallbackLocale = "es"))
        i18n.setLocale("es")
        assertEquals("2 páginas", i18n.t("pages", params = mapOf("count" to I18nParamNumber(2.0))))
        i18n.updateSettings(I18nSettingsPatch(clearLocale = true))
        assertEquals(null, i18n.settings().locale)
        i18n.resetSettings()
        assertEquals(registered, i18n.settings())
        viewer.destroy()
    }

    @Test
    fun stageSettingsMergeAndReset() = viewerTest {
        // Without responsive rules: the default ones rewrite padding once they apply.
        val viewer = started(ViewerConfig(stage = StageConfig(responsive = emptyList())))
        val stage = viewer.stage(viewer.open(Fixture.bytes("report")))
        val registered = stage.settings()
        stage.updateSettings(StageSettingsPatch(layout = StageLayout.HORIZONTAL, turnGestures = null))
        stage.updateSettings(StageSettingsPatch(zoomGestures = !registered.zoomGestures, turnGestures = null))
        val changed = stage.settings()
        assertEquals(StageLayout.HORIZONTAL, changed.layout)
        assertEquals(!registered.zoomGestures, changed.zoomGestures)
        assertEquals(registered.flow, changed.flow)
        stage.resetSettings()
        assertEquals(registered, stage.settings())
        viewer.destroy()
    }
}
