package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Collections
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import kotlin.coroutines.cancellation.CancellationException

class CommandsTest {
    private suspend fun startedCommands(): Pair<PdfViewer, CommandsCapability> {
        val viewer = started(ViewerConfig(commands = true))
        return viewer to viewer.commands()
    }

    private fun recorded(): MutableList<String> = Collections.synchronizedList(mutableListOf())

    private val undefined = """{"${'$'}undefined":true}"""

    @Test
    fun aSettingLockDisablesAndReportsItsRefusalUntilReleased() = viewerTest {
        val (viewer, commands) = startedCommands()
        val held = AtomicBoolean(true)
        val ran = AtomicInteger(0)
        val handler = CommandHandler(
            active = { true },
            locked = { if (held.get()) CommandSettingLock(by = "kiosk", reasonKey = null) else null },
            run = { ran.incrementAndGet() },
        )
        commands.register(CommandDefinition(id = "view:single", labelKey = "Single page", shortcuts = listOf("Ctrl+L"), handler = handler))
        val locked = commands.resolve("view:single")!!
        assertTrue(locked.visible && locked.active && !locked.enabled)
        val match = commands.matchStroke(CommandKeyStroke(key = "l", ctrl = true), isMac = false)
        assertEquals(CommandRefusalReason.LOCKED, match?.refusal?.reason)
        assertEquals(CommandExecuteResultRejected(CommandRejectReason.DISABLED), commands.execute("view:single"))
        assertEquals(0, ran.get())
        held.set(false)
        assertEquals(CommandExecuteResultExecuted, commands.execute("view:single"))
        assertEquals(1, ran.get())
        viewer.destroy()
    }

    @Test
    fun everyHookReceivesTheDefaultAppStateJson() = viewerTest {
        val (viewer, commands) = startedCommands()
        val seen = recorded()
        val record = { context: CommandContext -> seen.add(context.appStateJson) }
        val handler = CommandHandler(
            enabled = { record(it); true },
            active = { record(it); false },
            visible = { record(it); true },
            iconAccent = { record(it); null },
            locked = { record(it); null },
            run = { record(it) },
        )
        commands.register(CommandDefinition(id = "app:inspect", labelKey = "Inspect", handler = handler))
        commands.resolve("app:inspect")
        commands.execute("app:inspect")
        assertFalse(seen.isEmpty())
        assertTrue("$seen", seen.all { it == undefined })
        viewer.destroy()
    }

    @Test
    fun everyHookReceivesTheWholeReplacementAppState() = viewerTest {
        val (viewer, commands) = startedCommands()
        val seen = recorded()
        val runs = Collections.synchronizedList(mutableListOf<CommandContext>())
        val record = { context: CommandContext -> seen.add(context.appStateJson) }
        val approved = { context: CommandContext -> context.appStateJson == """{"status":"approved"}""" }
        val handler = CommandHandler(
            enabled = { record(it); true },
            active = { record(it); approved(it) },
            visible = { record(it); true },
            iconAccent = { record(it); IconAccent(primary = if (approved(it)) "#0f0" else "#f00", secondary = null) },
            run = { record(it); runs.add(it) },
        )
        val registration = commands.register(CommandDefinition(id = "app:inspect", labelKey = "Inspect", handler = handler))
        commands.disableCategory("hidden")
        for (json in listOf("""{"omitted":true,"status":"review"}""", """{"status":"approved"}""", "null")) {
            commands.setAppState(json.toByteArray())
            seen.clear()
            val resolved = commands.resolve("app:inspect")
            assertEquals(CommandExecuteResultExecuted, commands.execute("app:inspect", args = """{"copy":true}""".toByteArray()))
            val isApproved = json == """{"status":"approved"}"""
            assertEquals(isApproved, resolved?.active)
            assertEquals(if (isApproved) "#0f0" else "#f00", resolved?.iconAccent?.primary)
            // Each derivation is asked at most once per store change (upstream 7af98f4df).
            assertTrue("$seen", seen.size >= 7)
            assertTrue("$seen", seen.all { it == json })
            assertEquals("""{"copy":true}""", runs.last().argsJson)
            assertEquals(listOf("hidden"), commands.disabledCategories())
        }
        registration.unregister()
        viewer.destroy()
    }

