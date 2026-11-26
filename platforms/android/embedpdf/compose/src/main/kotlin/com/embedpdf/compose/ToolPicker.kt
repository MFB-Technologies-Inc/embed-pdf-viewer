package com.embedpdf.compose

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.embedpdf.ToolId
import com.embedpdf.models.ToolModel
import kotlinx.coroutines.launch

/**
 * One toggle chip per tool in [tools], in that order, selected for the
 * active tool. A chip is disabled while its tool is neither active nor in
 * `ToolModel.usable`, and every chip while `ToolModel.submitting`, which
 * lasts for an activation's hub call. TalkBack reads each chip's label and
 * whether it is selected or disabled.
 *
 * Tapping a tool activates it; tapping the active tool returns to the
 * default tool. [label] names each tool, and [icon], when given, draws its
 * leading icon. The picker runs the model while it is composed, so the app
 * does not run it as well.
 *
 * ```kotlin
 * ToolPicker(tools, listOf(ToolId.POINTER, ToolId.INK), label = { toolLabel(it) })
 * ```
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
public fun ToolPicker(
    model: ToolModel,
    tools: List<ToolId>,
    label: @Composable (ToolId) -> String,
    modifier: Modifier = Modifier,
    icon: (@Composable (ToolId) -> Unit)? = null,
) {
    ModelScope(model) { picker ->
        val scope = rememberCoroutineScope()
        LaunchedEffect(picker) { picker.run() }
        // Read so a change recomposes the chips; `isSelected` and
        // `isDisabled` read the same flows' values.
        //
        // Performance: every chip reads these three, so a change to any of
        // them recomposes all the chips. That is small for a handful of tools.
        val active by picker.activeToolId.collectAsState()
        val usable by picker.usable.collectAsState()
        val submitting by picker.submitting.collectAsState()
        val error by picker.error.collectAsState()
        Column(modifier, verticalArrangement = Arrangement.spacedBy(4.dp)) {
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                for (id in tools) {
                    val selected = active == id
                    val disabled = submitting || (id !in usable && !selected)
                    FilterChip(
                        selected = selected,
                        onClick = { scope.launch { picker.buttonTapped(id) } },
                        label = { Text(label(id)) },
                        enabled = !disabled,
                        leadingIcon = icon?.let { draw -> { draw(id) } },
                        modifier = Modifier.testTag("toolPicker-${id.value}"),
                    )
                }
            }
            error?.let {
                Text(
                    it.detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.testTag("toolPickerError"),
                )
            }
        }
    }
}

/**
 * A label for the tools the module names in its resources, translated with
 * the app's locale: pointer, pan, ink, highlight and note. Any other tool
 * reads as its id.
 */
@Composable
public fun toolLabel(id: ToolId): String = when (id) {
    ToolId.POINTER -> stringResource(R.string.embedpdf_tool_pointer)
    ToolId.PAN -> stringResource(R.string.embedpdf_tool_pan)
    ToolId.INK -> stringResource(R.string.embedpdf_tool_ink)
    ToolId.HIGHLIGHT -> stringResource(R.string.embedpdf_tool_highlight)
    ToolId.NOTE -> stringResource(R.string.embedpdf_tool_note)
    else -> id.value
}
