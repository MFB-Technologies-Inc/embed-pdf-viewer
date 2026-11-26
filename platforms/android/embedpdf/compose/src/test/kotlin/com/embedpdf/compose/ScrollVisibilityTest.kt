package com.embedpdf.compose

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.layout.Box
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ScrollVisibilityTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun aRowReportsVisibleOnScreenAndNotOnceScrolledAway() {
        val visible = mutableStateMapOf<Int, Boolean>()
        lateinit var list: LazyListState
        compose.setContent {
            list = rememberLazyListState()
            LazyColumn(Modifier.size(200.dp, 300.dp), state = list) {
                items(40) { index ->
                    Box(Modifier.fillMaxWidth().height(100.dp).trackingScrollVisibility { visible[index] = it })
                }
            }
        }
        compose.pumpFrame()
        assertEquals(true, visible[0])
        assertEquals(true, visible[2])
        assertNotEquals(true, visible[10])
        compose.onMain { list.scrollToItem(10) }
        compose.pumpFrame()
        assertEquals(false, visible[0])
        assertEquals(true, visible[10])
    }
}
