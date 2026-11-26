package com.embedpdf.compose

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.combinedClickable
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
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.selection.toggleable
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.MutableState
import androidx.compose.runtime.Stable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.embedpdf.LinkActivation
import com.embedpdf.OutlineItem
import com.embedpdf.OutlineItemInput
import com.embedpdf.OutlineItemPatch
import com.embedpdf.OutlineItemRef
import com.embedpdf.OutlinePatchKey
import com.embedpdf.OutlinePosition
import com.embedpdf.OutlinePositionAfter
import com.embedpdf.OutlinePositionEnd
import com.embedpdf.OutlinePositionStart
import com.embedpdf.OutlineRow
import com.embedpdf.PageLinkTarget
import com.embedpdf.PageLinkTargetUri
import com.embedpdf.ResourceStatus
import com.embedpdf.models.OutlineModel
import kotlinx.coroutines.launch

/**
 * A document's bookmarks over an [OutlineModel]: a header with reload and
 * add buttons, the rows the model shows, and forms to edit, move and delete
 * a bookmark.
 *
 * A row with children has an expand or collapse button whose state TalkBack
 * reads. Tapping a row activates its bookmark and passes the outcome to
 * [onActivation]; opening a `Uri` is the app's job. A long press on a row,
 * or its actions button, opens a menu to select it and, while the model
 * `canEdit`, to edit it, add a child or a sibling, move it or delete it. The
 * current location and the selected bookmarks carry an icon with a label.
 *
 * The panel runs the model while it is composed, so the app does not run it
 * as well. A new model starts fresh: a form open over the old model closes,
 * and an action the old one had not started is dropped.
 */
@Composable
public fun OutlinePanel(
    model: OutlineModel,
    modifier: Modifier = Modifier,
    onActivation: (LinkActivation) -> Unit = {},
) {
    ModelScope(model) { OutlinePanelContent(it, modifier, onActivation) }
}

