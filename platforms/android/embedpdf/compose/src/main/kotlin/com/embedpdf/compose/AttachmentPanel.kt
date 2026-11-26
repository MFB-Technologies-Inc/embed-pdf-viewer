package com.embedpdf.compose

import android.text.format.Formatter
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
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
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
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

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
 * ([AttachmentExportDocument]). A failure to write lands in the model's
 * error. The downloaded bytes are held in memory, not saved state, so a copy
 * whose picker outlives the activity (a configuration change while it is
 * open) is dropped and leaves the created document empty. "Remove…" asks
 * before it removes.
 *
 * The panel runs the model while it is composed, so the app does not run it
 * as well. A new model starts fresh: an import, a download or a dialog of
 * the old model is dropped.
 */
@Composable
public fun AttachmentPanel(model: AttachmentModel, modifier: Modifier = Modifier) {
    ModelScope(model) { AttachmentPanelContent(it, modifier) }
}

@Composable
private fun AttachmentPanelContent(model: AttachmentModel, modifier: Modifier) {
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val files by model.files.collectAsState()
    val selected by model.selectedFiles.collectAsState()
    val status by model.status.collectAsState()
    val canAdd by model.canAdd.collectAsState()
    val canDownload by model.canDownload.collectAsState()
    val canRemove by model.canRemoveSelection.collectAsState()
    val pending by model.pendingIds.collectAsState()
    val performing by model.performing.collectAsState()
    val error by model.error.collectAsState()
    var content by remember { mutableStateOf<AttachmentContent?>(null) }
    var deleting by remember { mutableStateOf<AttachedFile?>(null) }
    var download by remember { mutableStateOf<Job?>(null) }
    var import by remember { mutableStateOf<Job?>(null) }
    LaunchedEffect(model) { model.run() }

    val importer = rememberLauncherForActivityResult(ActivityResultContracts.OpenMultipleDocuments()) { uris ->
        if (uris.isEmpty()) return@rememberLauncherForActivityResult
        import?.cancel()
        import = scope.launch { model.importFiles(uris, context.contentResolver) }
    }
    val exporter = rememberLauncherForActivityResult(AttachmentExportDocument.contract()) { uri ->
        val saved = content
        content = null
        if (uri == null || saved == null) return@rememberLauncherForActivityResult
        scope.launch {
            try {
                AttachmentExportDocument.write(saved.data, uri, context.contentResolver)
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                model.fileOperationFailed(e)
            }
        }
    }
    val defaultName = stringResource(R.string.embedpdf_attachments_default_name)

    Column(modifier) {
        PanelHeader(stringResource(R.string.embedpdf_attachments_title)) {
            IconButton(onClick = { scope.launch { model.reload() } }, enabled = !performing) {
                Icon(painterResource(R.drawable.embedpdf_ic_refresh), stringResource(R.string.embedpdf_reload))
            }
            IconButton(
                onClick = { importer.launch(arrayOf("*/*")) },
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
                    download = scope.launch {
                        val result = model.download(file) ?: return@launch
                        content = result
                        exporter.launch(result.name.ifEmpty { defaultName })
                    }
                },
                enabled = canDownload && selected.size == 1 && !performing,
                modifier = Modifier.testTag("attachmentExport"),
            ) {
                Icon(painterResource(R.drawable.embedpdf_ic_download), null, Modifier.size(ButtonDefaults.IconSize))
                Spacer(Modifier.width(ButtonDefaults.IconSpacing))
                Text(stringResource(R.string.embedpdf_attachments_export))
            }
            TextButton(
                onClick = { deleting = selected.firstOrNull() },
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
            onDismissRequest = { deleting = null },
            title = { Text(stringResource(R.string.embedpdf_attachments_remove_title)) },
            text = { Text(file.name) },
            confirmButton = {
                TextButton(onClick = {
                    deleting = null
                    scope.launch { model.remove(file) }
                }) {
                    Text(stringResource(R.string.embedpdf_attachments_remove_confirm), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = { TextButton(onClick = { deleting = null }) { Text(stringResource(R.string.embedpdf_cancel)) } },
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
                .clickable(enabled = enabled, role = Role.Button, onClick = onSelect)
                .semantics(mergeDescendants = true) { this.selected = isSelected }
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
