package com.embedpdf.compose

import android.content.ContentResolver
import android.content.Context
import android.net.Uri
import android.provider.DocumentsContract
import android.text.format.Formatter
import androidx.activity.compose.ManagedActivityResultLauncher
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.selection.selectable
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.Stable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.snapshotFlow
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.embedpdf.AttachedFile
import com.embedpdf.AttachmentContent
import com.embedpdf.AttachmentHomePage
import com.embedpdf.ResourceStatus
import com.embedpdf.models.AttachmentModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.io.FileNotFoundException
import java.io.IOException

/**
 * A document's attached files over an [AttachmentModel]: a header with
 * reload and import buttons, one row per file, and buttons to save a copy
 * of the selected file or remove it.
 *
 * Tapping a row selects its file; TalkBack reads the row's name, its
 * description and size, and whether it is selected. A file whose bytes are
 * still loading shows a progress indicator, and a file that lives in a page
 * annotation has a button that shows it on its page.
 *
 * Import opens the system's document picker for any number of files and
 * attaches them through [AttachmentModel.importFiles]. "Save copy…"
 * downloads the selected file, then opens the system's create-document
 * picker named after it and writes the bytes there
 * ([AttachmentExportDocument]). The downloaded bytes wait in a file in the
 * app's cache directory until the picker answers, so a copy whose picker
 * outlives the activity, as across a rotation, is still written; the file is
 * deleted once the picker answers. A failure to write lands in the model's
 * error, and a created document whose copy is gone is deleted again.
 * "Remove…" asks before it removes.
 *
 * The panel runs the model while it is composed, so the app does not run it
 * as well. A new model starts fresh: an import, a download or a dialog of
 * the old model is dropped.
 */
@Composable
public fun AttachmentPanel(model: AttachmentModel, modifier: Modifier = Modifier) {
    ModelScope(model) { AttachmentPanelContent(it, modifier, rememberAttachmentPickers(it)) }
}

/** The panel with pickers made above it, as [PdfReader] makes them so they outlast a change of layout. */
@Composable
internal fun AttachmentPanel(model: AttachmentModel, pickers: AttachmentPickers, modifier: Modifier = Modifier) {
    ModelScope(model) { AttachmentPanelContent(it, modifier, pickers) }
}

