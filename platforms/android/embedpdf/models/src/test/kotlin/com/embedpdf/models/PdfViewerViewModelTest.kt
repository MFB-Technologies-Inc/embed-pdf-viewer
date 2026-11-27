package com.embedpdf.models

import android.app.Application
import android.content.pm.ActivityInfo
import androidx.activity.ComponentActivity
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.viewmodel.MutableCreationExtras
import androidx.test.core.app.ActivityScenario
import androidx.test.core.app.ApplicationProvider
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.Fixture
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ViewerConfig
import com.embedpdf.expectCode
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertSame
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Shadows.shadowOf

/** An activity holding a [PdfViewerViewModel], as an app's reader screen would. */
class HostActivity : ComponentActivity() {
    val holder: PdfViewerViewModel
        get() = ViewModelProvider(this, PdfViewerViewModel.factory(ViewerConfig()))[PdfViewerViewModel::class.java]
}

@RunWith(EmbedPdfTestRunner::class)
class PdfViewerViewModelTest {
    @get:Rule
    val main = MainThreadRule()

    @Before
    fun registerHostActivity() {
        val application = ApplicationProvider.getApplicationContext<Application>()
        shadowOf(application.packageManager).addOrUpdateActivity(
            ActivityInfo().apply {
                name = HostActivity::class.java.name
                packageName = application.packageName
            },
        )
    }

    /** Review focus: a recreated activity gets the same viewer, and clearing destroys it once. */
    @Test
    fun theViewerSurvivesRecreationAndIsDestroyedOnceWhenCleared() {
        var first: PdfViewerViewModel? = null
        var second: PdfViewerViewModel? = null
        val scenario = ActivityScenario.launch(HostActivity::class.java)
        scenario.onActivity { first = it.holder }
        val viewer = checkNotNull(first).viewer
        runBlocking {
            withTimeout(30_000) {
                assertSame(viewer, first!!.startedViewer())
                viewer.open(Fixture.bytes("report"))
            }
        }
        scenario.recreate()
        scenario.onActivity { second = it.holder }
        assertSame(first, second)
        assertSame(viewer, second!!.viewer)
        runBlocking { assertEquals(1, viewer.documentCount()) }
        scenario.close()
        val teardown = checkNotNull(first!!.teardown)
        runBlocking {
            withTimeout(30_000) { teardown.join() }
            expectCode(PdfException.Code.INSTANCE_CLOSED) { viewer.documentCount() }
        }
        // `onCleared` again starts nothing more.
        assertSame(teardown, first!!.teardown)
    }

    /** Clearing the view model while the viewer still starts tears it down cleanly. */
    @Test
    fun clearingWhileTheStartIsInFlightDestroysTheViewer() {
        val application = ApplicationProvider.getApplicationContext<Application>()
        val store = ViewModelStore()
        val extras = MutableCreationExtras().apply { set(ViewModelProvider.AndroidViewModelFactory.APPLICATION_KEY, application) }
        val model = ViewModelProvider(store, PdfViewerViewModel.factory(ViewerConfig()), extras)[PdfViewerViewModel::class.java]
        // The start began on creation and has not had a chance to finish.
        store.clear()
        val teardown = checkNotNull(model.teardown)
        runBlocking {
            withTimeout(30_000) {
                teardown.join()
                expectCode(PdfException.Code.INSTANCE_CLOSED) { model.startedViewer() }
                expectCode(PdfException.Code.INSTANCE_CLOSED) { model.viewer.documentCount() }
            }
        }
        assertFalse(teardown.isCancelled)
    }

    /** A start that finished before the clear does not hand out the destroyed viewer. */
    @Test
    fun clearingAfterTheStartFinishedAnswersInstanceClosed() {
        val application = ApplicationProvider.getApplicationContext<Application>()
        val store = ViewModelStore()
        val extras = MutableCreationExtras().apply { set(ViewModelProvider.AndroidViewModelFactory.APPLICATION_KEY, application) }
        val model = ViewModelProvider(store, PdfViewerViewModel.factory(ViewerConfig()), extras)[PdfViewerViewModel::class.java]
        runBlocking { withTimeout(30_000) { model.startedViewer() } }
        store.clear()
        runBlocking {
            withTimeout(30_000) {
                checkNotNull(model.teardown).join()
                expectCode(PdfException.Code.INSTANCE_CLOSED) { model.startedViewer() }
            }
        }
    }

    @Test
    fun startedViewerAnswersOnceTheViewerHasStarted() {
        val application = ApplicationProvider.getApplicationContext<Application>()
        val model = PdfViewerViewModel(application, ViewerConfig())
        val viewer: PdfViewer = runBlocking { withTimeout(30_000) { model.startedViewer() } }
        runBlocking { assertEquals(0, viewer.documentCount()) }
        viewer.close()
    }
}
