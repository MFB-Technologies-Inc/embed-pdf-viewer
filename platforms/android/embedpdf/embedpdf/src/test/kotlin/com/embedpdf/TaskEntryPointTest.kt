package com.embedpdf

import kotlinx.coroutines.delay
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.lang.reflect.Method
import java.lang.reflect.Modifier

/**
 * [PageNavigator], [PageZoom] and [ToolSwitcher] add names, not behaviour.
 * Every member forwards to the capability member of the same name: the
 * reflection check proves each facade member has a capability member with
 * the same parameters and result, and the equivalence tests run each member
 * both ways on fresh documents and compare what the engine reports.
 */
class TaskEntryPointTest {
    // ── forwarding table ──

    private fun members(type: Class<*>, skip: Set<String> = emptySet()): List<Method> =
        type.declaredMethods
            .filter { Modifier.isPublic(it.modifiers) && !it.isSynthetic && '$' !in it.name && it.name !in skip }

    private fun assertForwards(facade: Class<*>, capability: Class<*>, property: String, tested: Set<String>) {
        val names = mutableListOf<String>()
        for (member in members(facade, skip = setOf(property))) {
            // A value-class parameter or result mangles the JVM name: `hasTool-<hash>`.
            names.add(member.name.substringBefore('-'))
            val match = capability.methods.firstOrNull {
                it.name == member.name && it.parameterTypes.contentEquals(member.parameterTypes) && it.returnType == member.returnType
            }
            assertTrue("${facade.simpleName}.${member.name} has no ${capability.simpleName} twin", match != null)
        }
        assertEquals("${facade.simpleName} members without an equivalence step", tested, names.toSet())
    }

    @Test
    fun everyFacadeMemberForwardsToACapabilityMemberWithItsSignature() {
        assertForwards(PageNavigator::class.java, StageCapability::class.java, "getStage", navigatorSteps.map { it.name }.toSet())
        assertForwards(PageZoom::class.java, StageCapability::class.java, "getStage", zoomSteps.map { it.name }.toSet())
        assertForwards(
            ToolSwitcher::class.java,
            InteractionCapability::class.java,
            "getInteraction",
            toolSteps.map { it.name }.toSet(),
        )
    }

    // ── equivalence on the stage ──

    /** One facade member and the capability call it forwards to. A read returns what it read. */
    class Step<C, F>(
        val name: String,
        val viaCapability: suspend (C, Opened) -> String,
        val viaFacade: suspend (F, Opened) -> String,
    )

    class Opened(val viewer: PdfViewer, val document: DocumentInfo) {
        suspend fun page(index: Int): PageRef = viewer.pages(document)[index].page
    }

    private val navigatorSteps: List<Step<StageCapability, PageNavigator>> = listOf(
        Step("pageCount", { c, _ -> "${c.pageCount()}" }, { f, _ -> "${f.pageCount()}" }),
        Step("currentPageIndex", { c, _ -> "${c.currentPageIndex()}" }, { f, _ -> "${f.currentPageIndex()}" }),
        Step("currentPage", { c, _ -> "${c.currentPage()}" }, { f, _ -> "${f.currentPage()}" }),
        Step("canGoNext", { c, _ -> "${c.canGoNext()}" }, { f, _ -> "${f.canGoNext()}" }),
        Step("canGoPrevious", { c, _ -> "${c.canGoPrevious()}" }, { f, _ -> "${f.canGoPrevious()}" }),
        Step("goToPageIndex", { c, _ -> c.goToPageIndex(5, instant); "" }, { f, _ -> f.goToPageIndex(5, instant); "" }),
        Step("goToPage", { c, o -> c.goToPage(o.page(6), instant); "" }, { f, o -> f.goToPage(o.page(6), instant); "" }),
        Step("goToFirstPage", { c, _ -> c.goToFirstPage(instant); "" }, { f, _ -> f.goToFirstPage(instant); "" }),
        Step("goToLastPage", { c, _ -> c.goToLastPage(instant); "" }, { f, _ -> f.goToLastPage(instant); "" }),
        Step("nextPage", { c, _ -> c.nextPage(instant); "" }, { f, _ -> f.nextPage(instant); "" }),
        Step("previousPage", { c, _ -> c.previousPage(instant); "" }, { f, _ -> f.previousPage(instant); "" }),
    )

