package com.embedpdf.compose

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.test.core.app.ApplicationProvider
import android.content.Context
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.models.HistoryNotice
import com.embedpdf.models.HistoryStrings
import com.embedpdf.models.SearchStrings
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ModelStringsTest {
    @get:Rule
    val compose = createComposeRule()

    private val resources get() = ApplicationProvider.getApplicationContext<Context>().resources

    /** The English resources say what the models' English defaults say. */
    @Test
    fun theEnglishResourcesMatchTheModelDefaults() {
        val strings = ResourceHistoryStrings(resources)
        val english = HistoryStrings.English
        assertEquals(english.undo, strings.undo)
        assertEquals(english.redo, strings.redo)
        assertEquals(english.change, strings.change)
        assertEquals(english.undoTitle("Rotate pages"), strings.undoTitle("Rotate pages"))
        assertEquals(english.redoTitle("Rotate pages"), strings.redoTitle("Rotate pages"))
        for (notice in listOf(HistoryNotice.SkippedUndo(2), HistoryNotice.SkippedRedo(1))) {
            assertEquals(english.notice(notice), strings.notice(notice))
        }
        val search = ResourceSearchStrings(resources)
        for (searching in listOf(false, true)) {
            assertEquals(SearchStrings.English.searchSummary(3, 12, searching), search.searchSummary(3, 12, searching))
        }
        assertEquals("1,234 of 5,678+", search.searchSummary(1234, 5678, true))
    }

    /** The numbers group as the resources' locale does, not the JVM default. */
    @Test
    @Config(qualifiers = "de")
    fun theSummaryGroupsDigitsAsTheResourcesLocaleDoes() {
        assertEquals("1.234 of 5.678", ResourceSearchStrings(resources).searchSummary(1234, 5678, false))
    }

    @Test
    fun theRememberedStringsReadTheResources() {
        var history: HistoryStrings? = null
        var search: SearchStrings? = null
        compose.setContent {
            history = rememberHistoryStrings()
            search = rememberSearchStrings()
        }
        compose.waitForIdle()
        assertTrue(history is ResourceHistoryStrings)
        assertTrue(search is ResourceSearchStrings)
        assertEquals("Undo", history!!.undo)
    }
}
