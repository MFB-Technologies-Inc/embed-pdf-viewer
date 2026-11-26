package com.embedpdf.compose

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import com.embedpdf.models.HistoryModel
import kotlinx.coroutines.launch

/**
 * Undo and redo buttons over a [HistoryModel], titled with the model's
 * `undoTitle` and `redoTitle`, with the model's error and notice below. A
 * button is disabled while its side of the history is empty and while an
 * undo or redo is out, and TalkBack reads it as disabled then.
 *
 * Give the model resource-backed strings, from [rememberHistoryStrings], so
 * the titles and the notice speak the app's locale. The controls run the
 * model while they are composed, so the app does not run it as well. The
 * buttons flow onto a second line when they don't fit, as at the largest
 * font scales.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
public fun HistoryControls(model: HistoryModel, modifier: Modifier = Modifier) {
    ModelScope(model) { history ->
        val scope = rememberCoroutineScope()
        LaunchedEffect(history) { history.run() }
        val canUndo by history.canUndo.collectAsState()
        val canRedo by history.canRedo.collectAsState()
        val submitting by history.submitting.collectAsState()
        val undoTitle by history.undoTitle.collectAsState()
        val redoTitle by history.redoTitle.collectAsState()
        val error by history.error.collectAsState()
        val notice by history.noticeText.collectAsState()
        Column(modifier, verticalArrangement = Arrangement.spacedBy(4.dp)) {
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                HistoryButton(undoTitle, R.drawable.embedpdf_ic_undo, canUndo && !submitting, "historyUndo") {
                    scope.launch { history.undo() }
                }
                HistoryButton(redoTitle, R.drawable.embedpdf_ic_redo, canRedo && !submitting, "historyRedo") {
                    scope.launch { history.redo() }
                }
            }
            error?.let {
                Text(
                    it.detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.testTag("historyError"),
                )
            }
            notice?.let {
                Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Composable
private fun HistoryButton(title: String, icon: Int, enabled: Boolean, tag: String, onClick: () -> Unit) {
    TextButton(onClick = onClick, enabled = enabled, modifier = Modifier.testTag(tag)) {
        Icon(painterResource(icon), contentDescription = null, modifier = Modifier.size(ButtonDefaults.IconSize))
        Spacer(Modifier.width(ButtonDefaults.IconSpacing))
        Text(title)
    }
}
