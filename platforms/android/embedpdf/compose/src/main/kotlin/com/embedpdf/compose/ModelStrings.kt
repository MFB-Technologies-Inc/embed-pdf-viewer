package com.embedpdf.compose

import android.content.res.Resources
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalResources
import com.embedpdf.models.HistoryNotice
import com.embedpdf.models.HistoryStrings
import com.embedpdf.models.SearchStrings
import java.text.NumberFormat
import java.util.Locale

/**
 * [HistoryStrings] read from this module's string resources, so a
 * `HistoryModel` speaks the app's locale. Translations override the
 * `embedpdf_history_*` strings.
 */
public class ResourceHistoryStrings(private val resources: Resources) : HistoryStrings {
    override val undo: String get() = resources.getString(R.string.embedpdf_history_undo)
    override val redo: String get() = resources.getString(R.string.embedpdf_history_redo)
    override val change: String get() = resources.getString(R.string.embedpdf_history_change)

    override fun undoTitle(label: String): String = resources.getString(R.string.embedpdf_history_undo_title, label)

    override fun redoTitle(label: String): String = resources.getString(R.string.embedpdf_history_redo_title, label)

    override fun notice(notice: HistoryNotice): String = when (notice) {
        is HistoryNotice.SkippedUndo -> resources.getString(R.string.embedpdf_history_skipped_undo)
        is HistoryNotice.SkippedRedo -> resources.getString(R.string.embedpdf_history_skipped_redo)
    }
}

/**
 * [SearchStrings] read from this module's string resources. The numbers
 * group digits as the resources' locale does: "1,234 of 5,678" in English.
 */
public class ResourceSearchStrings(private val resources: Resources) : SearchStrings {
    override fun searchSummary(index: Int, count: Int, searching: Boolean): String {
        val locale = resources.configuration.locales[0] ?: Locale.getDefault()
        val numbers = NumberFormat.getIntegerInstance(locale)
        val id = if (searching) R.string.embedpdf_search_summary_searching else R.string.embedpdf_search_summary
        return resources.getString(id, numbers.format(index), numbers.format(count))
    }
}

/** [ResourceHistoryStrings] for the current resources, made again when the configuration changes. */
@Composable
public fun rememberHistoryStrings(): HistoryStrings {
    val resources = LocalResources.current
    val configuration = LocalConfiguration.current
    return remember(resources, configuration) { ResourceHistoryStrings(resources) }
}

/** [ResourceSearchStrings] for the current resources, made again when the configuration changes. */
@Composable
public fun rememberSearchStrings(): SearchStrings {
    val resources = LocalResources.current
    val configuration = LocalConfiguration.current
    return remember(resources, configuration) { ResourceSearchStrings(resources) }
}
