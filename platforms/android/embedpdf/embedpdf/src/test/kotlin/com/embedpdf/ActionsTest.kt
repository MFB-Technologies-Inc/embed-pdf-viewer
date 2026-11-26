package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Test

class RecordingUi : ActionUiHandler {
    private val log = mutableListOf<String>()

    val opened: List<String> get() = synchronized(log) { log.toList() }

    override fun openUri(uri: String, options: ActionUriOptions) {
        synchronized(log) { log.add(uri) }
    }

    override fun print(context: ActionUiContext?) {}

    override fun alert(message: String, options: ActionAlertOptions) {}

    override fun gotoPage(page: Double, context: ActionUiContext) {}
}

class RecordingExecutor : ActionExecutor {
    private val log = mutableListOf<String>()

    val types: List<String> get() = synchronized(log) { log.toList() }

    fun record(type: String) {
        synchronized(log) { log.add(type) }
    }

    override fun execute(node: PdfActionNode, context: ActionContext, completion: ActionExecutorCompletion) {
        record(node.nodeType())
        completion.executed()
    }
}

class ActionsTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val actions: ActionsCapability)

    companion object {
        suspend fun opened(config: ActionsConfig = ActionsConfig(openSequence = OpenSequence.OFF)): Opened {
            val viewer = started(ViewerConfig(actions = config))
            val document = viewer.open(Fixture.bytes("document_actions"))
            return Opened(viewer, document, viewer.actions(document))
        }

        val user = ActionContext(origin = ActionOrigin.USER, source = ActionSourceApi, event = ActionTriggerEventActivate)

        fun uriTree(uri: String): PdfActionTree = PdfActionTree.decode(
            """{"root":{"type":"uri","subtype":"URI","uri":"$uri","isMap":false,"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""",
        )
    }

    @Test
    fun theCatalogsOpenTreeIsJavaScript() = viewerTest {
        val (viewer, _, actions) = opened()
        val open = actions.actionTree(ActionTreeSourceDocument(event = null))
        assertEquals("javascript", open?.rootType())
        assertEquals(false, actions.isScriptingEnabled())
        viewer.destroy()
    }

    @Test
    fun aUserUriReachesTheHandlerAndAFileUriDoesNot() = viewerTest {
        val (viewer, _, actions) = opened()
        val ui = RecordingUi()
        val registration = actions.setUiHandler(ui)
        actions.execute(uriTree("https://a.test/"), user)
        val blocked = actions.execute(uriTree("file:///etc/passwd"), user)
        assertEquals(listOf(ActionNodeStatus.BLOCKED), blocked.nodes.map { it.status })
        assertEquals(listOf("https://a.test/"), ui.opened)
        registration.unregister()
        viewer.destroy()
    }

    @Test
    fun aKotlinExecutorRunsItsNodes() = viewerTest {
        val (viewer, _, actions) = opened()
        val executor = RecordingExecutor()
        actions.registerExecutor("goto", executor)
        val goto = PdfActionTree.decode(
            """{"root":{"type":"goto","subtype":"GoTo","destination":{"kind":"fit","page":{"kind":"objectNumber","pageObjectNumber":1}},"next":[]},"incomplete":false,"warningFlags":0,"warnings":[]}""",
        )
        val result = actions.execute(goto, user)
        assertEquals(listOf(ActionNodeStatus.EXECUTED), result.nodes.map { it.status })
        assertEquals(listOf("goto"), executor.types)
        viewer.destroy()
    }

    @Test
    fun prepareCloseFindsTheWillCloseScript() = viewerTest {
        val (viewer, _, actions) = opened()
        val result = actions.prepareClose()
        assertEquals(1, result.steps.size)
        assertEquals(listOf(ActionNodeStatus.INERT), result.steps.first().result.nodes.map { it.status })
        viewer.destroy()
    }

    @Test
    fun aDocumentVerbRunsItsOperation() = viewerTest {
        val (viewer, _, actions) = opened()
        val ran = RecordingExecutor()
        actions.runDocumentVerb(DocumentVerb.SAVE) {
            delay(5)
            ran.record("save")
        }
        assertEquals(listOf("save"), ran.types)
        viewer.destroy()
    }

    @Test
    fun aDocumentVerbsThrownPdfExceptionKeepsItsCode() = viewerTest {
        val (viewer, _, actions) = opened()
        val error = expectCode(PdfException.Code.PERMISSION_DENIED) {
            actions.runDocumentVerb(DocumentVerb.SAVE) {
                throw PdfException(PdfException.Code.PERMISSION_DENIED, "no doc.download")
            }
        }
        assertEquals("no doc.download", error.detail)
        viewer.destroy()
    }

    @Test
    fun aTreeThatIsNotJsonIsInvalidInput() = viewerTest {
        expectCode(PdfException.Code.INVALID_INPUT) { PdfActionTree.decode("{") }
    }

    @Test
    fun theCurrentTimeZoneConfigCarriesItsOffset() {
        val config = ActionsConfig.forCurrentTimeZone(openSequence = OpenSequence.OFF)
        val offset = java.util.TimeZone.getDefault().getOffset(System.currentTimeMillis()) / 60_000
        assertEquals(offset, config.utcOffsetMinutes)
        assertEquals(OpenSequence.OFF, config.openSequence)
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, actions) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { actions.policy() }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheFlowUnsubscribes() = viewerTest {
        val (viewer, _, actions) = opened()
        val collector = collecting(actions.observeEvents())
        delay(20)
        collector.cancel()
        collector.join()
        actions.prepareClose()
        viewer.destroy()
    }
}
