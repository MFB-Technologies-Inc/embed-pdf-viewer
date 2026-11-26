package com.embedpdf.compose

import androidx.compose.foundation.gestures.Orientation
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconToggleButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.VerticalDivider
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.material3.adaptive.currentWindowAdaptiveInfo
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.window.core.layout.WindowSizeClass
import com.embedpdf.DocumentInfo
import com.embedpdf.LinkActivation
import com.embedpdf.PdfException
import com.embedpdf.StageVisiblePage
import com.embedpdf.ToolId
import com.embedpdf.attachments
import com.embedpdf.history
import com.embedpdf.i18n
import com.embedpdf.models.AttachmentModel
import com.embedpdf.models.HistoryModel
import com.embedpdf.models.HistoryStrings
import com.embedpdf.models.OutlineModel
import com.embedpdf.models.PdfReaderModel
import com.embedpdf.models.SearchModel
import com.embedpdf.models.ThumbnailModel
import com.embedpdf.outline
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.launch

/**
 * A reader in one composable: it draws a [PdfReaderModel]'s phase.
 *
 * ```kotlin
 * val model = remember { PdfReaderModel() }
 * LaunchedEffect(model) { model.open(bytes, name = "report.pdf") }
 * PdfReader(model, tools = listOf(ToolId.POINTER, ToolId.INK))
 * ```
 *
 * Nothing shows while the model is idle and a progress indicator while it
 * loads. A locked document shows a password prompt that calls
 * [PdfReaderModel.unlock], with the reason below the field after a wrong
 * password. A failure shows a localized message, and [onError] receives
 * the exception with the engine's own detail, as it does when the panels'
 * models cannot be made.
 *
 * A ready document shows a [StageReader] under a bar that holds a
 * [SearchBar] when the session has search, and a toggle for each panel the
 * session supports. Below 600 dp of width the search bar takes a row of its
 * own under the toggles, so the field keeps room to type in. The panels are thumbnails always, bookmarks when the viewer has the
 * outline plugin, attachments when it has the attachment plugin (add them
 * with the model's `configure`). The reader scrolls to each hit the search
 * bar reports active, and a [SearchHighlightOverlay] on each page draws the
 * hits in the search settings' colors. On a window at least 600 dp wide an open panel sits
 * beside the pages; on a narrower one it is a modal bottom sheet. Under the
 * pages sits a [ToolPicker] for [tools] when the session has tools and
 * [tools] is not empty, and [HistoryControls] when the viewer has the
 * history plugin. Every control has a label TalkBack reads.
 *
 * Touch on a ready document divides this way. A drag pans, a pinch zooms and
 * a double tap zooms. With selection, a long press and then a drag selects
 * text. A tap follows a link with links on. While a tool other than the
 * default is active, a drag goes to that tool instead of the pan and text
 * selection is off. With selection the reader sets the stage reader's
 * `panBeginsOnMovement`, so a pan begins once the finger has moved 10 dp.
 *
 * [pageOverlay] is laid over each visible page, above the search
 * highlights and the text selection, in the page's frame. [onLinkActivation] receives each link activation
 * while a document shows, from the pages and from the bookmarks; opening a
 * `Uri` is the app's job. The reader runs the session's models; an app does
 * not run them again.
 */
@Composable
public fun PdfReader(
    model: PdfReaderModel,
    modifier: Modifier = Modifier,
    tools: List<ToolId> = emptyList(),
    toolLabel: @Composable (ToolId) -> String = DefaultToolLabel,
    onLinkActivation: (LinkActivation) -> Unit = {},
    onError: (PdfException) -> Unit = {},
    pageOverlay: @Composable (StageVisiblePage, PdfReaderModel.Session) -> Unit = NoPageOverlay,
) {
    val phase by model.phase.collectAsState()
    val activations by rememberUpdatedState(onLinkActivation)
    val errors by rememberUpdatedState(onError)
    when (val current = phase) {
        PdfReaderModel.Phase.Idle -> Box(modifier)
        PdfReaderModel.Phase.Loading -> {
            val label = stringResource(R.string.embedpdf_reader_loading)
            Box(modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator(Modifier.semantics { contentDescription = label }.testTag("readerLoading"))
            }
        }
        // Keyed to the document, so a second locked document starts with an empty field.
        is PdfReaderModel.Phase.Locked -> key(current.document.id) { PasswordPrompt(model, current.document, modifier) }
        is PdfReaderModel.Phase.Ready -> ModelScope(current.session) { session ->
            ReadyReader(session, modifier, tools, toolLabel, { activations(it) }, { errors(it) }, pageOverlay)
        }
        is PdfReaderModel.Phase.Failed -> {
            LaunchedEffect(current) { errors(current.error) }
            Column(
                modifier.fillMaxSize().padding(16.dp).semantics(mergeDescendants = true) {}.testTag("readerFailure"),
                verticalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterVertically),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(stringResource(R.string.embedpdf_reader_failed), style = MaterialTheme.typography.titleMedium, textAlign = TextAlign.Center)
            }
        }
    }
}