    private val zoomSteps: List<Step<StageCapability, PageZoom>> = listOf(
        Step("zoomLevel", { c, _ -> "${c.zoomLevel()}" }, { f, _ -> "${f.zoomLevel()}" }),
        Step("zoomMode", { c, _ -> "${c.zoomMode()}" }, { f, _ -> "${f.zoomMode()}" }),
        Step("canZoomIn", { c, _ -> "${c.canZoomIn()}" }, { f, _ -> "${f.canZoomIn()}" }),
        Step("canZoomOut", { c, _ -> "${c.canZoomOut()}" }, { f, _ -> "${f.canZoomOut()}" }),
        Step("zoomIn", { c, _ -> c.zoomIn(); "" }, { f, _ -> f.zoomIn(); "" }),
        Step("zoomOut", { c, _ -> c.zoomOut(); "" }, { f, _ -> f.zoomOut(); "" }),
        Step(
            "zoomTo",
            { c, _ -> c.zoomTo(StageZoomTargetLevel(2.0), Point(100.0, 200.0)); "" },
            { f, _ -> f.zoomTo(StageZoomTargetLevel(2.0), Point(100.0, 200.0)); "" },
        ),
        Step("fitWidth", { c, _ -> c.fitWidth(); "" }, { f, _ -> f.fitWidth(); "" }),
        Step("fitPage", { c, _ -> c.fitPage(); "" }, { f, _ -> f.fitPage(); "" }),
    )

    private data class StageState(
        val pageIndex: Int,
        val page: PageRef?,
        val zoomLevel: Double,
        val zoomMode: StageZoomMode,
        val camera: Camera,
    )

    /** Sized, and on page 3 so every navigation step has somewhere to go. */
    private suspend fun openedStage(): Triple<PdfViewer, DocumentInfo, StageCapability> {
        val viewer = started(ViewerConfig(stage = StageConfig()))
        val document = viewer.open(Fixture.bytes("report"))
        val stage = viewer.stage(document)
        stage.setViewportSize(Size(390.0, 844.0))
        stage.goToPageIndex(3, instant)
        return Triple(viewer, document, stage)
    }

    /** What the engine reports once any tween has finished. */
    private suspend fun observe(stage: StageCapability): StageState {
        var polls = 0
        while (stage.isMoving() && polls < 500) {
            polls++
            delay(10)
        }
        assertFalse(stage.isMoving())
        return StageState(stage.currentPageIndex(), stage.currentPage(), stage.zoomLevel(), stage.zoomMode(), stage.camera())
    }

    private suspend fun <F> compareOnTheStage(step: Step<StageCapability, F>, facade: suspend (PdfViewer, DocumentInfo) -> F) {
        val (viewer, document, stage) = openedStage()
        val before = observe(stage)
        val capabilityRead = step.viaCapability(stage, Opened(viewer, document))
        val capabilityState = observe(stage)

        val (facadeViewer, facadeDocument, facadeStage) = openedStage()
        val target = facade(facadeViewer, facadeDocument)
        val facadeRead = step.viaFacade(target, Opened(facadeViewer, facadeDocument))
        val facadeState = observe(facadeStage)

        assertEquals(step.name, capabilityRead, facadeRead)
        assertEquals(step.name, capabilityState, facadeState)
        if (capabilityRead.isEmpty()) assertNotEquals("${step.name} should change what the stage shows", before, capabilityState)
        viewer.destroy()
        facadeViewer.destroy()
    }

    @Test
    fun pageNavigatorMembersMatchTheStage() = viewerTest {
        for (step in navigatorSteps) compareOnTheStage(step) { viewer, document -> viewer.pageNavigator(document) }
    }

    @Test
    fun pageZoomMembersMatchTheStage() = viewerTest {
        for (step in zoomSteps) compareOnTheStage(step) { viewer, document -> viewer.pageZoom(document) }
    }

    // ── equivalence on the pointer hub ──

