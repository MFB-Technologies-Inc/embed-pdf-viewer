package com.embedpdf.models

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import com.embedpdf.DocumentFetcher
import com.embedpdf.HttpDocumentFetcher
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.ViewerConfig
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Deferred
import kotlinx.coroutines.DelicateCoroutinesApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.GlobalScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Holds one [PdfViewer] across configuration changes. The viewer is made
 * and started when the view model is created, and destroyed once when the
 * view model is cleared, so a rotation keeps the open documents and the
 * models over them.
 *
 * ```kotlin
 * private val holder: PdfViewerViewModel by viewModels {
 *     PdfViewerViewModel.factory(ViewerConfig(render = RenderConfig(), stage = StageConfig()))
 * }
 * ```
 *
 * Read [viewer] at once to subscribe to it, and await [startedViewer]
 * before the first call that needs a started viewer, such as an open.
 */
public class PdfViewerViewModel(
    application: Application,
    config: ViewerConfig,
    documentFetcher: DocumentFetcher = HttpDocumentFetcher(),
) : AndroidViewModel(application) {
    /** The viewer this view model holds; destroyed when it is cleared. */
    public val viewer: PdfViewer = PdfViewer(config, documentFetcher)

    private val start: Deferred<Unit> =
        viewModelScope.async(start = CoroutineStart.UNDISPATCHED) { viewer.start() }
    private val cleared = AtomicBoolean(false)

    /** The teardown [onCleared] started. Tests await it. */
    internal var teardown: Job? = null
        private set

    /**
     * The viewer once it has started. Throws the start's [PdfException] when
     * it failed, and `instance-closed` once the view model is cleared, whether
     * or not the start had finished.
     */
    public suspend fun startedViewer(): PdfViewer {
        try {
            start.await()
        } catch (e: kotlinx.coroutines.CancellationException) {
            if (start.isCancelled && cleared.get()) throw clearedException(e)
            throw e
        }
        if (cleared.get()) throw clearedException(null)
        return viewer
    }

    private fun clearedException(cause: Throwable?) =
        PdfException(PdfException.Code.INSTANCE_CLOSED, "The view model was cleared.", "Closed", cause)

    /**
     * Destroys the viewer. The view model's scope has already been cancelled
     * when this runs, so the destroy runs on its own and nothing waits for
     * it; the native handle is released after it either way.
     */
    @OptIn(DelicateCoroutinesApi::class)
    override fun onCleared() {
        if (!cleared.compareAndSet(false, true)) return
        teardown = GlobalScope.launch(Dispatchers.IO) {
            try {
                viewer.destroy()
            } catch (e: PdfException) {
                // Already destroyed by the app: the teardown already ran.
            } finally {
                viewer.close()
            }
        }
    }

    public companion object {
        /** A factory for `by viewModels { ... }` that makes the view model with [config]. */
        public fun factory(
            config: ViewerConfig,
            documentFetcher: DocumentFetcher = HttpDocumentFetcher(),
        ): ViewModelProvider.Factory = viewModelFactory {
            initializer {
                val application = checkNotNull(this[ViewModelProvider.AndroidViewModelFactory.APPLICATION_KEY]) {
                    "PdfViewerViewModel needs the Application in its creation extras"
                }
                PdfViewerViewModel(application, config, documentFetcher)
            }
        }
    }
}