@Composable
private fun OutlinePanelContent(model: OutlineModel, modifier: Modifier, onActivation: (LinkActivation) -> Unit) {
    val scope = rememberCoroutineScope()
    val activated by rememberUpdatedState(onActivation)
    val rows by model.rows.collectAsState()
    val status by model.status.collectAsState()
    val incomplete by model.incomplete.collectAsState()
    val canEdit by model.canEdit.collectAsState()
    val performing by model.performing.collectAsState()
    val error by model.error.collectAsState()
    val current by model.currentItem.collectAsState()
    val selected by model.selectedItems.collectAsState()
    val editor = remember { mutableStateOf<OutlineEditor?>(null) }
    val moving = remember { mutableStateOf<OutlineMoveEditor?>(null) }
    val deleting = remember { mutableStateOf<OutlineItem?>(null) }
    LaunchedEffect(model) { model.run() }

    Column(modifier) {
        PanelHeader(stringResource(R.string.embedpdf_outline_title)) {
            IconButton(onClick = { scope.launch { model.reload() } }, enabled = !performing) {
                Icon(painterResource(R.drawable.embedpdf_ic_refresh), stringResource(R.string.embedpdf_reload))
            }
            IconButton(
                onClick = { editor.value = OutlineEditor() },
                enabled = canEdit && !performing,
                modifier = Modifier.testTag("outlineAdd"),
            ) {
                Icon(painterResource(R.drawable.embedpdf_ic_add), stringResource(R.string.embedpdf_outline_add))
            }
        }
        if (incomplete) PanelNote(stringResource(R.string.embedpdf_outline_incomplete))
        error?.let { PanelError(it.detail, "outlineError") }
        if (status == ResourceStatus.LOADING) PanelProgress(stringResource(R.string.embedpdf_outline_loading))
        Box(Modifier.weight(1f, fill = true).fillMaxWidth()) {
            LazyColumn(Modifier.fillMaxSize()) {
                items(rows, key = { it.item.itemRef.objectNumber.toLong() }) { row ->
                    OutlineRowView(
                        row = row,
                        isCurrent = current == row.item.itemRef,
                        isSelected = row.item.itemRef in selected,
                        canEdit = canEdit,
                        enabled = !performing,
                        onToggle = { scope.launch { model.toggle(row.item.itemRef) } },
                        onActivate = {
                            scope.launch {
                                val result = model.activate(row.item.itemRef) ?: return@launch
                                activated(result)
                            }
                        },
                        onSelect = { scope.launch { model.select(listOf(row.item.itemRef)) } },
                        onEdit = { editor.value = OutlineEditor(item = row.item) },
                        onAddChild = { editor.value = OutlineEditor(parent = row.item.itemRef) },
                        onAddSibling = {
                            editor.value = OutlineEditor(parent = row.item.parent, position = OutlinePositionAfter(row.item.itemRef))
                        },
                        onMove = { moving.value = OutlineMoveEditor(row.item) },
                        onDelete = { deleting.value = row.item },
                    )
                }
            }
            if (rows.isEmpty() && status == ResourceStatus.READY) {
                Text(
                    stringResource(R.string.embedpdf_outline_empty),
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center),
                )
            }
        }
    }

    editor.value?.let { OutlineEditForm(model, it, editor) }
    moving.value?.let { OutlineMoveForm(model, it, moving) }
    deleting.value?.let { item ->
        AlertDialog(
            onDismissRequest = { deleting.controlPresented = false },
            title = { Text(stringResource(R.string.embedpdf_outline_delete_title)) },
            text = { Text(item.title) },
            confirmButton = {
                TextButton(onClick = {
                    deleting.value = null
                    scope.launch { model.delete(listOf(item.itemRef)) }
                }) {
                    Text(stringResource(R.string.embedpdf_outline_delete_confirm), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { deleting.value = null }) { Text(stringResource(R.string.embedpdf_cancel)) }
            },
        )
    }
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun OutlineRowView(
    row: OutlineRow,
    isCurrent: Boolean,
    isSelected: Boolean,
    canEdit: Boolean,
    enabled: Boolean,
    onToggle: () -> Unit,
    onActivate: () -> Unit,
    onSelect: () -> Unit,
    onEdit: () -> Unit,
    onAddChild: () -> Unit,
    onAddSibling: () -> Unit,
    onMove: () -> Unit,
    onDelete: () -> Unit,
) {
    var menu by remember { mutableStateOf(false) }
    val level = stringResource(R.string.embedpdf_outline_level, row.depth.toInt() + 1)
    Row(
        Modifier.fillMaxWidth().padding(start = (row.depth.toInt() * 16).dp).heightIn(min = 48.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (row.hasChildren) {
            val state = stringResource(if (row.expanded) R.string.embedpdf_outline_expanded else R.string.embedpdf_outline_collapsed)
            IconButton(onClick = onToggle, enabled = enabled, modifier = Modifier.semantics { stateDescription = state }) {
                Icon(
                    painterResource(if (row.expanded) R.drawable.embedpdf_ic_arrow_down else R.drawable.embedpdf_ic_chevron_end),
                    stringResource(if (row.expanded) R.string.embedpdf_outline_collapse else R.string.embedpdf_outline_expand),
                )
            }
        } else {
            Spacer(Modifier.width(48.dp))
        }
        Row(
            Modifier
                .weight(1f)
                .heightIn(min = 48.dp)
                .combinedClickable(
                    enabled = enabled,
                    role = Role.Button,
                    onClickLabel = stringResource(R.string.embedpdf_outline_activate),
                    onLongClick = { menu = true },
                    onClick = onActivate,
                )
                .semantics { stateDescription = level },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                row.item.title,
                modifier = Modifier.weight(1f),
                fontWeight = if (row.item.bold) FontWeight.Bold else null,
                fontStyle = if (row.item.italic) FontStyle.Italic else null,
            )
            if (isCurrent) {
                Icon(painterResource(R.drawable.embedpdf_ic_place), stringResource(R.string.embedpdf_outline_current), Modifier.size(20.dp))
            }
            if (isSelected) {
                Icon(painterResource(R.drawable.embedpdf_ic_check), stringResource(R.string.embedpdf_selected), Modifier.size(20.dp))
            }
        }
        Box {
            IconButton(onClick = { menu = true }, enabled = enabled) {
                Icon(painterResource(R.drawable.embedpdf_ic_more), stringResource(R.string.embedpdf_outline_actions))
            }
            DropdownMenu(expanded = menu, onDismissRequest = { menu = false }) {
                val item: @Composable (Int, () -> Unit) -> Unit = { label, action ->
                    DropdownMenuItem(text = { Text(stringResource(label)) }, onClick = {
                        menu = false
                        action()
                    })
                }
                item(R.string.embedpdf_outline_select, onSelect)
                if (canEdit) {
                    item(R.string.embedpdf_outline_edit, onEdit)
                    item(R.string.embedpdf_outline_add_child, onAddChild)
                    item(R.string.embedpdf_outline_add_sibling, onAddSibling)
                    item(R.string.embedpdf_outline_move, onMove)
                    item(R.string.embedpdf_outline_delete, onDelete)
                }
            }
        }
    }
}

/** What a destination choice in the edit form means. */
private enum class Destination { KEEP, URI, NONE }

/** The edit form's fields: a new bookmark when [item] is `null`, else an edit of it. */
@Stable
private class OutlineEditor(
    val item: OutlineItem? = null,
    val parent: OutlineItemRef? = null,
    val position: OutlinePosition? = null,
) {
    var title by mutableStateOf(item?.title ?: "")
    var destination by mutableStateOf(Destination.KEEP)
    var uri by mutableStateOf("")
    var defaultColor by mutableStateOf(item?.color == null)
    var color by mutableStateOf(item?.color ?: "#000000")
    var bold by mutableStateOf(item?.bold ?: false)
    var italic by mutableStateOf(item?.italic ?: false)
    var open by mutableStateOf(item?.open ?: true)
}

@Composable
private fun OutlineEditForm(model: OutlineModel, editor: OutlineEditor, presented: MutableState<OutlineEditor?>) {
    val scope = rememberCoroutineScope()
    val performing by model.performing.collectAsState()
    val canEdit by model.canEdit.collectAsState()
    val error by model.error.collectAsState()
    val isCurrent = { presented.value === editor }
    AlertDialog(
        onDismissRequest = { if (isCurrent()) presented.value = null },
        title = {
            Text(stringResource(if (editor.item == null) R.string.embedpdf_outline_new_title else R.string.embedpdf_outline_edit_title))
        },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = editor.title,
                    onValueChange = { editor.title = it },
                    label = { Text(stringResource(R.string.embedpdf_outline_field_title)) },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth().testTag("outlineTitle"),
                )
                Text(stringResource(R.string.embedpdf_outline_destination), style = MaterialTheme.typography.labelLarge)
                Column(Modifier.selectableGroup()) {
                    val keep = if (editor.item == null) R.string.embedpdf_outline_current_view else R.string.embedpdf_outline_keep_destination
                    Choice(stringResource(keep), editor.destination == Destination.KEEP) { editor.destination = Destination.KEEP }
                    Choice(stringResource(R.string.embedpdf_outline_web_address), editor.destination == Destination.URI) {
                        editor.destination = Destination.URI
                    }
                    Choice(stringResource(R.string.embedpdf_outline_no_destination), editor.destination == Destination.NONE) {
                        editor.destination = Destination.NONE
                    }
                }
                if (editor.destination == Destination.URI) {
                    OutlinedTextField(
                        value = editor.uri,
                        onValueChange = { editor.uri = it },
                        label = { Text(stringResource(R.string.embedpdf_outline_address)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
                SwitchRow(stringResource(R.string.embedpdf_outline_default_color), editor.defaultColor) { editor.defaultColor = it }
                if (!editor.defaultColor) {
                    OutlinedTextField(
                        value = editor.color,
                        onValueChange = { editor.color = it },
                        label = { Text(stringResource(R.string.embedpdf_outline_color)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
                SwitchRow(stringResource(R.string.embedpdf_outline_bold), editor.bold) { editor.bold = it }
                SwitchRow(stringResource(R.string.embedpdf_outline_italic), editor.italic) { editor.italic = it }
                if (editor.item != null) {
                    SwitchRow(stringResource(R.string.embedpdf_outline_open), editor.open) { editor.open = it }
                }
                error?.let { Text(it.detail, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall) }
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    scope.launch {
                        if (!isCurrent()) return@launch
                        if (save(model, editor) && isCurrent()) presented.value = null
                    }
                },
                enabled = !performing && canEdit && !(editor.destination == Destination.URI && editor.uri.isEmpty()),
                modifier = Modifier.testTag("outlineSave"),
            ) { Text(stringResource(R.string.embedpdf_outline_save)) }
        },
        dismissButton = {
            TextButton(onClick = { if (isCurrent()) presented.value = null }) { Text(stringResource(R.string.embedpdf_cancel)) }
        },
    )
}

/** Sends the form; whether the model saved it. */
private suspend fun save(model: OutlineModel, editor: OutlineEditor): Boolean {
    val target: PageLinkTarget? = if (editor.destination == Destination.URI) PageLinkTargetUri(editor.uri) else null
    val color = if (editor.defaultColor) null else editor.color
    val item = editor.item
    return if (item != null) {
        val clear = buildList {
            if (editor.destination == Destination.NONE) add(OutlinePatchKey.TARGET)
            if (editor.defaultColor) add(OutlinePatchKey.COLOR)
        }
        model.update(
            item.itemRef,
            OutlineItemPatch(
                title = editor.title,
                target = target,
                color = color,
                bold = editor.bold,
                italic = editor.italic,
                open = editor.open,
                clear = clear,
            ),
        ) != null
    } else {
        model.create(
            OutlineItemInput(
                title = editor.title.ifEmpty { null },
                parent = editor.parent,
                position = editor.position,
                target = target,
                clearTarget = editor.destination == Destination.NONE,
                color = color,
                bold = editor.bold,
                italic = editor.italic,
            ),
        ) != null
    }
}

/** The move form's fields. */
@Stable
private class OutlineMoveEditor(val item: OutlineItem) {
    var parent by mutableStateOf(item.parent)
    var first by mutableStateOf(false)
}

@Composable
private fun OutlineMoveForm(model: OutlineModel, editor: OutlineMoveEditor, presented: MutableState<OutlineMoveEditor?>) {
    val scope = rememberCoroutineScope()
    val items by model.items.collectAsState()
    val performing by model.performing.collectAsState()
    val canEdit by model.canEdit.collectAsState()
    val error by model.error.collectAsState()
    val isCurrent = { presented.value === editor }
    AlertDialog(
        onDismissRequest = { if (isCurrent()) presented.value = null },
        title = { Text(stringResource(R.string.embedpdf_outline_move_title)) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState())) {
                Text(stringResource(R.string.embedpdf_outline_parent), style = MaterialTheme.typography.labelLarge)
                Column(Modifier.selectableGroup()) {
                    Choice(stringResource(R.string.embedpdf_outline_root), editor.parent == null) { editor.parent = null }
                    for (item in items) {
                        Choice(item.title, editor.parent == item.itemRef) { editor.parent = item.itemRef }
                    }
                }
                SwitchRow(stringResource(R.string.embedpdf_outline_place_first), editor.first) { editor.first = it }
                error?.let { Text(it.detail, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall) }
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    scope.launch {
                        if (!isCurrent()) return@launch
                        model.move(listOf(editor.item.itemRef), editor.parent, if (editor.first) OutlinePositionStart else OutlinePositionEnd)
                        if (model.error.value == null && isCurrent()) presented.value = null
                    }
                },
                enabled = !performing && canEdit,
            ) { Text(stringResource(R.string.embedpdf_outline_move_confirm)) }
        },
        dismissButton = {
            TextButton(onClick = { if (isCurrent()) presented.value = null }) { Text(stringResource(R.string.embedpdf_cancel)) }
        },
    )
}

/** One radio choice of a group, labelled by its text. */
@Composable
private fun Choice(label: String, selected: Boolean, onSelect: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 48.dp).selectable(selected = selected, role = Role.RadioButton, onClick = onSelect),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        RadioButton(selected = selected, onClick = null)
        Spacer(Modifier.width(8.dp))
        Text(label)
    }
}

