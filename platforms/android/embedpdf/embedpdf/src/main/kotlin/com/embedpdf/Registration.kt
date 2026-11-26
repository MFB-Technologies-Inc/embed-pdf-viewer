package com.embedpdf

import uniffi.epdf_ffi.RegistrationHandle

/**
 * A registered tool, handler, command or locale. [unregister] undoes it;
 * dropping this without calling it leaves the registration in place.
 */
public class Registration internal constructor(private val handle: RegistrationHandle) {
    /** Idempotent. */
    public fun unregister() {
        handle.unregister()
    }
}
