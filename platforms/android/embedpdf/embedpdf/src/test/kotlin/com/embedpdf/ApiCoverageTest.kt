package com.embedpdf

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The reads and writes that close API coverage gaps, each through the
 * capability that owns it, and a viewer whose scope allows only `doc.open`
 * advertising none of the permissions those reads report.
 */
class ApiCoverageTest {
    @Test
    fun customMetadataKeepsOmittedKeysAndClearsAnExplicitNull() = viewerTest {
        val viewer = started(ViewerConfig(metadata = true))
        val document = viewer.open(Fixture.bytes("report"))
        val metadata = viewer.metadata(document)
        metadata.refreshCustom()
        metadata.updateCustom(mapOf("Project" to "Android", "Keep" to "yes"))
        val changed = metadata.updateCustom(mapOf("Project" to null))
        assertNull(changed["Project"])
        assertEquals("yes", changed["Keep"])
        metadata.refreshCustom()
        assertEquals("yes", metadata.customSnapshot()?.get("Keep"))
        assertEquals(ResourceStatus.READY, metadata.customStatus())
        expectCancelledBeforeItStarts { metadata.refreshCustom() }
        expectCancelledBeforeItStarts { metadata.updateCustom(mapOf("Cancelled" to "yes")) }
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { metadata.customSnapshot() }
        viewer.destroy()
    }

    @Test
    fun pageSelectionKeepsItsRefsAcrossAReorder() = viewerTest {
        val viewer = started(ViewerConfig(pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        val edit = viewer.pageEdit(document)
        val page = viewer.pages(document)[0].page
        edit.select(listOf(page))
        edit.movePages(listOf(page), placement = PageEditPlacementEnd)
        assertEquals(listOf(page), edit.selectedPages())
        assertTrue(edit.canExtract())
        viewer.destroy()
    }

    @Test
    fun formValidationAndPermissionReadsAreExposed() = viewerTest {
        val viewer = started(ViewerConfig(interaction = true, form = FormConfig(validation = FormValidation.NONE)))
        val document = viewer.open(Fixture.bytes("form_fields"))
        val form = viewer.form(document)
        form.refresh()
        val validation = form.validate()
        assertEquals(validation.missing.isEmpty(), validation.valid)
        assertTrue(form.canRestoreAttribution())
        assertTrue(form.canWriteScripts())
        viewer.destroy()
    }

    @Test
    fun stageDestinationAndZoomReadsAreExposed() = viewerTest {
        val viewer = started(ViewerConfig(stage = StageConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val stage = viewer.stage(document)
        stage.setViewportSize(Size(800.0, 600.0))
        assertEquals(document.pageCount.toInt(), stage.pageCount())
        stage.canZoomIn()
        stage.canZoomOut()
        stage.currentDestination()?.let { stage.goToDestination(it) }
        viewer.destroy()
    }

    @Test
    fun aDeniedScopeAdvertisesNoPermission() = viewerTest {
        val viewer = started(
            ViewerConfig(
                scope = listOf("doc.open"),
                interaction = true,
                link = true,
                annotation = "",
                form = FormConfig(),
                redaction = RedactionConfig(),
                stamp = StampConfig(),
                pageEdit = true,
                signature = SignatureConfig(),
            ),
        )
        val document = viewer.open(Fixture.bytes("report"))
        assertFalse(viewer.pageEdit(document).canExtract())
        val form = viewer.form(document)
        assertFalse(form.canRestoreAttribution())
        assertFalse(form.canWriteScripts())
        assertFalse(viewer.signatures(document).canReadRevision())
        assertFalse(viewer.stamps().canCreateFromAnnotations(document))
        assertFalse(viewer.interaction(document).hasCursorClaim())
        val page = viewer.pages(document)[0].page
        viewer.link(document).status(page)
        val redaction = viewer.redaction(document)
        val ref = AnnotationRefObjectNumber(page, 999_999u)
        assertFalse(redaction.canUnmark(ref))
        assertFalse(redaction.canUpdateLabel(ref))
        viewer.destroy()
    }
}
