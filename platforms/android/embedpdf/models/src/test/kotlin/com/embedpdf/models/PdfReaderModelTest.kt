package com.embedpdf.models

import com.embedpdf.DocumentStatus
import com.embedpdf.Fixture
import com.embedpdf.PdfException
import com.embedpdf.expectCode
import com.embedpdf.interaction
import com.embedpdf.link
import com.embedpdf.metadata
import kotlinx.coroutines.launch
import kotlinx.coroutines.yield
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNotSame
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Rule
import org.junit.Test

class PdfReaderModelTest {
    @get:Rule
    val main = MainThreadRule()

    private fun session(model: PdfReaderModel): PdfReaderModel.Session =
        (model.phase.value as? PdfReaderModel.Phase.Ready)?.session ?: run {
            fail("expected Ready, got ${model.phase.value}")
            throw AssertionError()
        }

    /** Returns once an open started in another coroutine has set Loading, which it does before its first suspension. */
    private suspend fun untilLoading(model: PdfReaderModel) {
        repeat(1000) {
            if (model.phase.value == PdfReaderModel.Phase.Loading) return
            yield()
        }
        fail("expected Loading, got ${model.phase.value}")
    }

    @Test
    fun opensToReady() = mainTest {
        val model = PdfReaderModel()
        assertEquals(PdfReaderModel.Phase.Idle, model.phase.value)
        model.open(Fixture.bytes("report"), name = "report.pdf")
        val session = session(model)
        assertEquals(session.viewer.pages(session.document), session.pages)
        assertTrue(session.pages.isNotEmpty())
        assertEquals(DocumentStatus.READY, session.document.status)
        assertNotNull(session.search)
        assertNotNull(session.selection)
        assertNotNull(session.tools)
        model.close()
        assertEquals(PdfReaderModel.Phase.Idle, model.phase.value)
        assertNull(model.viewer)
    }