private val DefaultToolLabel: @Composable (ToolId) -> String = { toolLabel(it) }

private val NoPageOverlay: @Composable (StageVisiblePage, PdfReaderModel.Session) -> Unit = { _, _ -> }

/** The panels a ready reader can show, one at a time. */
internal enum class ReaderPanel { THUMBNAILS, OUTLINE, ATTACHMENTS }

/** The models a session's panels and history controls read, made for that session. */
private class ReaderExtras(
    val outline: OutlineModel?,
    val attachments: AttachmentModel?,
    val history: HistoryModel?,
)

/** The models over the plugins the session's viewer has; a plugin it lacks gives `null`. */
private suspend fun readerExtras(session: PdfReaderModel.Session, strings: HistoryStrings): ReaderExtras {
    val viewer = session.viewer
    val document = session.document
    val outline = optionalCapability { viewer.outline(document) }
    val attachments = optionalCapability { viewer.attachments(document) }
    val history = optionalCapability { viewer.history(document) }
    val translator = if (history != null) optionalCapability { viewer.i18n() } else null
    return ReaderExtras(
        outline = outline?.let { OutlineModel(viewer, it) },
        attachments = attachments?.let { AttachmentModel(viewer, it) },
        history = history?.let { HistoryModel(viewer, it, translator, strings) },
    )
}

/**
 * [block]'s capability, or `null` when the viewer refuses it as `unsupported`,
 * as it refuses a plugin it was not configured with. Any other failure, such
 * as the document closing, is thrown.
 */
