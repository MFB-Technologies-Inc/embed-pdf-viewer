package com.embedpdf.compose

import androidx.compose.runtime.Composable
import androidx.compose.runtime.NonSkippableComposable
import androidx.compose.runtime.key

/**
 * Composes [content] under the identity of [model], so a new model starts
 * with fresh state: every `remember`, effect and pending action of the old
 * one is dropped, and its effects are cancelled.
 */
// Never skipped: a skippable call may compare a stable model with `equals`
// and keep the old one's content, where only identity decides.
@NonSkippableComposable
@Composable
internal fun <M : Any> ModelScope(model: M, content: @Composable (M) -> Unit) {
    key(IdentityKey(model)) { content(model) }
}

/** Compares by identity, as Swift's `ObjectIdentifier` does, whatever the model's `equals`. */
private class IdentityKey(val value: Any) {
    override fun equals(other: Any?): Boolean = other is IdentityKey && other.value === value

    override fun hashCode(): Int = System.identityHashCode(value)
}
