package com.embedpdf.compose

import androidx.compose.runtime.MutableState

/**
 * Whether a control for this value is presented: true while it holds a
 * value. Setting it to false dismisses the control by clearing the value;
 * setting it to true changes nothing, since presenting takes a value.
 */
internal var <T : Any> MutableState<T?>.controlPresented: Boolean
    get() = value != null
    set(presented) {
        if (!presented) value = null
    }