internal suspend fun <T> optionalCapability(block: suspend () -> T): T? = try {
    block()
} catch (e: PdfException) {
    if (e.code != PdfException.Code.UNSUPPORTED) throw e
    null
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
private fun ReadyReader(
    session: PdfReaderModel.Session,
    modifier: Modifier,
    tools: List<ToolId>,
    toolLabel: @Composable (ToolId) -> String,
    onLinkActivation: (LinkActivation) -> Unit,
    onError: (PdfException) -> Unit,
    pageOverlay: @Composable (StageVisiblePage, PdfReaderModel.Session) -> Unit,
) {
    val historyStrings = rememberHistoryStrings()
    val scope = rememberCoroutineScope()
    val sheet = rememberModalBottomSheetState()
    // Why the panels' models could not be made, when the reason is not a plugin left out.
    var extrasError by remember(session) { mutableStateOf<PdfException?>(null) }
    val extras by produceState<ReaderExtras?>(null, session) {
        value = try {
            readerExtras(session, historyStrings)
        } catch (e: PdfException) {
            extrasError = e
            onError(e)
            null
        }
    }
    val thumbnails = remember(session) { ThumbnailModel(session.reader.render, session.pages) }
    var panel by rememberSaveable { mutableStateOf<ReaderPanel?>(null) }
    val wide = currentWindowAdaptiveInfo().windowSizeClass.isWidthAtLeastBreakpoint(WindowSizeClass.WIDTH_DP_MEDIUM_LOWER_BOUND)
    val outline = extras?.outline
    val attachments = extras?.attachments
    val history = extras?.history
    // A panel whose plugin the viewer lacks stays shut.
    val shown = panel?.takeIf {
        when (it) {
            ReaderPanel.THUMBNAILS -> true
            ReaderPanel.OUTLINE -> outline != null
            ReaderPanel.ATTACHMENTS -> attachments != null
        }
    }
    val picker = session.tools?.takeIf { tools.isNotEmpty() }
    // Above the layout branch, so a picker result finds its launcher after a
    // rotation moves the panel between the side sheet and the bottom sheet.
    val attachmentPickers = rememberAttachmentPickers(attachments)

    // Shuts the panel; in the bottom sheet, once the sheet has slid away.
    val closePanel: () -> Unit = {
        if (wide || !sheet.isVisible) {
            panel = null
        } else {
            scope.launch { sheet.hide() }.invokeOnCompletion { if (!sheet.isVisible) panel = null }
        }
    }
    val panelContent: @Composable (Modifier) -> Unit = { panelModifier ->
        when (shown) {
            ReaderPanel.THUMBNAILS -> ThumbnailPanel(session, thumbnails, wide, panelModifier) { if (!wide) closePanel() }
            ReaderPanel.OUTLINE -> outline?.let {
                OutlinePanel(it, panelModifier) { activation ->
                    if (!wide) closePanel()
                    onLinkActivation(activation)
                }
            }
            ReaderPanel.ATTACHMENTS -> attachments?.let { AttachmentPanel(it, attachmentPickers, panelModifier) }
            null -> Unit
        }
    }

    Column(modifier) {
        val search = session.search
        Row(Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
            if (search != null && wide) {
                RevealingSearchBar(search, Modifier.weight(1f))
            } else {
                Spacer(Modifier.weight(1f))
            }
            val toggle: (ReaderPanel?) -> Unit = { next -> if (next == null) closePanel() else panel = next }
            PanelToggle(ReaderPanel.THUMBNAILS, shown, R.drawable.embedpdf_ic_grid, R.string.embedpdf_reader_thumbnails, toggle)
            if (outline != null) {
                PanelToggle(ReaderPanel.OUTLINE, shown, R.drawable.embedpdf_ic_bookmark, R.string.embedpdf_outline_title, toggle)
            }
            if (attachments != null) {
                PanelToggle(ReaderPanel.ATTACHMENTS, shown, R.drawable.embedpdf_ic_attach, R.string.embedpdf_attachments_title, toggle)
            }
        }
        if (extrasError != null) PanelError(stringResource(R.string.embedpdf_reader_panels_failed), "readerPanelsError")
        if (search != null && !wide) {
            RevealingSearchBar(search, Modifier.fillMaxWidth().padding(start = 8.dp, end = 8.dp, bottom = 4.dp))
        }
        HorizontalDivider()
        Row(Modifier.weight(1f).fillMaxWidth()) {
            if (wide && shown != null) {
                Surface(Modifier.width(SIDE_SHEET_WIDTH).fillMaxHeight().testTag("readerSideSheet"), tonalElevation = 1.dp) {
                    Column {
                        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                            IconButton(onClick = { panel = null }) {
                                Icon(painterResource(R.drawable.embedpdf_ic_close), stringResource(R.string.embedpdf_reader_close_panel))
                            }
                        }
                        panelContent(Modifier.weight(1f).fillMaxWidth())
                    }
                }
                VerticalDivider()
            }
            ReaderStage(session, Modifier.weight(1f).fillMaxHeight(), onLinkActivation, pageOverlay)
        }
        if (picker != null || history != null) {
            HorizontalDivider()
            FlowRow(
                Modifier.fillMaxWidth().padding(horizontal = 8.dp, vertical = 4.dp),
                horizontalArrangement = Arrangement.spacedBy(16.dp),
                itemVerticalAlignment = Alignment.CenterVertically,
            ) {
                picker?.let { ToolPicker(it, tools, toolLabel) }
                history?.let { HistoryControls(it) }
            }
        }
    }

    if (!wide && shown != null) {
        ModalBottomSheet(onDismissRequest = { panel = null }, sheetState = sheet, modifier = Modifier.testTag("readerBottomSheet")) {
            panelContent(Modifier.fillMaxWidth().height(BOTTOM_SHEET_HEIGHT))
        }
    }
}

/** A toolbar toggle that opens [panel], or closes it when it is the one [shown]. TalkBack reads its label and whether it is on. */
@Composable
private fun PanelToggle(panel: ReaderPanel, shown: ReaderPanel?, icon: Int, label: Int, onChange: (ReaderPanel?) -> Unit) {
    IconToggleButton(
        checked = shown == panel,
        onCheckedChange = { on -> onChange(if (on) panel else null) },
        modifier = Modifier.testTag("readerPanel-${panel.name.lowercase()}"),
    ) {
        Icon(painterResource(icon), stringResource(label))
    }
}

@Composable
private fun ThumbnailPanel(
    session: PdfReaderModel.Session,
    thumbnails: ThumbnailModel,
    wide: Boolean,
    modifier: Modifier,
    onSelected: () -> Unit,
) {
    val scope = rememberCoroutineScope()
    val pageIndex by session.reader.pageIndex.collectAsState()
    Column(modifier) {
        PanelHeader(stringResource(R.string.embedpdf_reader_thumbnails)) {}
        ThumbnailStrip(
            model = thumbnails,
            pages = session.pages,
            current = session.pages.getOrNull(pageIndex)?.page,
            onSelect = { page ->
                onSelected()
                scope.launch {
                    try {
                        session.reader.stage.goToPage(page.page)
                    } catch (e: CancellationException) {
                        throw e
                    } catch (e: PdfException) {
                        // The document closed under the tap; the next phase replaces the panel.
                    }
                }
            },
            modifier = if (wide) Modifier.weight(1f).fillMaxWidth() else Modifier.fillMaxWidth().height(160.dp),
            orientation = if (wide) Orientation.Vertical else Orientation.Horizontal,
        )
    }
}

