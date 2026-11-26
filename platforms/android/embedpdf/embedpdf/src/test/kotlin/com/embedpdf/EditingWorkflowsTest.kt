package com.embedpdf

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.boolean
import kotlinx.serialization.json.double
import kotlinx.serialization.json.jsonArray
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
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.form(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.actions(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.commands() }
        viewer.destroy()
    }

    private fun pageJson(page: PageRef): String = """{"kind":"objectNumber","objectNumber":${page.pageObjectNumber}}"""

    @Test
    fun independentWidgetEditsAndCalculationOrderPersist() = viewerTest {
        val viewer = editingViewer(ViewerConfig(interaction = true, annotation = "", form = FormConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        val page = viewer.pages(document)[0].page
        val first = form.create(FormCreateFieldInput(FormFieldFamily.TEXT, "first", page, Rect(20.0, 30.0, 100.0, 24.0)))
        val second = form.create(FormCreateFieldInput(FormFieldFamily.TEXT, "second", page, Rect(20.0, 70.0, 100.0, 24.0)))
        val bundle = obj(form.export(listOf(first.fieldRef, second.fieldRef)))
        val calculate = """{"calculate":{"root":{"type":"javascript","subtype":"JavaScript","script":"event.value = 1","next":[]},"incomplete":false,"warnings":[]}}"""
        val fields = bundle.getValue("fields").jsonArray.mapIndexed { index, field ->
            val data = field.jsonObject.getValue("data").jsonObject
            JsonObject(
                field.jsonObject + ("data" to JsonObject(data + mapOf("name" to JsonPrimitive("copy_$index"), "actions" to Json.parseToJsonElement(calculate)))),
            )
        }
        val refs = fields.map { it.getValue("data").jsonObject.getValue("ref") }
        val edited = JsonObject(bundle + mapOf("fields" to JsonArray(fields), "calculationOrder" to JsonArray(refs)))
        val imported = form.import(edited.toString())
        assertEquals(2, imported.fields.size)
        val a = imported.fields[0]
        val b = imported.fields[1]
        val widget = a.widgets[0].annotationRef!!
        val interaction = viewer.interaction(document)
        val annotations = viewer.annotations(document)
        interaction.activateTool(ToolId.FORM_EDIT)
        annotations.selection.select(listOf(widget))
        assertEquals(a.fieldRef, form.selectedField()?.fieldRef)
        annotations.selection.clear()
        val updated = form.updateWidget(widget, """{"rect":{"x":60,"y":90,"width":160,"height":28}}""")
        assertEquals(60.0, updated.bounds.x, 0.0)
        assertEquals(listOf(widget), form.widgets(a.fieldRef).map { it.annotationRef })
        val extra = form.addWidget(a.fieldRef, FormWidgetPlacement(page, Rect(20.0, 150.0, 100.0, 24.0)))
        val added = extra.field.widgets.last().annotationRef!!
        assertEquals(1, form.deleteWidget(added)?.widgets?.size)
        assertNull(form.widget(added))
        val order = form.reorderCalculations(listOf(b.fieldRef), FormCalculationPositionBefore(a.fieldRef))
        assertEquals(listOf<FormFieldRef?>(b.fieldRef, a.fieldRef), order)
        val saved = viewer.open(viewer.save(document))
        val reopened = viewer.form(saved)
        reopened.refresh()
        assertEquals(order, reopened.snapshot()?.calculationOrder)
        assertEquals(60.0, reopened.widget(widget)!!.bounds.x, 0.0)
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { form.selectedField() }
        expectCode(PdfException.Code.INSTANCE_CLOSED) { form.deleteWidget(widget) }
        viewer.destroy()
    }

    @Test
    fun directFormPayloadsAuthorOptionsScriptsAndPersistNullEdits() = viewerTest {
        val viewer = editingViewer(ViewerConfig(interaction = true, annotation = "", form = FormConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        form.refresh()
        val page = viewer.pages(document).first().page
        val action = """{"type":"javascript","script":"event.value = 7","next":[]}"""
        val field = form.create(
            """{"kind":"combobox","name":"direct_native","edit":true,"alternateName":"Preserved","mappingName":"Clear",""" +
                """"defaultValue":"a","options":[{"label":"Alpha","value":"a"},{"label":"Beta","value":"b"}],""" +
                """"actions":{"calculate":$action},"widgets":[{"page":${pageJson(page)},"rect":{"x":20,"y":30,"width":120,"height":24}}]}""",
        )
        val combobox = field.detail as FormFieldDetailCombobox
        assertTrue(combobox.edit)
        assertEquals(listOf("a", "b"), combobox.options.map { it.value })
        val updated = form.update(field.fieldRef, """{"mappingName":null,"edit":false,"actions":{"validate":$action}}""")
        assertEquals("Preserved", updated.alternateName)
        assertNull(updated.mappingName)
        val panel = form.fieldSettings(field.fieldRef)!!
        val choice = panel.properties.first { it.key == "defaultValue" }
        assertEquals(FormSettingControl.CHOICE, choice.control)
        assertEquals(false, choice.free)
        assertEquals(listOf("a", "b"), choice.options)
        assertEquals(false, obj(panel.valuesJson).getValue("edit").jsonPrimitive.boolean)
        val added = form.addWidget(field.fieldRef, """{"rect":{"x":20,"y":70,"width":120,"height":24},"fontSize":19,"actions":{"focus":$action}}""")
        assertEquals(19.0, obj(added.widgetJson).getValue("fontSize").jsonPrimitive.double, 0.0)
        val count = form.fields().size
        for (malformed in listOf("{", "[]", "null", "42")) {
            expectCode(PdfException.Code.INVALID_INPUT, malformed) { form.create(malformed) }
            expectCode(PdfException.Code.INVALID_INPUT, malformed) { form.update(field.fieldRef, malformed) }
            expectCode(PdfException.Code.INVALID_INPUT, malformed) { form.addWidget(field.fieldRef, malformed) }
        }
        assertEquals(count, form.fields().size)
        val saved = viewer.open(viewer.save(document))
        val reopened = viewer.form(saved)
        reopened.refresh()
        val found = reopened.field(field.fieldRef)!!
        assertEquals("Preserved", found.alternateName)
        assertNull(found.mappingName)
        val validate = obj(found.fieldsJson).getValue("actions").jsonObject.getValue("validate").jsonObject
        assertEquals("event.value = 7", validate.getValue("root").jsonObject.getValue("script").jsonPrimitive.content)
        assertEquals(2, reopened.widgets(found.fieldRef).size)
        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { form.fieldSettings(field.fieldRef) }
        viewer.destroy()
    }

    @Test
    fun directFormPayloadsRespectScriptPermission() = viewerTest {
        val viewer = started(ViewerConfig(scope = listOf("doc.open", "doc.forms.read", "doc.forms.modify"), interaction = true, form = FormConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val form = viewer.form(document)
        val actions = """{"calculate":{"type":"javascript","script":"event.value = 1"}}"""
        expectCode(PdfException.Code.PERMISSION_DENIED) { form.create("""{"kind":"text","name":"denied","widgets":[],"actions":$actions}""") }
        val missing = FormFieldRefFqn("missing")
        expectCode(PdfException.Code.PERMISSION_DENIED) { form.update(missing, """{"actions":$actions}""") }
        expectCode(PdfException.Code.PERMISSION_DENIED) { form.addWidget(missing, """{"actions":$actions}""") }
        viewer.destroy()
    }
}