    private suspend fun appStateUpdatedDuringRun(fails: Boolean) {
        val (viewer, commands) = startedCommands()
        val ran = recorded()
        commands.setAppState("""{"status":"review"}""".toByteArray())
        val approved = { context: CommandContext -> context.appStateJson == """{"status":"approved"}""" }
        val handler = CommandHandler(
            active = approved,
            locked = { if (approved(it)) CommandSettingLock(by = "app-state", reasonKey = null) else null },
            run = { context ->
                ran.add(context.appStateJson)
                commands.setAppState("""{"status":"approved"}""".toByteArray())
                if (fails) throw IllegalStateException("full")
            },
        )
        val registration = commands.register(
            CommandDefinition(id = "app:update", labelKey = "Update", shortcuts = listOf("Ctrl+L"), handler = handler),
        )
        val before = commands.resolve("app:update")
        assertEquals(false, before?.active)
        assertEquals(true, before?.enabled)
        if (fails) {
            expectCode(PdfException.Code.OPERATION_FAILED) { commands.execute("app:update") }
        } else {
            assertEquals(CommandExecuteResultExecuted, commands.execute("app:update"))
        }
        assertEquals(listOf("""{"status":"review"}"""), ran.toList())
        val after = commands.resolve("app:update")
        assertEquals(true, after?.active)
        assertEquals(false, after?.enabled)
        val match = commands.matchStroke(CommandKeyStroke(key = "l", ctrl = true), isMac = false)
        assertEquals(CommandRefusalReason.LOCKED, match?.refusal?.reason)
        registration.unregister()
        viewer.destroy()
    }

    @Test
    fun appStateUpdatedDuringASuccessfulRunIsCurrentAfterCompletion() = viewerTest { appStateUpdatedDuringRun(fails = false) }

    @Test
    fun appStateUpdatedDuringAFailingRunIsCurrentAfterCompletion() = viewerTest { appStateUpdatedDuringRun(fails = true) }

    @Test
    fun invalidAppStatePreservesThePreviousValueAndWakesNoReader() = viewerTest {
        val (viewer, commands) = startedCommands()
        val seen = recorded()
        val handler = CommandHandler(enabled = { seen.add(it.appStateJson); true })
        val registration = commands.register(CommandDefinition(id = "app:inspect", labelKey = "Inspect", handler = handler))
        commands.setAppState("""{"kept":true}""".toByteArray())
        commands.resolve("app:inspect")
        val revisions = subscribed(viewer.observeStateRevisions())
        delay(50)
        while (revisions.tryReceive().isSuccess) Unit
        for (invalid in listOf("{broken".toByteArray(), byteArrayOf(0x80.toByte()))) {
            expectCode(PdfException.Code.INVALID_INPUT) { commands.setAppState(invalid) }
            // A refused value makes no store change, so the hook isn't asked
            // again (upstream 7af98f4df); had the value replaced the state,
            // the hook would see it here.
            commands.resolve("app:inspect")
        }
        delay(50)
        assertTrue("an invalid value woke a reader", revisions.tryReceive().isFailure)
        assertFalse(seen.isEmpty())
        assertTrue("$seen", seen.all { it == """{"kept":true}""" })
        registration.unregister()
        viewer.destroy()
    }

    @Test
    fun aKotlinCommandRunsAsync() = viewerTest {
        val (viewer, commands) = startedCommands()
        val ran = recorded()
        commands.register("doc:save", labelKey = "Save") { context ->
            delay(10)
            ran.add(context.commandId)
        }
        val events = subscribed(commands.observeEvents())
        assertEquals(CommandExecuteResultExecuted, commands.execute("doc:save", args = """{"copy":true}""".toByteArray()))
        assertEquals(listOf("doc:save"), ran.toList())
        val executed = events.nextMapped { it as? CommandsEventExecuted }
        assertEquals("""{"copy":true}""", executed.event.argsJson)
        viewer.destroy()
    }

    @Test
    fun theRunBodyIsOffTheCallersThreadAndOffTheKernelThread() = viewerTest {
        val (viewer, commands) = startedCommands()
        val threads = recorded()
        val handler = CommandHandler(
            enabled = { threads.add("hook:" + Thread.currentThread().name); true },
            run = { threads.add("run:" + Thread.currentThread().name) },
        )
        commands.register(CommandDefinition(id = "doc:where", labelKey = "Where", handler = handler))
        commands.execute("doc:where")
        val run = threads.single { it.startsWith("run:") }.removePrefix("run:")
        assertTrue(run, run.startsWith("DefaultDispatcher-worker"))
        val hook = threads.first { it.startsWith("hook:") }.removePrefix("hook:")
        assertTrue(hook, hook != Thread.currentThread().name && !hook.startsWith("DefaultDispatcher-worker"))
        viewer.destroy()
    }

