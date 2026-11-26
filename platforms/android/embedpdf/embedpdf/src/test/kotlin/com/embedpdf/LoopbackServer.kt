package com.embedpdf

import java.io.InputStream
import java.net.InetAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.atomic.AtomicInteger
import kotlin.concurrent.thread

/**
 * An HTTP/1.1 server on a loopback port. Each request goes to [handler] with
 * its path; the handler answers through [Reply], which can also hold the
 * connection open without answering.
 */
class LoopbackServer(private val handler: (path: String, reply: Reply) -> Unit) : AutoCloseable {
    private val server = ServerSocket(0, 50, InetAddress.getLoopbackAddress())
    val requests = AtomicInteger()

    /** Held connections the client closed. */
    val stops = AtomicInteger()

    fun url(path: String): String = "http://127.0.0.1:${server.localPort}$path"

    init {
        thread(isDaemon = true, name = "loopback-accept") {
            while (!server.isClosed) {
                val socket = try {
                    server.accept()
                } catch (_: Exception) {
                    break
                }
                thread(isDaemon = true, name = "loopback-connection") { serve(socket) }
            }
        }
    }

    private fun serve(socket: Socket) {
        socket.use {
            val input = socket.getInputStream()
            val head = readHead(input) ?: return
            val path = head.lineSequence().first().split(" ").getOrElse(1) { "/" }
            requests.incrementAndGet()
            handler(path, Reply(socket, input))
        }
    }

    private fun readHead(input: InputStream): String? {
        val bytes = StringBuilder()
        while (!bytes.endsWith("\r\n\r\n")) {
            val next = input.read()
            if (next < 0) return null
            bytes.append(next.toChar())
        }
        return bytes.toString()
    }

    inner class Reply(private val socket: Socket, private val input: InputStream) {
        fun whole(status: Int, body: ByteArray) {
            val head = "HTTP/1.1 $status Status\r\ncontent-type: application/octet-stream\r\n" +
                "connection: close\r\ncontent-length: ${body.size}\r\n\r\n"
            socket.getOutputStream().apply {
                write(head.toByteArray())
                write(body)
                flush()
            }
        }

        /** Never answers; returns when the client closes the connection. */
        fun hold() {
            try {
                while (input.read() >= 0) {
                    // Drain until the client goes away.
                }
            } catch (_: Exception) {
                // A reset is a close too.
            }
            stops.incrementAndGet()
        }
    }

    override fun close() {
        server.close()
    }
}
