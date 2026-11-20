package com.embedpdf

import uniffi.epdf_ffi.ViewerException
import kotlin.coroutines.cancellation.CancellationException

/**
 * What every viewer and capability call throws.
 *
 * [code] is a [Code]: one of the kernel's nine plugin error codes, the
 * `PluginErrorCode` vocabulary of the TypeScript kernel's `toPluginError`, the
 * bindings' own `invalid-state`, or any other string the kernel adds later.
 * See [Code] for what each one means.
 *
 * An engine's own code (`NotFound`, `DocPasswordIncorrect`) never appears here:
 * it maps as `toPluginError` maps it, and [detail] is the engine's message with
 * no code in front. [name] says what raised the error (`PluginError`,
 * `EngineError`, `AbortError`, or a foreign error's type), and is for logging
 * rather than matching.
 *
 * Match a code with `when`:
 *
 * ```kotlin
 * try {
 *     viewer.unlock(document, password)
 * } catch (e: PdfException) {
 *     if (e.code == PdfException.Code.PERMISSION_DENIED) showWrongPassword()
 * }
 * ```
 *
 * Two exceptions are equal when their name, code and detail are.
 *
 * @property detail the engine's message; also [message].
 */
public class PdfException(
    public val code: Code,
    public val detail: String,
    public val name: String = "PluginError",
    cause: Throwable? = null,
) : Exception(detail, cause) {
    /**
     * A kernel error code. [value] is the wire string.
     *
     * The set is open, so this is a value class rather than an enum: a code the
     * kernel adds later needs no new constant to round-trip, and adding one
     * breaks no caller's exhaustive `when`. `Code("quota-exceeded")` is a code.
     */
    @JvmInline
    public value class Code(public val value: String) {
        /** The wire string. */
        override fun toString(): String = value

        public companion object {
            /**
             * `permission-denied`: the scope, the document's permissions or its
             * password refused it (a wrong `unlock` password is this).
             */
            public val PERMISSION_DENIED: Code = Code("permission-denied")

            /** `unsupported`: the plugin isn't in the `ViewerConfig`, or the engine can't do it. */
            public val UNSUPPORTED: Code = Code("unsupported")

            /** `not-found`: no such document, page, annotation or other resource. */
            public val NOT_FOUND: Code = Code("not-found")

            /** `not-ready`: the document is still loading, is locked, or failed. */
            public val NOT_READY: Code = Code("not-ready")

            /** `invalid-input`: an argument, or bytes that aren't a PDF. */
            public val INVALID_INPUT: Code = Code("invalid-input")

            /** `conflict`: the document changed under the call. */
            public val CONFLICT: Code = Code("conflict")

            /** `instance-closed`: the document closed or the viewer shut down. */
            public val INSTANCE_CLOSED: Code = Code("instance-closed")

            /** `operation-cancelled`: the call was cancelled. */
            public val OPERATION_CANCELLED: Code = Code("operation-cancelled")

            /** `operation-failed`: anything else. */
            public val OPERATION_FAILED: Code = Code("operation-failed")

            /**
             * `invalid-state`, the bindings' own code: a value that didn't survive
             * the trip across the boundary (a record the kernel answered without a
             * field, a count past `Int`). It is a bug to report, not something a
             * caller did.
             */
            public val INVALID_STATE: Code = Code("invalid-state")
        }
    }

    /** The code and the detail, for logs: `not-found: no page p9`. */
    override fun toString(): String = "$code: $detail"

    override fun equals(other: Any?): Boolean =
        other is PdfException && other.name == name && other.code == code && other.detail == detail

    override fun hashCode(): Int = (name.hashCode() * 31 + code.hashCode()) * 31 + detail.hashCode()

    internal companion object {
        /**
         * The exception a call surfaces for [error]: a generated `ViewerException`
         * keeps the kernel's name, code and message, a `PdfException` is itself,
         * a cancellation is `operation-cancelled`, and anything else is
         * `operation-failed` under its class name.
         */
        fun from(error: Throwable): PdfException = when (error) {
            is PdfException -> error
            is ViewerException.Kernel ->
                PdfException(error.code?.let(::Code) ?: Code.OPERATION_FAILED, error.detail, error.name, error)
            is ViewerException.Closed ->
                PdfException(Code.INSTANCE_CLOSED, "The viewer has shut down.", "Closed", error)
            is CancellationException ->
                PdfException(Code.OPERATION_CANCELLED, "operation cancelled", "AbortError", error)
            else -> PdfException(Code.OPERATION_FAILED, readable(error), error.javaClass.simpleName, error)
        }
    }
}

/** A message for an error nobody gave a code: its message, or its class name. */
internal fun readable(error: Throwable): String = error.message ?: error.toString()
