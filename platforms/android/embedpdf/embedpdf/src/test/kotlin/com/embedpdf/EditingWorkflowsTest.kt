package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Base64

/**
 * The editing workflows of `EditingWorkflowsTests` that cross the annotation
 * and form plugins. The command and named-action cases are CommandsTest's
 * and ActionsTest's.
 */
class EditingWorkflowsTest {
    private suspend fun editingViewer(config: ViewerConfig = ViewerConfig(interaction = true, annotation = "")): PdfViewer =
        started(config)

    private fun obj(json: String): JsonObject = Json.parseToJsonElement(json).jsonObject

    private fun resources(bundle: JsonObject): Map<String, Map<String, String>> =
        bundle.getValue("resources").jsonObject.mapValues { (_, resource) ->
            resource.jsonObject.mapValues { it.value.jsonPrimitive.content }
        }

    @Test
    fun annotationBundlesPreserveOwnedResourcesAndTypedMappings() = viewerTest {
        val viewer = editingViewer()
        val first = viewer.open(Fixture.bytes("report"))
        val second = viewer.open(Fixture.bytes("report"))
        val source = viewer.annotations(first)
        val target = viewer.annotations(second)
        val page = viewer.pages(first)[0].page
        val bytes = byteArrayOf(0, 255.toByte(), 128.toByte(), 0, 239.toByte())
        val file = source.drafts.createAttachment(AnnotationAttachmentFile(bytes, "résumé.bin"), Point(40.0, 40.0), page)
        val square = source.create("""{"subtype":"square","box":{"x":80,"y":40,"width":40,"height":30}}""", page)
        val bundle = source.export(refs = listOf(file, square))
        assertFalse(bundle.contains("\$host"))
        val resources = resources(obj(bundle))
        assertTrue(resources.values.any { resource -> resource["\$bytes"]?.let { Base64.getDecoder().decode(it).contentEquals(bytes) } == true })
        val copied = target.import(bundle, """{"pages":"by-position","attribution":"stamp"}""")
        assertEquals(2, copied.annotationInfos.size)
        assertTrue(copied.refMap.any { it.from == file })
        for (annotation in copied.annotationInfos) assertNotNull(target.get(annotation.annotationRef))
        val again = obj(target.export(refs = copied.annotationInfos.map { it.annotationRef }))
        assertEquals(resources, resources(again))
        source.selection.select(listOf(square))
        val result = source.selection.updateLink(PdfLinkTargetUri("https://example.test/selected"))
        assertTrue(result.failed.isEmpty())
        assertNotNull(source.threads.link(square))
        source.selection.updateLink(null)
        assertNull(source.threads.link(square))
        val saved = viewer.open(viewer.save(second))
        val reopened = viewer.annotations(saved)
        val persisted = obj(reopened.export(refs = copied.annotationInfos.map { it.annotationRef }))
        assertEquals(resources, resources(persisted))
        viewer.close(first)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { source.export() }
        assertNotNull(target.get(copied.annotationInfos[0].annotationRef))
        viewer.destroy()
    }

    @Test
    fun optionalEditingPluginsReportUnsupported() = viewerTest {
        val viewer = started(ViewerConfig())
        val document = viewer.open(Fixture.bytes("report"))
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.annotations(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.actions(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.commands() }
        viewer.destroy()
    }
}