@Composable
private fun AttachmentPanelContent(model: AttachmentModel, modifier: Modifier, pickers: AttachmentPickers) {
    val scope = rememberCoroutineScope()
    val files by model.files.collectAsState()
    val selected by model.selectedFiles.collectAsState()
    val status by model.status.collectAsState()
    val canAdd by model.canAdd.collectAsState()
    val canDownload by model.canDownload.collectAsState()
    val canRemove by model.canRemoveSelection.collectAsState()
    val pending by model.pendingIds.collectAsState()
    val performing by model.performing.collectAsState()
    val error by model.error.collectAsState()
    // Saved state by id, so a confirmation outlives a configuration change; shown while the model has the file.
    var deletingId by rememberSaveable { mutableStateOf<String?>(null) }
    val deleting = deletingId?.let { id -> files.firstOrNull { it.id == id } }
    var download by remember { mutableStateOf<Job?>(null) }
    LaunchedEffect(model) { model.run() }
    // A restored confirmation whose file the document no longer has closes.
    LaunchedEffect(files, status) {
        if (status == ResourceStatus.READY && deletingId != null && files.none { it.id == deletingId }) deletingId = null
    }
    val defaultName = stringResource(R.string.embedpdf_attachments_default_name)

    Column(modifier) {
        PanelHeader(stringResource(R.string.embedpdf_attachments_title)) {
            IconButton(onClick = { scope.launch { model.reload() } }, enabled = !performing) {
                Icon(painterResource(R.drawable.embedpdf_ic_refresh), stringResource(R.string.embedpdf_reload))
            }
            IconButton(
                onClick = { pickers.importer.launch(arrayOf("*/*")) },
                enabled = canAdd && !performing,
                modifier = Modifier.testTag("attachmentImport"),
            ) {
                Icon(painterResource(R.drawable.embedpdf_ic_add), stringResource(R.string.embedpdf_attachments_import))
            }
        }
        error?.let { PanelError(it.detail, "attachmentError") }
        if (status == ResourceStatus.LOADING) PanelProgress(stringResource(R.string.embedpdf_attachments_loading))
        Box(Modifier.weight(1f).fillMaxWidth()) {
            LazyColumn(Modifier.fillMaxSize()) {
                items(files, key = { it.id }) { file ->
                    AttachmentRow(
                        file = file,
                        isSelected = selected.any { it.id == file.id },
                        isPending = file.id in pending,
                        enabled = !performing,
                        onSelect = { scope.launch { model.select(listOf(file)) } },
                        onReveal = { scope.launch { model.reveal(file) } },
                    )
                }
            }
            if (files.isEmpty() && status == ResourceStatus.READY) {
                Text(
                    stringResource(R.string.embedpdf_attachments_empty),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center),
                )
            }
        }
        Row(Modifier.fillMaxWidth().padding(8.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            TextButton(
                onClick = {
                    val file = selected.firstOrNull() ?: return@TextButton
                    download?.cancel()
                    download = scope.launch { pickers.saveCopy(model, file, defaultName) }
                },
                enabled = canDownload && selected.size == 1 && !performing,
                modifier = Modifier.testTag("attachmentExport"),
            ) {
                Icon(painterResource(R.drawable.embedpdf_ic_download), null, Modifier.size(ButtonDefaults.IconSize))
                Spacer(Modifier.width(ButtonDefaults.IconSpacing))
                Text(stringResource(R.string.embedpdf_attachments_export))
            }
            TextButton(
                onClick = { deletingId = selected.firstOrNull()?.id },
                enabled = canRemove && selected.size == 1 && !performing,
                colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                modifier = Modifier.testTag("attachmentRemove"),
            ) {
                Icon(painterResource(R.drawable.embedpdf_ic_delete), null, Modifier.size(ButtonDefaults.IconSize))
                Spacer(Modifier.width(ButtonDefaults.IconSpacing))
                Text(stringResource(R.string.embedpdf_attachments_remove))
            }
        }
    }

    deleting?.let { file ->
        AlertDialog(
            onDismissRequest = { deletingId = null },
            title = { Text(stringResource(R.string.embedpdf_attachments_remove_title)) },
            text = { Text(file.name) },
            confirmButton = {
                TextButton(onClick = {
                    deletingId = null
                    scope.launch { model.remove(file) }
                }) {
                    Text(stringResource(R.string.embedpdf_attachments_remove_confirm), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = { TextButton(onClick = { deletingId = null }) { Text(stringResource(R.string.embedpdf_cancel)) } },
        )
    }
}

@Composable
private fun AttachmentRow(
    file: AttachedFile,
    isSelected: Boolean,
    isPending: Boolean,
    enabled: Boolean,
    onSelect: () -> Unit,
    onReveal: () -> Unit,
) {
    val context = LocalContext.current
    val size = file.sizeBytes?.let { Formatter.formatShortFileSize(context, minOf(it, Long.MAX_VALUE.toULong()).toLong()) }
    Row(Modifier.fillMaxWidth().heightIn(min = 56.dp), verticalAlignment = Alignment.CenterVertically) {
        Row(
            Modifier
                .weight(1f)
                .heightIn(min = 56.dp)
                .selectable(selected = isSelected, enabled = enabled, role = Role.Button, onClick = onSelect)
                .semantics(mergeDescendants = true) {}
                .padding(horizontal = 16.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Column(Modifier.weight(1f)) {
                Text(file.name, style = MaterialTheme.typography.bodyLarge)
                file.description?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                size?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
            }
            if (isSelected) {
                Icon(painterResource(R.drawable.embedpdf_ic_check), stringResource(R.string.embedpdf_selected), Modifier.size(20.dp))
            }
        }
        if (isPending) {
            val working = stringResource(R.string.embedpdf_attachments_working)
            CircularProgressIndicator(Modifier.size(20.dp).semantics { contentDescription = working }, strokeWidth = 2.dp)
        }
        if (file.home is AttachmentHomePage) {
            IconButton(onClick = onReveal, enabled = enabled) {
                Icon(painterResource(R.drawable.embedpdf_ic_find_in_page), stringResource(R.string.embedpdf_attachments_reveal))
            }
        }
    }
}

/** Where [AttachmentPickers] keeps a downloaded copy until the save picker answers. */
internal fun attachmentCopyDirectory(context: Context): File = File(context.cacheDir, "embedpdf-attachment-copies")

/**
 * The attachment panel's two document pickers and the copy waiting for the
 * save picker. A result goes to [model], and waits for one while it is
 * `null`, as it is while a recreated reader still builds its models.
 *
 * The launchers' saved-state keys come from where this is remembered, so a
 * reader remembers it above its layout: a result that arrives after a
 * recreation into another layout still finds its launcher.
 */
@Stable
internal class AttachmentPickers(
    private val scope: CoroutineScope,
    private val resolver: ContentResolver,
    private val directory: File,
    /** The path of the copy the save picker is open for. Saved state, so it outlives the activity. */
    private val pendingCopy: MutableState<String?>,
) {
    var model: AttachmentModel? by mutableStateOf(null)
    lateinit var importer: ManagedActivityResultLauncher<Array<String>, List<Uri>>
    lateinit var exporter: ManagedActivityResultLauncher<String, Uri?>
    private var import: Job? = null

    private suspend fun awaitModel(): AttachmentModel = snapshotFlow { model }.filterNotNull().first()

    /** Attaches the files the import picker returned. */
    fun imported(uris: List<Uri>) {
        if (uris.isEmpty()) return
        import?.cancel()
        import = scope.launch { awaitModel().importFiles(uris, resolver) }
    }

    /** Downloads [file] into the cache directory and opens the save picker for it. */
    suspend fun saveCopy(model: AttachmentModel, file: AttachedFile, defaultName: String) {
        val content: AttachmentContent = model.download(file) ?: return
        val copy = try {
            withContext(Dispatchers.IO) { stash(content.data) }
        } catch (e: IOException) {
            model.fileOperationFailed(e)
            return
        }
        pendingCopy.value?.let { earlier -> withContext(Dispatchers.IO) { File(earlier).delete() } }
        pendingCopy.value = copy.path
        exporter.launch(content.name.ifEmpty { defaultName })
    }

    /** Writes the waiting copy where the save picker says, or drops it when the picker was cancelled. */
    fun exported(uri: Uri?) {
        val copy = pendingCopy.value?.let(::File)
        pendingCopy.value = null
        scope.launch {
            try {
                if (uri == null) return@launch
                val bytes = withContext(Dispatchers.IO) { copy?.takeIf { it.isFile }?.readBytes() }
                if (bytes == null) {
                    // The picker created an empty document for a copy that is gone.
                    withContext(Dispatchers.IO) { deleteQuietly(uri) }
                    awaitModel().fileOperationFailed(FileNotFoundException("The attachment's copy is no longer available."))
                    return@launch
                }
                AttachmentExportDocument.write(bytes, uri, resolver)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                awaitModel().fileOperationFailed(e)
            } finally {
                if (copy != null) withContext(NonCancellable + Dispatchers.IO) { copy.delete() }
            }
        }
    }

    private fun stash(data: ByteArray): File {
        directory.mkdirs()
        // A copy whose picker never answered, as when the process died and its reader never came back.
        val stale = System.currentTimeMillis() - STALE_COPY_MILLIS
        directory.listFiles()?.filter { it.lastModified() < stale }?.forEach { it.delete() }
        return File.createTempFile("copy-", ".bin", directory).apply { writeBytes(data) }
    }

    private fun deleteQuietly(uri: Uri) {
        try {
            DocumentsContract.deleteDocument(resolver, uri)
        } catch (e: Exception) {
            // A provider that cannot delete leaves the empty document; the error says why it is empty.
        }
    }

    private companion object {
        const val STALE_COPY_MILLIS = 24L * 60 * 60 * 1000
    }
}

/** Remembers the attachment pickers whose results go to [model]. */
@Composable
internal fun rememberAttachmentPickers(model: AttachmentModel?): AttachmentPickers {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val pendingCopy = rememberSaveable { mutableStateOf<String?>(null) }
    val pickers = remember(scope, pendingCopy) {
        AttachmentPickers(scope, context.applicationContext.contentResolver, attachmentCopyDirectory(context), pendingCopy)
    }
    SideEffect { pickers.model = model }
    pickers.importer = rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { pickers.imported(it) }
    pickers.exporter = rememberLauncherForActivityResult(AttachmentExportDocument.contract()) { pickers.exported(it) }
    return pickers
}