    @Test
    fun opensAFileUrl() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        model.open(Fixture.file("report").toURI().toURL())
        assertTrue(session(model).pages.isNotEmpty())
        model.close()
    }

    @Test
    fun nonPdfBytesFail() = mainTest {
        val model = PdfReaderModel()
        model.open("no".toByteArray())
        assertTrue("${model.phase.value}", model.phase.value is PdfReaderModel.Phase.Failed)
        // The failed tab the engine leaves behind is closed.
        assertEquals(0, model.viewer?.documentCount())
        // A failure does not end the model: the next open works.
        model.open(Fixture.bytes("report"))
        session(model)
        model.close()
    }

    @Test
    fun anEncryptedDocumentLocksThenUnlocks() = mainTest {
        val model = PdfReaderModel()
        model.open(Fixture.bytes("encrypted-hello"))
        val locked = model.phase.value as? PdfReaderModel.Phase.Locked ?: run { fail("expected Locked, got ${model.phase.value}"); return@mainTest }
        assertEquals(DocumentStatus.LOCKED, locked.document.status)
        assertNull(model.unlockError.value)
        model.unlock("wrong")
        assertTrue("${model.phase.value}", model.phase.value is PdfReaderModel.Phase.Locked)
        assertEquals(PdfException.Code.PERMISSION_DENIED, model.unlockError.value?.code)
        model.unlock("hôtel")
        val session = session(model)
        assertEquals(locked.document.id, session.document.id)
        assertEquals(DocumentStatus.READY, session.document.status)
        assertTrue(session.pages.isNotEmpty())
        assertNull(model.unlockError.value)
        model.close()
    }

    @Test
    fun anEncryptedDocumentOpensWithItsPassword() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        model.open(Fixture.bytes("encrypted-hello"), password = "hôtel")
        session(model)
        model.close()
    }

    @Test
    fun unlockDoesNothingUnlessLocked() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        model.unlock("hôtel")
        assertEquals(PdfReaderModel.Phase.Idle, model.phase.value)
        model.open(Fixture.bytes("report"))
        val before = session(model)
        model.unlock("hôtel")
        assertEquals(before.document, session(model).document)
        model.close()
    }

    @Test
    fun featuresPickThePlugins() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        model.open(Fixture.bytes("report"))
        val session = session(model)
        assertNull(session.search)
        assertNull(session.selection)
        assertNull(session.tools)
        expectCode(PdfException.Code.UNSUPPORTED) { session.viewer.interaction(session.document) }
        model.close()
    }

    @Test
    fun eachFeatureBringsItsOwnModel() = mainTest {
        val model = PdfReaderModel(features = setOf(PdfReaderModel.Feature.SEARCH, PdfReaderModel.Feature.LINKS))
        model.open(Fixture.bytes("report"))
        val session = session(model)
        assertNotNull(session.search)
        assertNull(session.selection)
        assertNull(session.tools)
        session.viewer.link(session.document)
        model.close()
    }

    @Test
    fun openingASecondDocumentClosesTheFirst() = mainTest {
        val model = PdfReaderModel()
        model.open(Fixture.bytes("report"))
        val first = session(model)
        model.open(Fixture.bytes("features"))
        val second = session(model)
        assertSame(first.viewer, second.viewer)
        assertNotEquals(first.document.id, second.document.id)
        assertEquals(1, second.viewer.documentCount())
        assertNotSame(first.reader, second.reader)
        model.close()
    }

    @Test
    fun anOpenSupersededByASecondOpenLeavesTheSecond() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        val report = Fixture.bytes("report")
        val features = Fixture.bytes("features")
        val first = launch { model.open(report, name = "first") }
        untilLoading(model)
        model.open(features, name = "second")
        first.join()
        val session = session(model)
        assertEquals("second", session.document.name)
        assertEquals(1, session.viewer.documentCount())
        model.close()
    }

    @Test
    fun anOpenSupersededByCloseEndsIdle() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        val opening = launch { model.open(Fixture.bytes("report")) }
        untilLoading(model)
        model.close()
        opening.join()
        assertEquals(PdfReaderModel.Phase.Idle, model.phase.value)
        assertNull(model.viewer)
    }

    @Test
    fun openShowsLoadingUntilItLands() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        val opening = launch { model.open(Fixture.bytes("report")) }
        untilLoading(model)
        opening.join()
        session(model)
        model.close()
    }

    @Test
    fun configureAddsAPlugin() = mainTest {
        val model = PdfReaderModel(configure = { it.metadata() })
        model.open(Fixture.bytes("report"), name = "report.pdf")
        val session = session(model)
        session.viewer.metadata(session.document)
        // The features' own plugins are still there.
        assertNotNull(session.search)
        model.close()
    }

    @Test
    fun theDefaultModelLeavesOutTheMetadataPlugin() = mainTest {
        val model = PdfReaderModel()
        model.open(Fixture.bytes("report"), name = "report.pdf")
        val session = session(model)
        expectCode(PdfException.Code.UNSUPPORTED) { session.viewer.metadata(session.document) }
        model.close()
    }

    @Test
    fun configureAppliesToEachViewerTheModelStarts() = mainTest {
        var calls = 0
        val model = PdfReaderModel(
            configure = {
                calls += 1
                it.metadata()
            },
        )
        model.open(Fixture.bytes("report"))
        model.close()
        model.open(Fixture.bytes("report"))
        val session = session(model)
        session.viewer.metadata(session.document)
        assertEquals(2, calls)
        model.close()
    }

    @Test
    fun turningOffAFeaturesPluginFailsTheOpen() = mainTest {
        val model = PdfReaderModel(configure = { it.selection(false) })
        model.open(Fixture.bytes("report"))
        val failed = model.phase.value as? PdfReaderModel.Phase.Failed ?: run { fail("expected Failed, got ${model.phase.value}"); return@mainTest }
        assertEquals(PdfException.Code.UNSUPPORTED, failed.error.code)
        // Nothing is left open, and the model is not stuck.
        assertEquals(0, model.viewer?.documentCount())
        model.close()
    }

    @Test
    fun aCancelledOpenLeavesTheNextOpenWorking() = mainTest {
        val model = PdfReaderModel(features = emptySet())
        val opening = launch { model.open(Fixture.bytes("report")) }
        untilLoading(model)
        opening.cancel()
        opening.join()
        model.open(Fixture.bytes("features"), name = "next")
        assertEquals("next", session(model).document.name)
        model.close()
    }
}
