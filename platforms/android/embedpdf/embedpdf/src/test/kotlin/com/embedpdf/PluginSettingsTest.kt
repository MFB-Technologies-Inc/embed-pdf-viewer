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

    @Test
    fun selectionColorsClearAndSettingsReachBothDocuments() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, selection = true))
        val first = viewer.open(Fixture.bytes("report"))
        val second = viewer.open(Fixture.bytes("report"))
        val selection = viewer.selection(first)
        val other = viewer.selection(second)
        val registered = selection.settings()
        selection.updateSettings(
            SelectionSettingsPatch(
                dragThresholdPx = 11.0,
                color = "red",
                handles = SelectionHandleSettingsPatch(color = "blue", shadow = "none"),
            ),
        )
        selection.updateSettings(SelectionSettingsPatch(clearColor = true))
        val cleared = other.settings()
        assertEquals(null, cleared.color)
        assertEquals(11.0, cleared.dragThresholdPx, 0.0)
        assertEquals("blue", cleared.handles.color)
        selection.updateSettings(SelectionSettingsPatch(handles = SelectionHandleSettingsPatch(clearColor = true)))
        assertEquals("none", other.settings().handles.shadow)
        assertEquals(null, other.settings().handles.color)
        other.resetSettings()
        assertEquals(registered, selection.settings())
        viewer.close(first)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { selection.settings() }
        assertEquals(registered, other.settings())
        viewer.destroy()
    }

    @Test
    fun commandSettingsValidateOpenAppDataAndReset() = viewerTest {
        val viewer = started(ViewerConfig(commands = true))
        val commands = viewer.commands()
        val registered = commands.settings()
        commands.updateSettings(
            CommandsSettingsPatch(
                disabledCategories = listOf("edit"),
                disabledCommands = listOf("print"),
                notAllowed = NotAllowedMode.DISABLE,
                appStateJson = "null",
            ),
        )
        val changed = commands.settings()
        assertEquals("null", changed.appStateJson)
        try {
            commands.updateSettings(CommandsSettingsPatch(appStateJson = "{"))
            org.junit.Assert.fail("an invalid appStateJson was accepted")
        } catch (e: PdfException) {
            // Refused, and the settings stay as they were.
        }
        assertEquals(changed, commands.settings())
        commands.resetSettings()
        assertEquals(registered, commands.settings())
        viewer.destroy()
    }

    @Test
    fun formSettingsKeepValidationWhileClearingFocusAndFieldBorders() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, form = FormConfig(validation = FormValidation.NONE)))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        val registered = form.settings()
        form.updateSettings(
            FormSettingsPatch(
                focus = FormFocusSettingsPatch(color = "red"),
                fields = FormFieldSettingsPatch(border = "blue", background = "white"),
                fillable = FormFillableKinds(listOf(FormFieldFamily.TEXT, FormFieldFamily.CHECKBOX)),
            ),
        )
        form.updateSettings(FormSettingsPatch(focus = FormFocusSettingsPatch(clearColor = true), fields = FormFieldSettingsPatch(clearBorder = true)))
        val changed = form.settings()
        assertEquals(FormValidation.NONE, changed.validation)
        assertEquals(null, changed.focus.color)
        assertEquals(null, changed.fields.border)
        assertEquals("white", changed.fields.background)
        assertEquals(FormFillableKinds(listOf(FormFieldFamily.TEXT, FormFieldFamily.CHECKBOX)), changed.fillable)
        form.resetSettings()
        assertEquals(registered, form.settings())
        viewer.destroy()
    }

    @Test
    fun redactionOverlayTextMergesAndResets() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", redaction = RedactionConfig(fill = "#000000")))
        val document = viewer.open(Fixture.bytes("report"))
        val redaction = viewer.redaction(document)
        val registered = redaction.settings()
        redaction.updateSettings(RedactionConfig(overlayText = RedactionOverlayText(fontSize = 9.0)))
        val changed = redaction.settings()
        assertEquals(9.0, changed.overlayText.fontSize, 0.0)
        assertEquals(registered.fill, changed.fill)
        assertEquals(registered.overlayText.color, changed.overlayText.color)
        redaction.resetSettings()
        assertEquals(registered, redaction.settings())
        viewer.destroy()
    }

    @Test
    fun measurementDefaultScaleChangesAndResets() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", measurement = MeasurementConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val measurement = viewer.measurement(document)
        val registered = measurement.settings()
        measurement.updateSettings(MeasurementConfig(defaultScale = MeasurementDefaultScaleImperial))
        val changed = measurement.settings()
        assertEquals(MeasurementDefaultScaleImperial, changed.defaultScale)
        assertEquals(registered.presets, changed.presets)
        measurement.resetSettings()
        assertEquals(registered, measurement.settings())
        viewer.destroy()
    }

    @Test
    fun stampResetRestoresRegisteredPreviewWidth() = viewerTest {
        val viewer = started(ViewerConfig(stamp = StampConfig(previewWidth = 320.0, dynamic = true)))
        val stamps = viewer.stamps()
        stamps.updateSettings(StampConfig(dynamic = false))
        assertEquals(320.0, stamps.settings().previewWidth, 0.0)
        assertEquals(false, stamps.settings().dynamic)
        stamps.resetSettings()
        assertEquals(true, stamps.settings().dynamic)
        viewer.destroy()
    }
}
