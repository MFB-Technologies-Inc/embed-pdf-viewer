package com.embedpdf.compose

import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.referentialEqualityPolicy
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.junit4.createComposeRule
import com.embedpdf.EmbedPdfTestRunner
import kotlinx.coroutines.awaitCancellation
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config

/**
 * `PanelPresentationTests`: replacing the model a panel shows drops what the
 * old model's panel held and ends its lifetime. Swift drives two outline
 * models; the scope's behaviour does not depend on the model, so plain
 * objects stand in, which also covers models whose `equals` says equal.
 */
@RunWith(EmbedPdfTestRunner::class)
@Config(sdk = [35])
class ModelScopeTest {
    @get:Rule
    val compose = createComposeRule()

    /** Equal to every other, so only identity tells two apart. */
    private class Model(val name: String) {
        val saved = mutableListOf<String>()

        override fun equals(other: Any?): Boolean = other is Model

        override fun hashCode(): Int = 0
    }

    private class Harness(first: Model) {
        // Referential, so swapping in an equal model is a change.
        var model by mutableStateOf(first, referentialEqualityPolicy())
        val started = mutableListOf<String>()
        val ended = mutableListOf<String>()
        var open: (() -> Unit)? = null
        var save: (() -> Unit)? = null
    }

    @Test
    fun modelReplacementClearsPendingActionsAndCancelsTheOldLifetime() {
        val first = Model("first")
        val second = Model("second")
        val harness = Harness(first)
        compose.setContent {
            ModelScope(harness.model) { model ->
                var pending by remember { mutableStateOf<String?>(null) }
                harness.open = { pending = "${model.name} edit" }
                harness.save = { pending?.let(model.saved::add) }
                LaunchedEffect(Unit) {
                    harness.started += model.name
                    try {
                        awaitCancellation()
                    } finally {
                        harness.ended += model.name
                    }
                }
            }
        }
        compose.waitForIdle()
        assertEquals(listOf("first"), harness.started)
        harness.open!!()
        val oldSave = harness.save!!
        compose.runOnIdle { harness.model = second }
        compose.pumpFrame()
        assertEquals(listOf("first", "second"), harness.started)
        assertEquals(listOf("first"), harness.ended)
        // The new panel holds nothing pending, so saving does nothing.
        harness.save!!()
        assertTrue(second.saved.isEmpty())
        // The old panel's action still names the old model only.
        oldSave()
        assertEquals(listOf("first edit"), first.saved)
        assertTrue(second.saved.isEmpty())
    }

    @Test
    fun dismissingAControlClearsItsValueAndPresentingChangesNothing() {
        val state = mutableStateOf<String?>("sheet")
        assertTrue(state.controlPresented)
        state.controlPresented = true
        assertEquals("sheet", state.value)
        state.controlPresented = false
        assertNull(state.value)
        assertFalse(state.controlPresented)
    }
}