/** The pages, with search highlights under text selection, both under the app's overlay. */
@Composable
private fun ReaderStage(
    session: PdfReaderModel.Session,
    modifier: Modifier,
    onLinkActivation: (LinkActivation) -> Unit,
    pageOverlay: @Composable (StageVisiblePage, PdfReaderModel.Session) -> Unit,
) {
    val selection = session.selection
    val search = session.search
    val forwardsDrags by session.reader.forwardsDrags.collectAsState()
    // A long press delays the stage's drag to the first move or the lift, so
    // a tap that moves a few dp must not start a pan: it would cancel the
    // double tap's zoom or the link's scroll that the same tap started.
    LaunchedEffect(session.reader) { session.reader.panBeginsOnMovement = selection != null }
    if (selection != null) LaunchedEffect(selection) { selection.run() }
    StageReader(session.reader, modifier, onLinkActivation) { page ->
        Box(Modifier.fillMaxSize()) {
            val info = session.pageInfo(page)
            // The search bar runs the model; the overlay only draws it.
            if (search != null && info != null) {
                SearchHighlightOverlay(search, page, info, Modifier.fillMaxSize())
            }
            // Absent under a tool other than the default, whose drags the
            // reader forwards: the long press would take them from it.
            if (selection != null && !forwardsDrags && info != null) {
                TextSelectionOverlay(selection, page, info, Modifier.fillMaxSize(), activation = TextSelectionActivation.LONG_PRESS)
            }
            pageOverlay(page, session)
        }
    }
}

/** A [SearchBar] that brings each hit it reports active into view, and reveals at no other time. */
@Composable
private fun RevealingSearchBar(model: SearchModel, modifier: Modifier) {
    val reveals = remember(model) { SearchHitReveals() }
    var waiting by remember(model) { mutableStateOf<Int?>(null) }
    SearchBar(model, modifier) {
        reveals.hitReported()
        waiting = reveals.waiting
    }
    // One reveal waits at a time: a newer report cancels the wait for the
    // last one's reveal and starts its own.
    //
    // Performance: the engine already reveals on a next or previous step, so
    // each step makes a second reveal with the same target; only the first
    // hit of a new search needs this one.
    LaunchedEffect(waiting) {
        val request = waiting ?: return@LaunchedEffect
        model.revealActiveHit()
        reveals.finished(request)
        waiting = reveals.waiting
    }
}

/** The password field and unlock button a locked document shows. */
@Composable
private fun PasswordPrompt(model: PdfReaderModel, document: DocumentInfo, modifier: Modifier) {
    val scope = rememberCoroutineScope()
    // Not saved state: the saved-state bundle is written to disk, and a password must not be.
    var password by remember { mutableStateOf("") }
    // Whether an unlock is on its way to the engine.
    var submitting by remember { mutableStateOf(false) }
    val unlockError by model.unlockError.collectAsState()
    val fieldLabel = stringResource(R.string.embedpdf_reader_password_label)
    val submit = {
        if (password.isNotEmpty() && !submitting) {
            submitting = true
            scope.launch {
                try {
                    model.unlock(password)
                } finally {
                    submitting = false
                }
            }
        }
    }
    Column(
        modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        val name = document.name
        Text(
            if (name != null) stringResource(R.string.embedpdf_reader_locked_named, name) else stringResource(R.string.embedpdf_reader_locked),
            style = MaterialTheme.typography.titleMedium,
            textAlign = TextAlign.Center,
        )
        OutlinedTextField(
            value = password,
            onValueChange = { password = it },
            label = { Text(stringResource(R.string.embedpdf_reader_password)) },
            singleLine = true,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Go),
            keyboardActions = KeyboardActions(onGo = { submit() }),
            modifier = Modifier.widthIn(max = 320.dp).semantics { contentDescription = fieldLabel }.testTag("readerPassword"),
        )
        Button(onClick = { submit() }, enabled = password.isNotEmpty() && !submitting, modifier = Modifier.testTag("readerUnlock")) {
            Text(stringResource(R.string.embedpdf_reader_unlock))
        }
        unlockError?.let {
            Text(
                it.detail,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
                textAlign = TextAlign.Center,
                modifier = Modifier.testTag("readerUnlockError"),
            )
        }
    }
}

/** The width of a panel beside the pages. */
private val SIDE_SHEET_WIDTH = 320.dp

/** The height of a panel in the bottom sheet. */
private val BOTTOM_SHEET_HEIGHT = 480.dp
