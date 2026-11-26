package com.embedpdf.demo

import android.app.Application
import android.net.Uri
import android.provider.OpenableColumns
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.embedpdf.CustomMetadata
import com.embedpdf.DocumentMetadata
import com.embedpdf.LinkActivation
import com.embedpdf.NoteAnnotationDraft
import com.embedpdf.Nullable
import com.embedpdf.PdfException
import com.embedpdf.Rect
import com.embedpdf.annotations
import com.embedpdf.metadata
import com.embedpdf.models.PdfReaderModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.DelicateCoroutinesApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.GlobalScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.IOException

/**
 * The demo's one screen: the reader, the document's metadata sheet and the
 * note button. A view model, so a rotation keeps the open document.
 */
class DocumentViewModel(application: Application) : AndroidViewModel(application) {
    /** What the metadata sheet shows. */
    class MetadataSheet(val metadata: DocumentMetadata, val custom: CustomMetadata)

    /**
     * Opens, unlocks and shows the document; `PdfReader` draws its phase and
     * its panels. The plugins the reader's features leave out are for the
     * metadata sheet and the bookmark, attachment and history panels.
     */
    val reader = PdfReaderModel(
        configure = { it.metadata().history().outline().attachment() },
        actionScope = viewModelScope,
    )

    private val _metadataSheet = MutableStateFlow<MetadataSheet?>(null)
    private val _error = MutableStateFlow<String?>(null)
    private val _canAddNote = MutableStateFlow(false)
    private val _linkActivation = MutableStateFlow<LinkActivation?>(null)

    val metadataSheet: StateFlow<MetadataSheet?> = _metadataSheet.asStateFlow()

    /** The detail of the last failure, for the alert. */
    val error: StateFlow<String?> = _error.asStateFlow()

    /** Whether the open document takes a new annotation. */
    val canAddNote: StateFlow<Boolean> = _canAddNote.asStateFlow()

    /** The last link activation the reader reported. */
    val linkActivation: StateFlow<LinkActivation?> = _linkActivation.asStateFlow()

    private val session: PdfReaderModel.Session?
        get() = (reader.phase.value as? PdfReaderModel.Phase.Ready)?.session

    init {
        viewModelScope.launch { openBundled("report") }
        // Each ready document starts with no link shown and asks whether it takes notes.
        viewModelScope.launch {
            reader.phase.collectLatest { phase ->
                _linkActivation.value = null
                _canAddNote.value = false
                if (phase is PdfReaderModel.Phase.Ready) refreshCanAddNote(phase.session)
            }
        }
    }

    fun sampleSelected(name: String) {
        viewModelScope.launch { openBundled(name) }
    }

    /** Opens the document the system picker returned; `null` when it was cancelled. */
    fun fileOpened(uri: Uri?) {
        uri ?: return
        viewModelScope.launch {
            val resolver = getApplication<Application>().contentResolver
            val opened = try {
                withContext(Dispatchers.IO) {
                    val name = resolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use { cursor ->
                        if (cursor.moveToFirst()) cursor.getString(0) else null
                    }
                    val bytes = resolver.openInputStream(uri)?.use { it.readBytes() } ?: throw IOException("Nothing to read at $uri")
                    bytes to name
                }
            } catch (e: IOException) {
                _error.value = e.message
                return@launch
            } catch (e: SecurityException) {
                _error.value = e.message
                return@launch
            }
            reader.open(opened.first, name = opened.second)
        }
    }

    fun infoSelected() {
        val session = session ?: return
        viewModelScope.launch {
            reporting {
                val metadata = session.viewer.metadata(session.document)
                metadata.refresh()
                metadata.refreshCustom()
                val snapshot = metadata.snapshot() ?: return@reporting
                _metadataSheet.value = MetadataSheet(snapshot, metadata.customSnapshot() ?: emptyMap())
            }
        }
    }

    fun metadataDismissed() {
        _metadataSheet.value = null
    }

    fun addNoteSelected() {
        val session = session ?: return
        val page = session.pages.firstOrNull()?.page ?: return
        val contents = getApplication<Application>().getString(R.string.note_contents)
        viewModelScope.launch {
            reporting {
                session.viewer.annotations(session.document).create(
                    NoteAnnotationDraft(rect = Rect(x = 72.0, y = 72.0, width = 24.0, height = 24.0), contents = Nullable.Value(contents)),
                    page,
                )
            }
            refreshCanAddNote(session)
        }
    }

    fun linkActivated(activation: LinkActivation) {
        _linkActivation.value = activation
    }

    /** A failure the reader reported, with the engine's detail. */
    fun readerFailed(error: PdfException) {
        _error.value = error.detail
    }

    fun errorDismissed() {
        _error.value = null
    }

    private suspend fun refreshCanAddNote(session: PdfReaderModel.Session) {
        val allowed = try {
            session.viewer.annotations(session.document).canCreate()
        } catch (e: PdfException) {
            false
        }
        if (this.session?.document?.id == session.document.id) _canAddNote.value = allowed
    }

    private suspend fun openBundled(name: String) {
        val bytes = try {
            withContext(Dispatchers.IO) { getApplication<Application>().assets.open("$name.pdf").use { it.readBytes() } }
        } catch (e: IOException) {
            _error.value = e.message
            return
        }
        reader.open(bytes, name = "$name.pdf")
    }

    private suspend fun reporting(block: suspend () -> Unit) {
        try {
            block()
        } catch (e: CancellationException) {
            throw e
        } catch (e: PdfException) {
            _error.value = e.detail
        }
    }

    /**
     * The view model's scope is already cancelled here, so the close runs on
     * its own; it destroys the viewer the reader started.
     */
    @OptIn(DelicateCoroutinesApi::class)
    override fun onCleared() {
        GlobalScope.launch(Dispatchers.Main) { reader.close() }
    }
}
