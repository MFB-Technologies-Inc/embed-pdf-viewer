package com.embedpdf

import kotlinx.coroutines.delay
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.concurrent.atomic.AtomicInteger

/** Answers every prompt with `tiny.png`, and counts the prompts. */
class TinyPngPicker(private val png: ByteArray) : AnnotationFilePicker {
    val prompts = AtomicInteger(0)

    override suspend fun pick(request: AnnotationFilePromptRequest): AnnotationPickedFile? {
        prompts.incrementAndGet()
        return AnnotationPickedFile(png, "tiny.png", "image/png")
    }
}

class AnnotationTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val annotations: AnnotationCapability)

    companion object {
        const val SQUARE = """{"subtype": "square", "box": {"x": 72, "y": 72, "width": 100, "height": 50}}"""
        const val SMALL_SQUARE = """{"subtype": "square", "box": {"x": 0, "y": 0, "width": 10, "height": 10}}"""

        suspend fun opened(fixture: String = "annotation_kinds", config: ViewerConfig = ViewerConfig(interaction = true, annotation = "")): Opened {
            val viewer = started(config)
            val document = viewer.open(Fixture.bytes(fixture))
            val annotations = viewer.annotations(document)
            annotations.refresh()
            return Opened(viewer, document, annotations)
        }

        fun tinyPng(): ByteArray = Fixture.bytes("tiny", "png")
    }

    private suspend fun firstPage(viewer: PdfViewer, document: DocumentInfo): PageRef = viewer.pages(document)[0].page

    @Test
    fun loadingListsTheFixturesAnnotations() = viewerTest {
        val (viewer, _, annotations) = opened()
        // `refresh` re-reads every annotation and waits for it; `whenSynced`
        // waits for any page re-read still in flight.
        annotations.whenSynced()
        assertEquals(ResourceStatus.READY, annotations.status())
        assertTrue(annotations.list().isNotEmpty())
        viewer.destroy()
    }

    @Test
    fun createUpdateDeleteIsObserved() = viewerTest {
        val (viewer, document, annotations) = opened("report")
        val page = firstPage(viewer, document)
        val events = subscribed(annotations.observeEvents())
        val created = annotations.create(SQUARE, page)
        annotations.update(created, """{"contents": "From Kotlin"}""")
        assertEquals("From Kotlin", annotations.get(created)?.contents)
        annotations.delete(created)
        val createdEvent = events.nextMapped { it as? AnnotationEventCreated }
        assertEquals(created, createdEvent.event.annotationInfo.annotationRef)
        assertEquals(AnnotationSubtype.SQUARE, createdEvent.event.annotationInfo.subtype)
        val deleted = events.nextMapped { it as? AnnotationEventDeleted }
        assertEquals(listOf(created), deleted.event.refs)
        viewer.destroy()
    }

    @Test
    fun aStampFromPngBytesIsCreated() = viewerTest {
        val (viewer, document, annotations) = opened("report")
        val page = firstPage(viewer, document)
        val stamp = annotations.drafts.placeStamp(
            AnnotationStampSource(tinyPng(), mimeType = "image/png"),
            AnnotationStampPlacement(page, Point(200.0, 200.0)),
            targetWidth = 80.0,
        )
        assertEquals(AnnotationSubtype.STAMP, annotations.get(stamp)?.subtype)
        viewer.destroy()
    }

    @Test
    fun aKotlinFilePickerSuppliesARequestedStamp() = viewerTest {
        val (viewer, document, annotations) = opened("report")
        val page = firstPage(viewer, document)
        val picker = TinyPngPicker(tinyPng())
        annotations.setFilePicker(picker)
        viewer.interaction(document).activateTool(ToolId.STAMP)
        assertTrue(annotations.drafts.requestStamp(Point(200.0, 200.0), page))
        // The pick answers off the kernel thread and the stamp is placed
        // after it. A placed stamp is selected, not announced as created, so
        // poll.
        var subtypes = emptyList<AnnotationSubtype>()
        withTimeout(5_000) {
            while (subtypes.isEmpty()) {
                delay(20)
                subtypes = annotations.list().map { it.subtype }
            }
        }
        assertEquals(listOf(AnnotationSubtype.STAMP), subtypes)
        assertEquals(1, picker.prompts.get())
        annotations.setFilePicker(null)
        viewer.destroy()
    }

    @Test
    fun selectingAndClearing() = viewerTest {
        val (viewer, _, annotations) = opened()
        val first = annotations.list().first()
        annotations.selection.select(listOf(first.annotationRef))
        assertEquals(listOf(first.annotationRef), annotations.selection.refs())
        assertEquals(listOf(first), annotations.selected())
        annotations.selection.clear()
        assertEquals(emptyList<AnnotationRef>(), annotations.selection.refs())
        viewer.destroy()
    }

    @Test
    fun createFromSelectionMarksTheSelectedText() = viewerTest {
        val (annotations, selection, page) = selectedText()
        selection.selectPage(page)
        val refs = annotations.drafts.createFromSelection(ToolId.HIGHLIGHT)
        assertTrue(refs.isNotEmpty())
        assertEquals(AnnotationSubtype.HIGHLIGHT, annotations.get(refs.first())?.subtype)
        annotations.viewer.destroy()
    }

    @Test
    fun createLeftOutDoesNotSelect() = viewerTest {
        val (viewer, document, annotations) = opened("report")
        val page = firstPage(viewer, document)
        annotations.create(SQUARE, page)
        val omitted = annotations.selection.refs()
        annotations.create(SQUARE, page, select = false)
        val unselected = annotations.selection.refs()
        val selected = annotations.create(SQUARE, page, select = true)
        val chosen = annotations.selection.refs()
        assertEquals(emptyList<AnnotationRef>(), omitted)
        assertEquals(emptyList<AnnotationRef>(), unselected)
        assertEquals(listOf(selected), chosen)
        viewer.destroy()
    }

    private suspend fun selectedText(): Triple<AnnotationCapability, SelectionCapability, PageRef> {
        val (viewer, document, annotations) = opened("report", ViewerConfig(interaction = true, selection = true, annotation = ""))
        val page = firstPage(viewer, document)
        val selection = viewer.selection(document)
        // The page's text geometry loads on demand; without it the selection
        // has no segments to mark.
        selection.ensureLoaded(page)
        return Triple(annotations, selection, page)
    }

    @Test
    fun createFromSelectionLeftOutClearsTheTextSelection() = viewerTest {
        val (annotations, selection, page) = selectedText()
        selection.selectPage(page)
        assertTrue(annotations.drafts.createFromSelection(ToolId.HIGHLIGHT).isNotEmpty())
        val omitted = selection.hasSelection()
        selection.selectPage(page)
        annotations.drafts.createFromSelection(ToolId.HIGHLIGHT, clear = true)
        val cleared = selection.hasSelection()
        selection.selectPage(page)
        annotations.drafts.createFromSelection(ToolId.HIGHLIGHT, clear = false)
        val kept = selection.hasSelection()
        assertEquals(listOf(false, false, true), listOf(omitted, cleared, kept))
        annotations.viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, annotations) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { annotations.list() }
        assertEquals("PluginError", error.name)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { annotations.tools.tools() }
        viewer.destroy()
    }

    @Test
    fun aTextSelectionBelowZeroOrReversedThrowsInsteadOfWrapping() = viewerTest {
        val (viewer, _, annotations) = opened()
        val ref = annotations.list().first().annotationRef
        for (range in listOf(-1 until 3, 5..2)) {
            expectCode(PdfException.Code.INVALID_INPUT, "$range") { annotations.drafts.setTextSelection(range, ref) }
        }
        viewer.destroy()
    }

    /** A sub-capability held on its own keeps answering while its viewer lives. */
    @Test
    fun aSubCapabilityHeldAloneAnswers() = viewerTest {
        val tools = opened().annotations.tools
        val listed = tools.tools()
        assertTrue(listed.isNotEmpty())
        assertEquals(ToolId.HIGHLIGHT, listed.first { it.toolId == ToolId.HIGHLIGHT }.toolId)
        tools.viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, annotations) = opened()
        val collector = collecting(annotations.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        annotations.refresh()
        viewer.destroy()
    }

    @Test
    fun theFlowCompletesWhenTheDocumentCloses() = viewerTest {
        val (viewer, document, annotations) = opened()
        val collector = collecting(annotations.observeEvents())
        viewer.close(document)
        assertTrue(finishes(collector))
        viewer.destroy()
    }

    @Test
    fun annotationSettingsMergeAndReset() = viewerTest {
        val (viewer, _, annotations) = opened()
        val registered = annotations.settings()
        annotations.updateSettings("""{"editable": false}""")
        assertTrue(annotations.settings().replace(" ", "").contains("\"editable\":false"))
        annotations.resetSettings()
        assertEquals(registered, annotations.settings())
        viewer.destroy()
    }

    @Test
    fun theAuthorityQuestionsAnswer() = viewerTest {
        val (viewer, _, annotations) = opened()
        val first = annotations.list().first()
        assertTrue(annotations.canRead())
        assertTrue(annotations.canCreate())
        assertEquals(first.canUpdate, annotations.canUpdate(first.annotationRef))
        assertEquals(first.canDelete, annotations.canDelete(first.annotationRef))
        assertFalse(annotations.isPending(first.annotationRef))
        viewer.destroy()
    }

    @Test
    fun aHitTestAnswersTheRecordAGetDoes() = viewerTest {
        // As Swift's test, this pins the typing, not what is hit: the
        // plugin's hit regions come from the page geometry its views load.
        val (viewer, _, annotations) = opened()
        val first = annotations.list().first()
        val center = Point(first.rect.x + first.rect.width / 2, first.rect.y + first.rect.height / 2)
        annotations.hitTest(center, first.page)?.let { hit -> assertEquals(annotations.get(hit.annotationRef), hit) }
        viewer.destroy()
    }

    // One per member that takes a `CancelToken`, beyond `refresh` and
    // `create`. Each is called in an already-cancelled coroutine.

    /** The fixture's first annotation and its page. */
    private suspend fun cancellable(): Triple<AnnotationCapability, AnnotationRef, PageRef> {
        val annotations = opened().annotations
        val first = annotations.list().first()
        return Triple(annotations, first.annotationRef, first.page)
    }

    @Test
    fun refreshCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (annotations, _, _) = cancellable()
        expectCancelledBeforeItStarts { annotations.refresh() }
        annotations.list()
        annotations.viewer.destroy()
    }

    @Test
    fun whenSyncedCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (annotations, _, _) = cancellable()
        expectCancelledBeforeItStarts { annotations.whenSynced() }
        annotations.viewer.destroy()
    }

    @Test
    fun createCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (annotations, _, page) = cancellable()
        val before = annotations.list().size
        expectCancelledBeforeItStarts { annotations.create(SMALL_SQUARE, page) }
        assertEquals(before, annotations.list().size)
        annotations.viewer.destroy()
    }

    @Test
    fun updateLeavesTheRecordWhenCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        val before = annotations.get(ref)
        expectCancelledBeforeItStarts { annotations.update(ref, """{"contents": "x"}""") }
        expectCancelledBeforeItStarts { annotations.update(ref, """{"rotation": 90}""") }
        assertEquals(before, annotations.get(ref))
        annotations.viewer.destroy()
    }

    @Test
    fun reorderCancelsWhenItsCoroutineIsCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        expectCancelledBeforeItStarts { annotations.reorder(listOf(ref), AnnotationPosition.End) }
        annotations.viewer.destroy()
    }

    @Test
    fun exportAndImportCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        val bundle = annotations.export(refs = listOf(ref))
        expectCancelledBeforeItStarts { annotations.export() }
        val before = annotations.list().size
        expectCancelledBeforeItStarts { annotations.import(bundle) }
        assertEquals(before, annotations.list().size)
        annotations.viewer.destroy()
    }

    @Test
    fun deleteLeavesTheAnnotationAndSelectionWhenCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        annotations.selection.select(listOf(ref))
        expectCancelledBeforeItStarts { annotations.delete(ref) }
        assertNotNull(annotations.get(ref))
        assertEquals(listOf(ref), annotations.selection.refs())
        annotations.viewer.destroy()
    }

    @Test
    fun selectionChangesCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        val before = annotations.get(ref)
        val selection = annotations.selection
        selection.select(listOf(ref))
        expectCancelledBeforeItStarts { selection.update("""{"opacity": 0.5}""") }
        expectCancelledBeforeItStarts { selection.update("""{"hidden": true}""") }
        expectCancelledBeforeItStarts { selection.rotate(90.0) }
        expectCancelledBeforeItStarts { selection.resetRotation() }
        expectCancelledBeforeItStarts { selection.toggle(AnnotationTextFormat.BOLD) }
        expectCancelledBeforeItStarts { selection.group() }
        expectCancelledBeforeItStarts { selection.ungroup() }
        expectCancelledBeforeItStarts { selection.updateLink(null) }
        expectCancelledBeforeItStarts { selection.delete() }
        assertEquals(before, annotations.get(ref))
        assertEquals(listOf(ref), selection.refs())
        annotations.viewer.destroy()
    }

    @Test
    fun draftChangesCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (annotations, ref, page) = cancellable()
        val drafts = annotations.drafts
        val source = AnnotationStampSource(tinyPng(), mimeType = "image/png")
        expectCancelledBeforeItStarts { drafts.finishCreationDraft() }
        expectCancelledBeforeItStarts { drafts.endTextEdit() }
        expectCancelledBeforeItStarts { drafts.createFromSelection(ToolId.HIGHLIGHT) }
        expectCancelledBeforeItStarts { drafts.armStamp(source) }
        assertFalse(drafts.hasArmedStamp())
        expectCancelledBeforeItStarts { drafts.placeStamp(source, AnnotationStampPlacement(page, Point(50.0, 50.0))) }
        expectCancelledBeforeItStarts { drafts.createAttachment(AnnotationAttachmentFile(tinyPng(), "tiny.png"), Point(50.0, 50.0), page) }
        expectCancelledBeforeItStarts { drafts.readAttachment(ref) }
        annotations.viewer.destroy()
    }

    @Test
    fun threadChangesCancelWhenTheirCoroutineIsCancelled() = viewerTest {
        val (annotations, ref, _) = cancellable()
        val threads = annotations.threads
        expectCancelledBeforeItStarts { threads.reply(ref, "x") }
        expectCancelledBeforeItStarts { threads.setText("x", ref) }
        expectCancelledBeforeItStarts { threads.setStatus(AnnotationThreadStatus.ACCEPTED, ref) }
        expectCancelledBeforeItStarts { threads.setMarked(true, ref) }
        expectCancelledBeforeItStarts { threads.delete(ref) }
        expectCancelledBeforeItStarts { threads.deleteThread(ref) }
        expectCancelledBeforeItStarts { threads.setLink("""{"kind": "uri", "uri": "https://example.invalid"}""", ref) }
        expectCancelledBeforeItStarts { threads.setLink(AnnotationLinkTarget.Uri("https://example.invalid"), ref) }
        expectCancelledBeforeItStarts { threads.clearLink(ref) }
        assertNotNull(annotations.get(ref))
        annotations.viewer.destroy()
    }
}
