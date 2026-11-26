package com.embedpdf.compose

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import com.embedpdf.SearchHit
import com.embedpdf.models.SearchModel
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

/**
 * A search field over a [SearchModel]: the text field, previous and next
 * buttons, the model's `summary` and a clear button, with the model's
 * `error` below. The keyboard's search action runs
 * [SearchModel.searchSubmitted]. Previous and next are disabled while the
 * model has no hits. The clear button empties the field and ends the
 * session; it is disabled while [SearchModel.canClear] is false.
 *
 * The bar runs the model while it is composed. An app does not also run a
 * model it gives to a `SearchBar`: a second `run()` on one model supersedes
 * the first.
 *
 * [onActiveHit] is called whenever the bar observes the model's
 * `activeHit` change to a hit after the bar appears: the first hit of a new
 * search, each previous or next step, a move made through the
 * `SearchCapability` or by the plugin's own rerun after an edit. A hit the
 * model already holds when the bar appears is not reported, and a query
 * submitted again whose first hit equals the hit active before it may or may
 * not be. It is not called when the hits clear. A scroll made in this
 * callback should therefore be safe to repeat and not the only way back to
 * the active hit.
 *
 * The buttons sit after the field, and under it at font scales of 1.5 and
 * up. The bar mirrors under a right-to-left layout.
 */
@Composable
public fun SearchBar(
    model: SearchModel,
    modifier: Modifier = Modifier,
    onActiveHit: (SearchHit) -> Unit = {},
) {
    ModelScope(model) { search ->
        val scope = rememberCoroutineScope()
        val reported by rememberUpdatedState(onActiveHit)
        LaunchedEffect(search) { search.run() }
        // The value held at appearance is the old search's, so it is skipped.
        LaunchedEffect(search) { search.activeHit.drop(1).filterNotNull().collect { reported(it) } }
        // Performance: this one scope reads the text, the summary, the hit
        // count, `canClear` and the error, so each keystroke recomposes the
        // whole bar. That is one row of five controls.
        val text by search.textFlow.collectAsState()
        val summary by search.summary.collectAsState()
        val hitCount by search.hitCount.collectAsState()
        val canClear by search.canClear.collectAsState()
        val error by search.error.collectAsState()
        Column(modifier, verticalArrangement = Arrangement.spacedBy(4.dp)) {
            val field: @Composable (Modifier) -> Unit = { fieldModifier ->
                OutlinedTextField(
                    value = text,
                    onValueChange = { search.text = it },
                    modifier = fieldModifier.testTag("searchField"),
                    placeholder = { Text(stringResource(R.string.embedpdf_search)) },
                    leadingIcon = { Icon(painterResource(R.drawable.embedpdf_ic_search), contentDescription = null) },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(autoCorrectEnabled = false, imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { scope.launch { search.searchSubmitted() } }),
                )
            }
            val buttons: @Composable () -> Unit = {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    summary?.let {
                        Text(
                            it,
                            style = MaterialTheme.typography.labelMedium.copy(fontFeatureSettings = TABULAR_DIGITS),
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.testTag("searchSummary"),
                        )
                    }
                    IconButton(
                        onClick = { scope.launch { search.previousButtonTapped() } },
                        enabled = hitCount > 0,
                        modifier = Modifier.testTag("searchPrevious"),
                    ) {
                        Icon(painterResource(R.drawable.embedpdf_ic_arrow_up), stringResource(R.string.embedpdf_search_previous))
                    }
                    IconButton(
                        onClick = { scope.launch { search.nextButtonTapped() } },
                        enabled = hitCount > 0,
                        modifier = Modifier.testTag("searchNext"),
                    ) {
                        Icon(painterResource(R.drawable.embedpdf_ic_arrow_down), stringResource(R.string.embedpdf_search_next))
                    }
                    IconButton(
                        onClick = {
                            search.text = ""
                            scope.launch { search.clearButtonTapped() }
                        },
                        enabled = canClear,
                        modifier = Modifier.testTag("searchClear"),
                    ) {
                        Icon(painterResource(R.drawable.embedpdf_ic_close), stringResource(R.string.embedpdf_search_clear))
                    }
                }
            }
            // At the largest font scales the buttons go under the field, as
            // Swift stacks them at the accessibility text sizes.
            if (LocalDensity.current.fontScale >= LARGE_FONT_SCALE) {
                field(Modifier.fillMaxWidth())
                buttons()
            } else {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(4.dp)) {
                    field(Modifier.weight(1f))
                    buttons()
                }
            }
            error?.let {
                Text(
                    it.detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.testTag("searchError"),
                )
            }
        }
    }
}

/** The font scale from which controls stack, Swift's accessibility text sizes. */
internal const val LARGE_FONT_SCALE: Float = 1.5f

/** Digits of one width, so the summary doesn't jitter as it counts. */
private const val TABULAR_DIGITS: String = "tnum"
