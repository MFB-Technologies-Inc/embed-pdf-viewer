package com.embedpdf.compose

import android.os.Looper
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.junit4.ComposeContentTestRule
import com.embedpdf.DocumentInfo
import com.embedpdf.Fixture
import com.embedpdf.PageInfo
import com.embedpdf.PdfViewer
import com.embedpdf.ViewerConfig
import com.embedpdf.started
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import android.graphics.Bitmap
import android.graphics.Canvas
import android.view.View
import androidx.activity.ComponentActivity
import androidx.compose.ui.test.junit4.AndroidComposeTestRule
import androidx.compose.ui.test.onNodeWithTag
import org.robolectric.Shadows.shadowOf
import java.time.Duration

/*
 * Robolectric runs these tests on its main looper, paused: the test thread is
 * the main thread, and a message posted to the looper runs only when the test
 * idles it, and a delayed one only once the looper's clock reaches it. The
 * models keep their state on `Dispatchers.Main`, that looper, and poll with
 * `delay`, while the engine answers on threads of its own in real time. So a
 * test never blocks the main thread on a model call: it pumps instead,
 * sleeping a little real time for the engine and advancing the looper a frame
 * at a time.
 */

/** Runs [block], which calls the facade only, on a background thread and waits for it. Fails after a minute. */
fun <T> engine(block: suspend CoroutineScope.() -> T): T = runBlocking(Dispatchers.IO) { withTimeout(60_000) { block() } }

/** Advances the main looper and Compose a frame at a time until [condition] holds. Fails after [timeoutMs]. */
fun ComposeContentTestRule.pumpUntil(timeoutMs: Long = 20_000, message: () -> String = { "" }, condition: () -> Boolean) {
    val deadline = System.nanoTime() + timeoutMs * 1_000_000
    while (!condition()) {
        check(System.nanoTime() < deadline) { "condition not met after $timeoutMs ms. ${message()}" }
        pumpFrame()
    }
}

/** Lets the engine run for a moment, then advances the main looper and Compose by one frame. */
fun ComposeContentTestRule.pumpFrame() {
    Thread.sleep(5)
    shadowOf(Looper.getMainLooper()).idleFor(Duration.ofMillis(16))
    waitForIdle()
}

/** Pumps until no node matches [matcher], such as a form that closes once its save returns. */
fun ComposeContentTestRule.pumpUntilGone(matcher: SemanticsMatcher, timeoutMs: Long = 20_000) =
    pumpUntil(timeoutMs, { "a node still matches ${matcher.description}" }) {
        onAllNodes(matcher).fetchSemanticsNodes().isEmpty()
    }

/** Pumps for about [millis] of looper time. */
fun ComposeContentTestRule.pumpFor(millis: Long) {
    repeat((millis / 16).toInt().coerceAtLeast(1)) { pumpFrame() }
}

/** Runs [block] on the main dispatcher, as a view's effect would, and pumps until it returns. */
fun <T> ComposeContentTestRule.onMain(block: suspend CoroutineScope.() -> T): T {
    var result: Result<T>? = null
    CoroutineScope(Dispatchers.Main).launch { result = runCatching { block() } }
    pumpUntil { result != null }
    return result!!.getOrThrow()
}

/** A started viewer with one document open. */
class Opened(val viewer: PdfViewer, val document: DocumentInfo, val pages: List<PageInfo>) {
    fun destroy() = engine { viewer.destroy() }
}

fun opened(config: ViewerConfig, fixture: String = "report"): Opened = engine {
    val viewer = started(config)
    val document = viewer.open(Fixture.bytes(fixture))
    Opened(viewer, document, viewer.pages(document))
}

/**
 * What the window draws inside the node tagged [tag], drawn in software
 * through the view hierarchy. Robolectric's window capture waits on a frame
 * the paused looper does not always deliver, so this draws the root view
 * itself.
 */
fun AndroidComposeTestRule<*, out ComponentActivity>.drawnImage(tag: String): Bitmap {
    waitForIdle()
    val bounds = onNodeWithTag(tag).fetchSemanticsNode().boundsInRoot
    val root = activity.window.decorView.findViewById<View>(android.R.id.content)
    val whole = Bitmap.createBitmap(root.width, root.height, Bitmap.Config.ARGB_8888)
    runOnIdle { root.draw(Canvas(whole)) }
    return Bitmap.createBitmap(
        whole,
        bounds.left.toInt(),
        bounds.top.toInt(),
        bounds.width.toInt().coerceAtMost(whole.width - bounds.left.toInt()),
        bounds.height.toInt().coerceAtMost(whole.height - bounds.top.toInt()),
    )
}

/** Whether any pixel of the image is dark: a page's text, where a blank or unpainted area has none. */
fun Bitmap.hasInk(): Boolean {
    for (y in 0 until height step 2) {
        for (x in 0 until width step 2) {
            val color = getPixel(x, y)
            val dark = (color ushr 16 and 0xff) < 128 && (color ushr 8 and 0xff) < 128 && (color and 0xff) < 128
            if ((color ushr 24) > 128 && dark) return true
        }
    }
    return false
}

/** A node TalkBack can name: it has a content description or text. */
fun hasLabel(): androidx.compose.ui.test.SemanticsMatcher =
    androidx.compose.ui.test.SemanticsMatcher("has a content description or text") {
        it.config.contains(androidx.compose.ui.semantics.SemanticsProperties.ContentDescription) ||
            it.config.contains(androidx.compose.ui.semantics.SemanticsProperties.Text)
    }
