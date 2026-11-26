package com.embedpdf.models

import com.embedpdf.AnnotationCapability
import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ViewerConfig
import com.embedpdf.annotations
import com.embedpdf.started
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class AnnotationModelTest {
    @get:Rule
    val main = MainThreadRule()

    private data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val annotations: AnnotationCapability)

    private suspend fun opened(scope: List<String>? = null): Opened {
        val viewer = started(ViewerConfig(scope = scope, interaction = true, annotation = ""))
        val document = viewer.open(Fixture.bytes("report"))
        return Opened(viewer, document, viewer.annotations(document))
    }

    @Test
    fun aNewModelAllowsNothing() = mainTest {
        val (viewer, _, annotations) = opened()
        val model = AnnotationModel(annotations)
        assertFalse(model.canRead.value)
        assertFalse(model.canCreate.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun theObservingModelFollowsTheKernelUntilTheDocumentCloses() = mainTest {
        val (viewer, document, annotations) = opened()
        val model = AnnotationModel(annotations)
        val observing = launch { model.run() }
        expectObserved(model.canCreate, true)
        assertEquals(annotations.canRead(), model.canRead.value)
        assertEquals(annotations.canCreate(), model.canCreate.value)
        expectObserved(model.canRead, false) { viewer.close(document) }
        assertTrue(ends(observing))
        assertFalse(model.canCreate.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        viewer.destroy()
    }

    @Test
    fun aRefreshNotifiesCollectorsOfEachProperty() = mainTest {
        val (viewer, _, annotations) = opened()
        val model = AnnotationModel(annotations)
        assertTrue(changes(model.canRead) { model.refresh() })
        val other = AnnotationModel(annotations)
        assertTrue(changes(other.canCreate) { other.refresh() })
        viewer.destroy()
    }

    @Test
    fun aScopeWithoutAnnotationsReadsFalse() = mainTest {
        val (viewer, _, annotations) = opened(scope = listOf("doc.open", "doc.render"))
        val model = AnnotationModel(annotations)
        model.refresh()
        assertEquals(annotations.canRead(), model.canRead.value)
        assertFalse(model.canRead.value)
        assertFalse(model.canCreate.value)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun anOlderReadCannotOverwriteANewerOne() = mainTest {
        val (viewer, document, annotations) = opened()
        val model = AnnotationModel(annotations)
        val (reached, release) = holdingFirstRead(model.snapshots)
        val stale = async { model.refresh() }
        reached.await()
        viewer.close(document)
        model.refresh()
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        release.complete(Unit)
        stale.await()
        assertFalse(model.canRead.value)
        assertFalse(model.canCreate.value)
        assertEquals(PdfException.Code.INSTANCE_CLOSED, model.error.value?.code)
        viewer.destroy()
    }
}