    @Test
    fun argsThatAreNotUtf8AreInvalidInput() = viewerTest {
        val (viewer, commands) = startedCommands()
        val ran = AtomicBoolean(false)
        commands.register("doc:save", labelKey = "Save") { ran.set(true) }
        // A lone continuation byte inside a JSON string: a lenient decoder
        // would have made it U+FFFD and run the command.
        val args = """{"name":"""".toByteArray() + byteArrayOf(0x80.toByte()) + """"}""".toByteArray()
        expectCode(PdfException.Code.INVALID_INPUT) { commands.execute("doc:save", args = args) }
        assertFalse(ran.get())
        viewer.destroy()
    }

    @Test
    fun aThrowingCommandFails() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register("doc:save", labelKey = "Save") { throw IllegalStateException("full") }
        expectCode(PdfException.Code.OPERATION_FAILED) { commands.execute("doc:save") }
        viewer.destroy()
    }

    @Test
    fun aThrownPdfExceptionKeepsItsCode() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register("doc:save", labelKey = "Save") {
            throw PdfException(PdfException.Code.PERMISSION_DENIED, "no doc.annotate")
        }
        val events = subscribed(commands.observeEvents())
        val error = expectCode(PdfException.Code.PERMISSION_DENIED) { commands.execute("doc:save") }
        assertEquals("no doc.annotate", error.detail)
        val failed = events.nextMapped { it as? CommandsEventExecutionFailed }
        assertEquals("permission-denied", failed.event.error.code)
        assertEquals("no doc.annotate", failed.event.error.message)
        viewer.destroy()
    }

    @Test
    fun aCancelledRunIsOperationCancelled() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register("doc:save", labelKey = "Save") { throw CancellationException("stopped") }
        expectCode(PdfException.Code.OPERATION_CANCELLED) { commands.execute("doc:save") }
        viewer.destroy()
    }

    @Test
    fun anUncodedErrorIsOperationFailedWithItsMessage() = viewerTest {
        val (viewer, commands) = startedCommands()
        class DiskFull : Exception("the disk is full")
        commands.register("doc:save", labelKey = "Save") { throw DiskFull() }
        val error = expectCode(PdfException.Code.OPERATION_FAILED) { commands.execute("doc:save") }
        assertEquals("the disk is full", error.detail)
        viewer.destroy()
    }

    @Test
    fun anUncodedErrorWithoutAMessageIsNotDumped() = viewerTest {
        val (viewer, commands) = startedCommands()
        class Full(val bytes: Int = 512) : Exception()
        commands.register("doc:save", labelKey = "Save") { throw Full() }
        val error = expectCode(PdfException.Code.OPERATION_FAILED) { commands.execute("doc:save") }
        assertFalse(error.detail, error.detail.contains("512"))
        viewer.destroy()
    }

    @Test
    fun aDerivationGatesExecution() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register(CommandDefinition(id = "doc:save", labelKey = "Save", handler = CommandHandler(enabled = { false }, run = {})))
        assertEquals(CommandExecuteResultRejected(CommandRejectReason.DISABLED), commands.execute("doc:save"))
        assertFalse(commands.canExecute("doc:save"))
        viewer.destroy()
    }

    /** From `CommandContextTest`: a hook that throws answers as a failed hook, every resolve. */
    @Test
    fun aThrowingHookDisablesTheCommandAndDropsItsAccent() = viewerTest {
        val (viewer, commands) = startedCommands()
        val failures = AtomicInteger(0)
        val handler = CommandHandler(
            enabled = { failures.incrementAndGet(); error("derive failed in Kotlin") },
            iconAccent = { failures.incrementAndGet(); error("accent failed in Kotlin") },
        )
        commands.register(CommandDefinition(id = "app:callback-failure", labelKey = "Failure", handler = handler))
        repeat(3) {
            // Derivations are asked once per store change, so each resolve follows one.
            commands.setAppState("""{"resolve":$it}""".toByteArray())
            val refused = commands.resolve("app:callback-failure")!!
            assertFalse(refused.enabled)
            assertNull(refused.iconAccent)
        }
        assertEquals(6, failures.get())
        viewer.destroy()
    }

    @Test
    fun theHooksAreTheOnesTheHandlerImplements() {
        assertEquals(listOf(CommandHook.RUN), CommandHandler(run = {}).hooks)
        assertEquals(listOf(CommandHook.ENABLED, CommandHook.LOCKED), CommandHandler(enabled = { true }, locked = { null }).hooks)
        val definition = CommandDefinition(id = "a:b", labelKey = "B", handler = CommandHandler(visible = { true }))
        assertEquals(listOf(CommandHook.VISIBLE), definition.hooks)
    }

    @Test
    fun aShortcutMatches() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register(CommandDefinition(id = "zoom:in", labelKey = "Zoom in", shortcuts = listOf("Mod+=")))
        val matched = commands.matchStroke(CommandKeyStroke(key = "=", meta = true), isMac = true)
        assertEquals(CommandShortcutMatch(commandId = "zoom:in", refusal = null), matched)
        viewer.destroy()
    }

    @Test
    fun unregisterRemovesTheCommand() = viewerTest {
        val (viewer, commands) = startedCommands()
        val registration = commands.register(CommandDefinition(id = "zoom:in", labelKey = "Zoom in"))
        registration.unregister()
        assertFalse(commands.hasCommand("zoom:in"))
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, commands) = startedCommands()
        commands.register(CommandDefinition(id = "zoom:in", labelKey = "Zoom in"))
        val collector = collecting(commands.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        commands.execute("zoom:in")
        viewer.destroy()
    }

    @Test
    fun theFlowCompletesWhenTheViewerIsDestroyed() = viewerTest {
        val (viewer, commands) = startedCommands()
        val flow = commands.observeEvents()
        val collector = collecting(flow)
        viewer.destroy()
        assertTrue(finishes(collector))
    }

    @Test
    fun aDestroyedViewersCapabilityThrows() = viewerTest {
        val (viewer, commands) = startedCommands()
        viewer.destroy()
        expectCode(PdfException.Code.INSTANCE_CLOSED) { commands.commandIds() }
    }

    @Test
    fun aPlatformCanBeSetAndClearedAndIsIgnoredAfterClose() = viewerTest {
        val (viewer, _) = startedCommands()
        val platform = object : StandardCommandsPlatform {
            override fun saveFile(bytes: ByteArray, fileName: String, contentType: String) {}

            override fun copyText(text: String, completion: StandardCommandCompletion) {
                completion.finish()
            }

            override fun copyImage(png: ByteArray, completion: StandardCommandCompletion) {
                completion.finish()
            }
        }
        viewer.setCommandsPlatform(platform)
        viewer.setCommandsPlatform(null)
        viewer.close()
        viewer.setCommandsPlatform(platform)
    }

    /** `SignatureTransportTests.theStandardCommandsJoinInUpstreamsOrderAndReachThePlatform`. */
    @Test
    fun theStandardCommandsJoinInUpstreamsOrderAndReachThePlatform() = viewerTest {
        // Named out of order: the join keeps upstream's.
        val viewer = started(
            ViewerConfig(
                interaction = true, history = true, selection = true, annotation = "", commands = true,
                standardCommands = listOf(StandardCommandList.DOCUMENT, StandardCommandList.HISTORY),
            ),
        )
        val saved = java.util.Collections.synchronizedList(mutableListOf<Triple<ByteArray, String, String>>())
        viewer.setCommandsPlatform(
            object : StandardCommandsPlatform {
                override fun saveFile(bytes: ByteArray, fileName: String, contentType: String) {
                    saved.add(Triple(bytes, fileName, contentType))
                }

                override fun copyText(text: String, completion: StandardCommandCompletion) = completion.finish()

                override fun copyImage(png: ByteArray, completion: StandardCommandCompletion) = completion.finish()
            },
        )
        val document = viewer.open(Fixture.bytes("unsigned_sigfield"))
        val commands = viewer.commands()
        val ids = commands.commandIds()
        assertTrue("$ids", ids.indexOf("history:undo") in 0 until ids.indexOf("document:download"))
        assertEquals(CommandExecuteResultExecuted, commands.execute("document:download", document))
        assertEquals(1, saved.size)
        assertEquals("%PDF", String(saved[0].first, 0, 4, Charsets.US_ASCII))
        assertEquals("application/pdf", saved[0].third)
        viewer.destroy()
    }
}
