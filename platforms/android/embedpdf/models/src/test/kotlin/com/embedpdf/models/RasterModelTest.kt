package com.embedpdf.models

import android.graphics.Bitmap
import android.graphics.PointF
import com.embedpdf.EmbedPdfTestRunner
import com.embedpdf.Fixture
import com.embedpdf.PageEditRotation
import com.embedpdf.PageInfo
import com.embedpdf.PageRef
import com.embedpdf.PdfException
import com.embedpdf.PdfViewer
import com.embedpdf.Rect
import com.embedpdf.RenderCapability
import com.embedpdf.RenderConfig
import com.embedpdf.RenderInvalidateScope
import com.embedpdf.StagePageTransform
import com.embedpdf.StageVisiblePage
import com.embedpdf.ViewerConfig
import com.embedpdf.pageEdit
import com.embedpdf.render
import com.embedpdf.started
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.launch
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNotSame
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

/**
 * The models that make bitmaps or take `PointF`: `ThumbnailModelTests`,
 * `TiledPageModelTests`, `StageVisiblePageGeometryTests` and the bitmap
 * pool, under Robolectric for the Android graphics classes.
 */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class RasterModelTest {
    @get:Rule
    val main = MainThreadRule()

    private class Opened(val viewer: PdfViewer, val pages: List<PageInfo>, val render: RenderCapability)

    private suspend fun opened(): Opened {
        val viewer = started(ViewerConfig(render = RenderConfig(), pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        return Opened(viewer, viewer.pages(document), viewer.render(document))
    }

    @Test
    fun aLoadedThumbnailIsTurnedToThePageAsShown() = mainTest {
        val viewer = started(ViewerConfig(render = RenderConfig(), pageEdit = true))
        val document = viewer.open(Fixture.bytes("report"))
        val unturned = viewer.pages(document)
        assertTrue(unturned[0].height > unturned[0].width)
        viewer.pageEdit(document).setRotation(listOf(unturned[0].page), PageEditRotation.CLOCKWISE90)
        val pages = viewer.pages(document)
        assertEquals(90u, pages[0].rotation)
        val model = ThumbnailModel(viewer.render(document), pages)
        assertNull(model.image(pages[0].page))
        model.load(pages[0].page)
        val turned = checkNotNull(model.image(pages[0].page))
        assertTrue(turned.width > turned.height)
        model.load(pages[1].page)
        val upright = checkNotNull(model.image(pages[1].page))
        assertTrue(upright.height > upright.width)
        assertNull(model.error.value)
        viewer.destroy()
    }

    @Test
    fun anInvalidationMakesTheNextLoadRenderAgain() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        val running = launch(start = CoroutineStart.UNDISPATCHED) { model.run() }
        val page = o.pages[0].page
        model.load(page)
        assertEquals(1, model.renderCount)
        // The revision is the same, so the cached thumbnail answers.
        model.load(page)
        assertEquals(1, model.renderCount)
        expectObserved({ model.revision(page) }, 1L) { o.render.invalidate(listOf(page), RenderInvalidateScope.ANNOTATIONS) }
        model.load(page)
        assertEquals(2, model.renderCount)
        assertNotNull(model.image(page))
        running.cancel()
        o.viewer.destroy()
    }

    @Test
    fun twoLoadsOfOnePageShareOneRender() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        val page = o.pages[0].page
        listOf(async { model.load(page) }, async { model.load(page) }).awaitAll()
        assertEquals(1, model.renderCount)
        assertNotNull(model.image(page))
        o.viewer.destroy()
    }

    /** With `run()` going, the render outlives a cancelled waiter and lands for the other. */
    @Test
    fun aCancelledWaiterLeavesTheRenderToTheOtherWaiter() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        val running = launch(start = CoroutineStart.UNDISPATCHED) { model.run() }
        val page = o.pages[0].page
        val reached = CompletableDeferred<Unit>()
        val gate = CompletableDeferred<Unit>()
        model.renderStarted = {
            reached.complete(Unit)
            gate.await()
        }
        val first = launch { model.load(page) }
        val second = launch { model.load(page) }
        reached.await()
        expectObserved({ model.waiterCount(page) }, 2)
        first.cancel()
        first.join()
        assertEquals(1, model.waiterCount(page))
        gate.complete(Unit)
        second.join()
        assertNotNull(model.image(page))
        assertEquals(1, model.renderCount)
        running.cancel()
        o.viewer.destroy()
    }

    @Test
    fun aLoadCancelledInFlightLeavesNoImageAndNoError() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        val page = o.pages[0].page
        val reached = CompletableDeferred<Unit>()
        model.renderStarted = {
            reached.complete(Unit)
            CompletableDeferred<Unit>().await()
        }
        val loading = launch { model.load(page) }
        reached.await()
        loading.cancel()
        loading.join()
        assertNull(model.image(page))
        assertNull(model.error.value)
        assertEquals(0, model.waiterCount(page))
        o.viewer.destroy()
    }

    @Test
    fun aPageOutsideTheModelFailsWithoutRendering() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        model.load(PageRef(pageObjectNumber = UInt.MAX_VALUE))
        assertEquals(PdfException.Code.NOT_FOUND, model.error.value?.code)
        assertEquals(0, model.renderCount)
        o.viewer.destroy()
    }

    @Test
    fun theCacheDropsThePageLoadedLongestAgo() = mainTest {
        val o = opened()
        val model = ThumbnailModel(o.render, o.pages)
        model.cacheLimit = 2
        model.load(o.pages[0].page)
        model.load(o.pages[1].page)
        // A use moves page 0 to the recent end.
        model.load(o.pages[0].page)
        model.load(o.pages[2].page)
        assertNotNull(model.image(o.pages[0].page))
        assertNull(model.image(o.pages[1].page))
        assertNotNull(model.image(o.pages[2].page))
        o.viewer.destroy()
    }

    private suspend fun withTiledModel(pool: BitmapPool? = null, body: suspend (TiledPageModel, PageInfo) -> Unit) {
        val o = opened()
        val demand = o.render.viewDemand("tiled-pages")
        val model = TiledPageModel(o.render, demand, o.pages[0].page, pool = pool)
        try {
            body(model, o.pages[0])
        } finally {
            model.release()
            demand.dispose()
            o.viewer.destroy()
        }
    }

    @Test
    fun smallDemandOnlyNeedsTheBase() = mainTest {
        withTiledModel { model, page ->
            model.update(256 / page.width, page.width, null)
            assertNotNull(model.base.value)
            assertTrue(model.tiles.value.isEmpty())
        }
    }

    @Test
    fun visibleDeepDemandAddsDetailOverTheBudgetedBase() = mainTest {
        withTiledModel { model, page ->
            val visible = Rect(0.0, 0.0, page.width / 2, page.height / 4)
            var painted = false
            model.update(8.0, page.width, visible) { painted = true }
            assertTrue(checkNotNull(model.base.value).width <= 640)
            assertTrue(model.tiles.value.isNotEmpty())
            for (tile in model.tiles.value) {
                assertTrue(tile.rect.x < visible.width + 1)
                assertTrue(tile.rect.y < visible.height + 1)
            }
            assertTrue(painted)
        }
    }

    @Test
    fun offscreenDemandRetiresTilesIntoThePoolAndReturningRestoresThem() = mainTest {
        val pool = BitmapPool(64L * 1024 * 1024)
        withTiledModel(pool) { model, page ->
            val visible = Rect(0.0, 0.0, page.width / 2, page.height / 4)
            model.update(8.0, page.width, visible)
            assertTrue(model.tiles.value.isNotEmpty())
            model.update(256 / page.width, page.width, Rect(0.0, 0.0, 0.0, 0.0))
            assertTrue(model.tiles.value.isEmpty())
            assertTrue(checkNotNull(model.base.value).width <= 256)
            // The retired tiles and base went back to the pool a reconcile later.
            assertTrue(pool.pooledBytes > 0)
            model.update(8.0, page.width, visible)
            assertTrue(model.tiles.value.isNotEmpty())
        }
    }

    /** Without frame acknowledgements a retired bitmap waits two reconciles and a quarter second. */
    @Test
    fun withoutFrameAcknowledgementsARetiredBaseWaitsBeforeItIsPooled() = mainTest {
        val pool = BitmapPool(64L * 1024 * 1024)
        withTiledModel(pool) { model, page ->
            model.update(128 / page.width, page.width, null)
            val first = checkNotNull(model.base.value)
            var pooledAtFirstReconcile: Long? = null
            model.reconciled = { if (pooledAtFirstReconcile == null) pooledAtFirstReconcile = pool.pooledBytes }
            // The new update restarts the poll loop; its first reconcile comes
            // straight after the base it replaced left the model.
            model.update(256 / page.width, page.width, null)
            assertNotSame(first, model.base.value)
            assertEquals(0L, pooledAtFirstReconcile)
            // The update polls on until the held bitmap has gone back.
            assertEquals(first.allocationByteCount.toLong(), pool.pooledBytes)
        }
    }

    /** With frame acknowledgements a retired bitmap waits for two frames after it left, however many reconciles pass. */
    @Test
    fun withFrameAcknowledgementsARetiredBaseWaitsForTwoFrames() = mainTest {
        val pool = BitmapPool(64L * 1024 * 1024)
        withTiledModel(pool) { model, page ->
            model.frameDisplayed()
            model.update(128 / page.width, page.width, null)
            val first = checkNotNull(model.base.value)
            model.update(256 / page.width, page.width, null)
            model.update(256 / page.width, page.width, null)
            assertNotSame(first, model.base.value)
            assertEquals(0L, pool.pooledBytes)
            // The first frame may have drawn the old base; the second did not.
            model.frameDisplayed()
            assertEquals(0L, pool.pooledBytes)
            model.frameDisplayed()
            assertEquals(first.allocationByteCount.toLong(), pool.pooledBytes)
        }
    }

    @Test
    fun releaseDropsTilesAndKeepsTheBase() = mainTest {
        withTiledModel { model, page ->
            model.update(8.0, page.width, Rect(0.0, 0.0, page.width / 2, page.height / 4))
            assertTrue(model.tiles.value.isNotEmpty())
            model.release()
            assertTrue(model.tiles.value.isEmpty())
            assertNotNull(model.base.value)
        }
    }

    @Test
    fun paintAcknowledgementsDoNotDropTheCurrentTiles() = mainTest {
        withTiledModel { model, page ->
            model.update(8.0, page.width, Rect(0.0, 0.0, page.width / 2, page.height / 4))
            val tiles = model.tiles.value
            assertTrue(tiles.isNotEmpty())
            for (tile in tiles) model.tileDisplayed(tile.id)
            assertTrue(model.tiles.value.isNotEmpty())
        }
    }

    @Test
    fun aPoolReusesABitmapOfTheSameSizeWithinItsBudget() {
        val pool = BitmapPool(maxBytes = 2L * 100 * 100 * 4)
        val first = pool.acquire(100, 100)
        pool.release(first)
        assertEquals(first.allocationByteCount.toLong(), pool.pooledBytes)
        assertSame(first, pool.acquire(100, 100))
        assertEquals(0L, pool.pooledBytes)
        // Past the budget the oldest goes.
        val a = pool.acquire(100, 100)
        val b = pool.acquire(100, 100)
        val c = pool.acquire(100, 100)
        pool.release(a)
        pool.release(b)
        pool.release(c)
        assertEquals(2L * a.allocationByteCount, pool.pooledBytes)
        // An immutable bitmap, or one larger than the budget, is not kept.
        pool.clear()
        pool.release(Bitmap.createBitmap(IntArray(4), 2, 2, Bitmap.Config.ARGB_8888))
        pool.release(Bitmap.createBitmap(200, 200, Bitmap.Config.ARGB_8888))
        assertEquals(0L, pool.pooledBytes)
    }

    @Test
    fun aRenderedPageIntoAPooledBitmapKeepsItsPixels() = mainTest {
        val o = opened()
        val rendered = o.render.renderThumbnail(o.pages[0].page)
        val pool = BitmapPool(16L * 1024 * 1024)
        val pooled = rendered.image.toBitmap(pool)
        val plain = rendered.image.toBitmap()
        assertEquals(plain.width, pooled.width)
        assertEquals(plain.getPixel(10, 10), pooled.getPixel(10, 10))
        assertEquals(rendered.image.pixel(10, 10), pooled.getPixel(10, 10))
        o.viewer.destroy()
    }

    private val info = PageInfo(page = PageRef(pageObjectNumber = 1u), index = 0u, width = 612.0, height = 792.0, rotation = 0u)

    private fun visible(rotation: Double, viewWidth: Double): StageVisiblePage {
        val transform = StagePageTransform(
            rotation = rotation, viewWidth = viewWidth, viewHeight = viewWidth * 792 / 612, contentWidth = 612.0,
            contentHeight = 792.0, deviceWidth = viewWidth * 2, deviceHeight = viewWidth * 2 * 792 / 612, renderScale = 2.0,
            viewScale = 1.0, baseScale = 1.0, zoom = 1.0,
        )
        return StageVisiblePage(
            page = info.page, pageIndex = 0u, x = 0.0, y = 0.0, width = 612.0, height = 792.0, rotation = rotation,
            contentScale = 1.0, screenX = 10.0, screenY = 20.0, transform = transform, visibleRect = Rect(0.0, 0.0, 612.0, 792.0),
        )
    }

    @Test
    fun aZeroWidthMapsToZeroNotToInfinity() {
        val page = visible(0.0, 0.0)
        val point = page.pagePoint(PointF(50f, 50f), info)
        assertTrue(point.x.isFinite() && point.y.isFinite())
        val rect = page.localRect(Rect(10.0, 10.0, 100.0, 100.0), info)
        assertEquals(0f, rect.width())
    }

    @Test
    fun geometryTakesItsTurnFromTheStageAndAQuarterTurnComesBack() {
        val page = visible(90.0, 396.0)
        assertEquals(90, page.geometry(info).rotation)
        val start = com.embedpdf.Point(100.0, 200.0)
        val local = page.localRect(Rect(start.x, start.y, 0.0, 0.0), info)
        val back = page.pagePoint(PointF(local.left, local.top), info)
        assertEquals(start.x, back.x, 0.01)
        assertEquals(start.y, back.y, 0.01)
    }

    @Test
    fun unturnedPagePointScalesByTheDrawnSizeAndViewportPointAddsTheOrigin() {
        val page = visible(0.0, 306.0)
        val point = page.pagePoint(PointF(153f, 0f), info)
        assertEquals(306.0, point.x, 0.01)
        val viewport = page.viewportPoint(PointF(5f, 6f))
        assertEquals(15.0, viewport.x, 0.001)
        assertEquals(26.0, viewport.y, 0.001)
        assertFalse(viewport.x.isNaN())
    }
}