    private val toolSteps: List<Step<InteractionCapability, ToolSwitcher>> = listOf(
        Step("tools", { c, _ -> "${c.tools()}" }, { f, _ -> "${f.tools()}" }),
        Step("activeToolId", { c, _ -> "${c.activeToolId()}" }, { f, _ -> "${f.activeToolId()}" }),
        Step("defaultToolId", { c, _ -> "${c.defaultToolId()}" }, { f, _ -> "${f.defaultToolId()}" }),
        Step("hasTool", { c, _ -> "${c.hasTool(ToolId.ZOOM)}" }, { f, _ -> "${f.hasTool(ToolId.ZOOM)}" }),
        Step("activateTool", { c, _ -> c.activateTool(ToolId.ZOOM); "" }, { f, _ -> f.activateTool(ToolId.ZOOM); "" }),
        Step("activateDefaultTool", { c, _ -> c.activateDefaultTool(); "" }, { f, _ -> f.activateDefaultTool(); "" }),
        Step("pushTool", { c, _ -> c.pushTool(ToolId.ZOOM); "" }, { f, _ -> f.pushTool(ToolId.ZOOM); "" }),
        Step("popTool", { c, _ -> c.popTool(); "" }, { f, _ -> f.popTool(); "" }),
    )

    /** Starts on `pan` pushed over the default tool, so `activateDefaultTool` and `popTool` both have something to change. */
    private suspend fun openedInteraction(): Triple<PdfViewer, DocumentInfo, InteractionCapability> {
        val viewer = started(ViewerConfig(interaction = true))
        val document = viewer.open(Fixture.bytes("report"))
        val interaction = viewer.interaction(document)
        interaction.pushTool(ToolId.PAN)
        return Triple(viewer, document, interaction)
    }

    private suspend fun observe(interaction: InteractionCapability): List<String> =
        listOf(interaction.activeToolId().value, interaction.cursor())

    @Test
    fun toolSwitcherMembersMatchTheHub() = viewerTest {
        for (step in toolSteps) {
            val (viewer, document, interaction) = openedInteraction()
            val before = observe(interaction)
            val capabilityRead = step.viaCapability(interaction, Opened(viewer, document))
            val capabilityState = observe(interaction)

            val (facadeViewer, facadeDocument, facadeInteraction) = openedInteraction()
            val switcher = facadeViewer.toolSwitcher(facadeDocument)
            val facadeRead = step.viaFacade(switcher, Opened(facadeViewer, facadeDocument))
            val facadeState = observe(facadeInteraction)

            assertEquals(step.name, capabilityRead, facadeRead)
            assertEquals(step.name, capabilityState, facadeState)
            if (capabilityRead.isEmpty()) assertNotEquals("${step.name} should change the active tool", before, capabilityState)
            viewer.destroy()
            facadeViewer.destroy()
        }
    }

    // ── errors ──

    @Test
    fun theFacadesThrowWhatTheCapabilitiesThrow() = viewerTest {
        val (viewer, document, stage) = openedStage()
        val navigator = viewer.pageNavigator(document)
        val zoom = viewer.pageZoom(document)
        expectCode(PdfException.Code.INVALID_INPUT) { stage.goToPageIndex(-1) }
        expectCode(PdfException.Code.INVALID_INPUT) { navigator.goToPageIndex(-1) }

        val (toolViewer, toolDocument, interaction) = openedInteraction()
        val switcher = toolViewer.toolSwitcher(toolDocument)
        expectCode(PdfException.Code.NOT_FOUND) { interaction.activateTool(ToolId("nobody-registered-this")) }
        expectCode(PdfException.Code.NOT_FOUND) { switcher.activateTool(ToolId("nobody-registered-this")) }

        viewer.close(document)
        expectCode(PdfException.Code.INSTANCE_CLOSED) { stage.zoomLevel() }
        expectCode(PdfException.Code.INSTANCE_CLOSED) { zoom.zoomLevel() }
        expectCode(PdfException.Code.INSTANCE_CLOSED) { navigator.pageCount() }
        viewer.destroy()
        toolViewer.destroy()
    }

    @Test
    fun theAccessorsRefuseALeftOutPluginAsTheCapabilityAccessorsDo() = viewerTest {
        val viewer = started()
        val document = viewer.open(Fixture.bytes("report"))
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.stage(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.pageNavigator(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.pageZoom(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.interaction(document) }
        expectCode(PdfException.Code.UNSUPPORTED) { viewer.toolSwitcher(document) }
        viewer.destroy()
    }

    private companion object {
        val instant = StageGoToOptions(behavior = StageScrollBehavior.INSTANT)
    }
}
