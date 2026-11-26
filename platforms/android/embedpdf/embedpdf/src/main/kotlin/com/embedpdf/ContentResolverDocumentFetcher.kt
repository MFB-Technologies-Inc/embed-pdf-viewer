package com.embedpdf

import android.content.ContentResolver
import android.net.Uri
import java.io.FileNotFoundException
import java.io.InputStream
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicReference

/**
 * A transport for `content:`, `file:` and `android.resource:` document sources,
 * read through a [ContentResolver]: what a document picker or a share intent
 * hands an app. Every other scheme goes to [fallback].
 *
 * A source the resolver can't open fails `not-found`. Reading runs on a
 * background thread; cancelling closes the stream, and the open fails
 * `operation-cancelled`.
 */
public class ContentResolverDocumentFetcher(
    private val resolver: ContentResolver,
    private val fallback: DocumentFetcher = HttpDocumentFetcher(),
) : DocumentFetcher {
    override fun fetch(url: String, completion: DocumentFetchCompletion): DocumentFetchCancellation {
        val uri = Uri.parse(url)
        if (uri.scheme !in schemes) return fallback.fetch(url, completion)
        val read = Read()
        fetchThreads.execute {
            try {
                val stream = resolver.openInputStream(uri)
                    ?: throw FileNotFoundException("The content provider has no stream for $url.")
                read.stream.set(stream)
                if (read.cancelled.get()) stream.close()
                val bytes = stream.use { readChecking(it, read.cancelled) }
                completion.finish(200u, bytes)
            } catch (e: Throwable) {
                // An `Error` too (a source too large for memory fails the open
                // rather than the thread); the first answer wins.
                completion.failWith(
                    when {
                        read.cancelled.get() ->
                            HostFailure(name = "AbortError", code = "operation-cancelled", detail = "operation cancelled")
                        e is FileNotFoundException ->
                            HostFailure(name = "FileNotFoundException", code = "not-found", detail = readable(e))
                        else -> HostFailure(e)
                    },
                )
            }
        }
        return read
    }

    private class Read : DocumentFetchCancellation {
        val cancelled = AtomicBoolean(false)
        val stream = AtomicReference<InputStream?>(null)

        override fun cancel() {
            cancelled.set(true)
            try {
                stream.get()?.close()
            } catch (_: Exception) {
                // The read fails on the closed stream and answers the completion.
            }
        }
    }

    private companion object {
        val schemes = setOf(ContentResolver.SCHEME_CONTENT, ContentResolver.SCHEME_FILE, ContentResolver.SCHEME_ANDROID_RESOURCE)
    }
}
