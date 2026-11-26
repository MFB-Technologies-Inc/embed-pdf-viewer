package com.embedpdf.demo

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.embedpdf.ToolId
import com.embedpdf.compose.PdfReader
import com.embedpdf.models.PdfReaderModel

/** The reader under a top bar that opens documents, adds a note and shows the metadata sheet. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentScreen(model: DocumentViewModel = viewModel()) {
    val phase by model.reader.phase.collectAsState()
    val error by model.error.collectAsState()
    val canAddNote by model.canAddNote.collectAsState()
    val linkActivation by model.linkActivation.collectAsState()
    val metadataSheet by model.metadataSheet.collectAsState()
    var openMenu by rememberSaveable { mutableStateOf(false) }
    val opener = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { model.fileOpened(it) }
    val ready = phase as? PdfReaderModel.Phase.Ready
    val select = stringResource(R.string.tool_select)
    val ink = stringResource(R.string.tool_ink)

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(ready?.session?.document?.name ?: stringResource(R.string.app_name), maxLines = 1, overflow = TextOverflow.Ellipsis)
                },
                navigationIcon = {
                    Box {
                        TextButton(onClick = { openMenu = true }) { Text(stringResource(R.string.open)) }
                        DropdownMenu(expanded = openMenu, onDismissRequest = { openMenu = false }) {
                            DropdownMenuItem(text = { Text(stringResource(R.string.open_files)) }, onClick = {
                                openMenu = false
                                opener.launch(arrayOf("application/pdf"))
                            })
                            DropdownMenuItem(text = { Text(stringResource(R.string.sample_report)) }, onClick = {
                                openMenu = false
                                model.sampleSelected("report")
                            })
                            DropdownMenuItem(text = { Text(stringResource(R.string.sample_encrypted)) }, onClick = {
                                openMenu = false
                                model.sampleSelected("encrypted-hello")
                            })
                        }
                    }
                },
                actions = {
                    TextButton(onClick = { model.addNoteSelected() }, enabled = canAddNote) { Text(stringResource(R.string.add_note)) }
                    TextButton(onClick = { model.infoSelected() }, enabled = ready != null) { Text(stringResource(R.string.info)) }
                },
            )
        },
    ) { padding ->
        Box(Modifier.padding(padding).fillMaxSize()) {
            PdfReader(
                model.reader,
                Modifier.fillMaxSize(),
                tools = listOf(ToolId.POINTER, ToolId.INK),
                toolLabel = { if (it == ToolId.INK) ink else select },
                onLinkActivation = model::linkActivated,
                onError = model::readerFailed,
            )
            // Over the reader: a label that changed the reader's height would
            // resize the viewport while the link's reveal animates.
            linkActivation?.let { activation ->
                Surface(
                    Modifier.align(Alignment.BottomCenter).padding(8.dp),
                    shape = MaterialTheme.shapes.extraLarge,
                    tonalElevation = 3.dp,
                ) {
                    Text(
                        stringResource(R.string.link_activation, activation.toString()),
                        style = MaterialTheme.typography.labelMedium,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                    )
                }
            }
        }
    }

    error?.let { detail ->
        AlertDialog(
            onDismissRequest = model::errorDismissed,
            title = { Text(stringResource(R.string.error_title)) },
            text = { Text(detail) },
            confirmButton = { TextButton(onClick = model::errorDismissed) { Text(stringResource(R.string.ok)) } },
        )
    }

    metadataSheet?.let { sheet -> MetadataSheet(sheet, onDismiss = model::metadataDismissed) }
}
