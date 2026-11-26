package com.embedpdf.compose

import androidx.activity.ComponentActivity
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.unit.dp
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.PageEditBlankOptions
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.Rect
import com.embedpdf.RenderConfig
import com.embedpdf.RenderViewDemand
import com.embedpdf.Size
import com.embedpdf.ViewerConfig
import com.embedpdf.models.BitmapPool
import com.embedpdf.models.TiledPageModel
import com.embedpdf.pageEdit
import com.embedpdf.render
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode

@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
@GraphicsMode(GraphicsMode.Mode.NATIVE)
class TiledPageImageTest {
    @get:Rule
    val compose = createAndroidComposeRule<ComponentActivity>()

    private class Selection(first: TiledPageModel) {
        var model by mutableStateOf(first)
    }

    private fun PageInfo.size() = Size(width, height)

    /** Whether the page has no demand left: nothing engaged, painted or in flight. */
    private fun released(demand: RenderViewDemand, page: PageRef): Boolean = engine {
        val plan = demand.plan(page)
        !plan.engaged && plan.paint.isEmpty() && plan.fetching.isEmpty()
    }

    /** `TiledPageImageLifecycleTests`. */
    @Test
    fun replacingTheModelWithEqualDemandUpdatesTheNewPageAndReleasesTheOldPage() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            assertTrue(o.pages.size >= 2)
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("c8-model-replacement") }
            val old = TiledPageModel(render, demand, o.pages[0].page)
            val new = TiledPageModel(render, demand, o.pages[1].page)
            val selection = Selection(old)
            compose.setContent {
                // The structural position, scale, rect and revision stay the same.
                TiledPageImage(selection.model, o.pages[0].size(), 8.0, null, Modifier.size(320.dp, 480.dp))
            }
            compose.pumpUntil { old.base.value != null && old.tiles.value.isNotEmpty() }
            assertTrue(engine { demand.plan(old.page).engaged })

            compose.runOnIdle { selection.model = new }

            compose.pumpUntil { new.base.value != null }
            compose.pumpUntil { released(demand, old.page) && old.tiles.value.isEmpty() }
            compose.onMain { new.release() }
            engine { demand.dispose() }
        } finally {
            o.destroy()
        }
    }

    @Test
    fun theBaseAndTilesAreDrawnWhereThePageIs() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("pixels") }
            val model = TiledPageModel(render, demand, o.pages[0].page)
            val phases = mutableListOf<TiledPagePhase>()
            compose.setContent {
                Box(Modifier.size(306.dp, 396.dp).testTag("page")) {
                    TiledPageImage(model, o.pages[0].size(), 2.0, null, Modifier.fillMaxSize(), onPhase = { phases += it })
                }
            }
            compose.pumpUntil { TiledPagePhase.SETTLED in phases }
            assertEquals(TiledPagePhase.BEGAN, phases.first())
            assertTrue(compose.drawnImage("page").hasInk())
        } finally {
            o.destroy()
        }
    }

    /** Review fix 6: a new page size starts the update again, as a new scale does. */
    @Test
    fun aNewPageSizeUpdatesAgain() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("pixels") }
            val model = TiledPageModel(render, demand, o.pages[0].page)
            val phases = mutableListOf<TiledPagePhase>()
            var size by mutableStateOf(o.pages[0].size())
            compose.setContent {
                Box(Modifier.size(306.dp, 396.dp)) {
                    TiledPageImage(model, size, 2.0, null, Modifier.fillMaxSize(), onPhase = { phases += it })
                }
            }
            compose.pumpUntil { TiledPagePhase.SETTLED in phases }
            val began = phases.count { it == TiledPagePhase.BEGAN }
            size = Size(size.width * 2, size.height * 2)
            compose.pumpUntil { phases.count { it == TiledPagePhase.BEGAN } > began }
        } finally {
            o.destroy()
        }
    }

    /**
     * Tiles that leave go back to the pool once the drawn frames or the time
     * backstop allow, and leaving the composition releases the page and hands
     * its last tiles back too.
     */
    @Test
    fun retiredTilesReachThePoolAndLeavingReleasesThePage() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("pool") }
            val pool = BitmapPool(64L * 1024 * 1024)
            val model = TiledPageModel(render, demand, o.pages[0].page, pool = pool)
            val page = o.pages[0]
            var visible by mutableStateOf<Rect?>(Rect(0.0, 0.0, page.width / 2, page.height / 4))
            var shown by mutableStateOf(true)
            compose.setContent {
                if (shown) TiledPageImage(model, page.size(), 8.0, visible, Modifier.size(320.dp, 414.dp))
            }
            compose.pumpUntil { model.tiles.value.isNotEmpty() }
            val tileBytes = model.tiles.value.sumOf { it.image.allocationByteCount.toLong() }
            // Built but off screen: every tile leaves.
            compose.runOnIdle { visible = Rect(0.0, 0.0, 0.0, 0.0) }
            compose.pumpUntil { model.tiles.value.isEmpty() && pool.pooledBytes >= tileBytes }

            compose.runOnIdle { visible = null }
            compose.pumpUntil { model.tiles.value.isNotEmpty() }
            val before = pool.pooledBytes
            compose.runOnIdle { shown = false }
            compose.pumpUntil { released(demand, page.page) && model.tiles.value.isEmpty() && pool.pooledBytes > before }
            engine { demand.dispose() }
        } finally {
            o.destroy()
        }
    }

    /**
     * Review focus: a fast scroll through 500 pages, each a tiled image over
     * one pool, keeps the pool within its budget at every step, reuses its
     * bitmaps, and leaves no page behind the viewport holding tiles.
     */
    @Test
    fun aFastScrollThroughFiveHundredPagesKeepsThePoolWithinItsBudget() {
        val o = opened(ViewerConfig(render = RenderConfig(), pageEdit = true))
        try {
            val pages = engine {
                val edit = o.viewer.pageEdit(o.document)
                var missing = 500 - o.pages.size
                // At most a hundred a call.
                while (missing > 0) {
                    val count = minOf(missing, 100)
                    edit.insertBlank(PageEditBlankOptions(count = count.toUInt()))
                    missing -= count
                }
                o.viewer.pages(o.document)
            }
            assertEquals(500, pages.size)
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("scroll") }
            val budget = 8L * 1024 * 1024
            val pool = BitmapPool(budget)
            val models = HashMap<PageRef, TiledPageModel>()
            lateinit var list: LazyListState
            compose.setContent {
                list = rememberLazyListState()
                LazyColumn(Modifier.size(320.dp, 480.dp), state = list) {
                    items(pages, key = { it.page.pageObjectNumber.toLong() }) { page ->
                        val model = remember(page.page) { models.getOrPut(page.page) { TiledPageModel(render, demand, page.page, pool = pool) } }
                        TiledPageImage(
                            model,
                            page.size(),
                            3.0,
                            null,
                            Modifier.fillMaxWidth().aspectRatio((page.width / page.height).toFloat()),
                        )
                    }
                }
            }
            var peak = 0L
            // The first pages land their tiles, so leaving them has tiles to give back.
            compose.pumpUntil { models.values.any { it.tiles.value.isNotEmpty() } }
            for (index in 0 until pages.size step 7) {
                compose.onMain { list.scrollToItem(index) }
                // Long enough for some tiles to land before the next jump.
                compose.pumpFor(100)
                peak = maxOf(peak, pool.pooledBytes)
                assertTrue("${pool.pooledBytes} pooled at page $index", pool.pooledBytes <= budget)
            }
            compose.pumpFor(600)
            assertTrue(pool.pooledBytes <= budget)
            // Pages that left gave their tiles back.
            assertTrue("peak $peak", peak in 1..budget)
            // Only the pages still composed hold tiles.
            val composed = compose.runOnIdle { list.layoutInfo.visibleItemsInfo.map { it.key } }.toSet()
            val holding = models.filterValues { it.tiles.value.isNotEmpty() }.keys.map { it.pageObjectNumber.toLong() }
            assertTrue("$holding outside $composed", composed.containsAll(holding))
            engine { demand.dispose() }
        } finally {
            o.destroy()
        }
    }

    /**
     * Review focus: recreating the activity while a page renders disposes its
     * composition without a crash, and the page's work ends: its demand is
     * released and no tile lands afterwards.
     */
    @Test
    fun recreatingTheActivityMidRenderLeavesNoRenderWork() {
        val o = opened(ViewerConfig(render = RenderConfig()))
        try {
            val render = engine { o.viewer.render(o.document) }
            val demand = engine { render.viewDemand("recreate") }
            val model = TiledPageModel(render, demand, o.pages[0].page)
            val phases = mutableListOf<TiledPagePhase>()
            compose.setContent {
                TiledPageImage(model, o.pages[0].size(), 8.0, null, Modifier.size(320.dp, 414.dp), onPhase = { phases += it })
                PageImageView(render, o.pages[1].page, 4.0, Modifier.size(100.dp))
            }
            compose.pumpUntil { TiledPagePhase.BEGAN in phases }
            compose.activityRule.scenario.recreate()
            compose.pumpUntil { released(demand, model.page) && model.tiles.value.isEmpty() }
            // Nothing lands later.
            compose.pumpFor(500)
            assertTrue(model.tiles.value.isEmpty())
            assertTrue(released(demand, model.page))
            engine { demand.dispose() }
        } finally {
            o.destroy()
        }
    }
}