/** A switch with its label, toggled from anywhere on the row. */
@Composable
private fun SwitchRow(label: String, checked: Boolean, onChange: (Boolean) -> Unit) {
    Row(
        Modifier.fillMaxWidth().heightIn(min = 48.dp).toggleable(value = checked, role = Role.Switch, onValueChange = onChange),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(label, Modifier.weight(1f))
        Switch(checked = checked, onCheckedChange = null)
    }
}

/** A panel's title row, with its [actions] at the end. */
@Composable
internal fun PanelHeader(title: String, actions: @Composable () -> Unit) {
    Row(Modifier.fillMaxWidth().padding(start = 16.dp, end = 4.dp, top = 4.dp, bottom = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Text(title, style = MaterialTheme.typography.titleMedium, modifier = Modifier.weight(1f))
        actions()
    }
}

@Composable
internal fun PanelNote(text: String) {
    Text(text, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(horizontal = 16.dp))
}

@Composable
internal fun PanelError(text: String, tag: String) {
    Text(
        text,
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.error,
        modifier = Modifier.padding(horizontal = 16.dp).testTag(tag),
    )
}

@Composable
internal fun PanelProgress(label: String) {
    Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        CircularProgressIndicator(Modifier.size(20.dp), strokeWidth = 2.dp)
        Text(label, style = MaterialTheme.typography.bodyMedium)
    }
}
