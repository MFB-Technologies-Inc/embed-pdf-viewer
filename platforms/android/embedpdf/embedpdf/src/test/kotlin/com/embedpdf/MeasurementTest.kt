package com.embedpdf

import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.long
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs

class MeasurementTest {
    data class Opened(val viewer: PdfViewer, val document: DocumentInfo, val measurement: MeasurementCapability, val page: PageRef)

    private suspend fun opened(fixture: String = "report"): Opened {
        val viewer = started(ViewerConfig(interaction = true, annotation = "", measurement = MeasurementConfig()))
        val document = viewer.open(Fixture.bytes(fixture))
        val page = viewer.pages(document)[0].page
        return Opened(viewer, document, viewer.measurement(document), page)
    }

    @Test
    fun ensureLoadedMakesThePageReady() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        measurement.ensureLoaded(page)
        val scale = measurement.pageScale(page)
        assertEquals(true, scale.ready)
        assertEquals(MeasurementScaleSource.DEFAULT, scale.source)
        assertEquals(true, scale.persistent)
        viewer.destroy()
    }

    @Test
    fun aPresetIsWrittenAndReadsBack() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        val preset = measurement.presets().first()
        val reports = measurement.setPreset(MeasurementPageTargetPage(page), preset.id)
        assertEquals(1, reports.size)
        assertEquals(MeasurementScaleSource.OWNED, measurement.pageScale(page).source)
        viewer.destroy()
    }

    @Test
    fun aForeignScaleIsReadAndKept() = viewerTest {
        val (viewer, _, measurement, page) = opened("measure-foreign-viewport")
        measurement.ensureLoaded(page)
        val foreign = measurement.pageScale(page)
        assertEquals(MeasurementScaleSource.FOREIGN, foreign.source)
        assertEquals("1 in = 1 m", foreign.measure?.ratio)
        val preset = measurement.presets().first()
        measurement.setPreset(MeasurementPageTargetPage(page), preset.id)
        assertEquals(MeasurementScaleSource.OWNED, measurement.pageScale(page).source)
        measurement.clearScale(MeasurementPageTargetPage(page))
        assertEquals(MeasurementScaleSource.FOREIGN, measurement.pageScale(page).source)
        viewer.destroy()
    }

    @Test
    fun measureDistanceUsesThePageScale() = viewerTest {
        val (viewer, _, measurement, page) = opened("measure-foreign-viewport")
        measurement.ensureLoaded(page)
        val reading = measurement.measureDistance(page, Point(x = 0.0, y = 0.0), Point(x = 72.0, y = 0.0))
        val readout = (reading as? MeasurementReadingValue)?.readout ?: error("expected a readout, got $reading")
        assertEquals(MeasurementReadoutKind.DISTANCE, readout.kind)
        assertTrue(readout.value > 0)
        viewer.destroy()
    }

    @Test
    fun calibratingAZeroLengthThrows() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCode(PdfException.Code.INVALID_INPUT) {
            measurement.calibrate(
                MeasurementCalibrateInput(page = page, from = Point(x = 5.0, y = 5.0), to = Point(x = 5.0, y = 5.0), value = 1.0, unit = "m"),
            )
        }
        viewer.destroy()
    }

    @Test
    fun aCreatedMeasurementIsAnAnnotation() = viewerTest {
        val (viewer, document, measurement, page) = opened()
        // A measurement needs the page's scale: `not-ready` until it loads.
        measurement.ensureLoaded(page)
        val ref = measurement.createMeasurement(
            CreateMeasurementInput(
                kind = MeasurementKind.DISTANCE,
                page = page,
                points = listOf(Point(x = 10.0, y = 10.0), Point(x = 82.0, y = 10.0)),
            ),
        )
        // `ref` is the engine's `AnnotationRef` record: its object number
        // names the line the annotation plugin lists.
        val objectNumber = Json.parseToJsonElement(ref).jsonObject.getValue("objectNumber").jsonPrimitive.long
        val annotationRef = AnnotationRefObjectNumber(page, objectNumber.toUInt())
        val found = viewer.annotations(document).get(annotationRef) ?: error("no annotation $ref")
        assertEquals(AnnotationSubtype.LINE, found.subtype)
        // The engine's line create writes `/IT /LineDimension` and
        // `/Measure`, so the read-back line is a dimension: 72 points, one
        // inch, in the page's default metric scale.
        for (reading in listOf(measurement.readout(ref), measurement.readout(annotationRef), measurement.readout(found))) {
            val readout = (reading as? MeasurementReadingValue)?.readout ?: error("expected a readout, got $reading")
            assertEquals(MeasurementReadoutKind.DISTANCE, readout.kind)
            assertTrue(abs(readout.value - 0.0254) < 1e-6)
        }
        viewer.destroy()
    }

    @Test
    fun aClosedDocumentsCapabilityThrows() = viewerTest {
        val (viewer, document, measurement, page) = opened()
        viewer.close(document)
        val error = expectCode(PdfException.Code.INSTANCE_CLOSED) { measurement.pageScale(page) }
        assertEquals("PluginError", error.name)
        viewer.destroy()
    }

    @Test
    fun droppingTheStreamUnsubscribes() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        val job = launch(start = CoroutineStart.UNDISPATCHED) { measurement.observeEvents().collect {} }
        delay(20)
        job.cancel()
        job.join()
        measurement.setPreset(MeasurementPageTargetPage(page), measurement.presets().first().id)
        viewer.destroy()
    }

    // One per member that takes a `CancelToken`, each called in an
    // already-cancelled coroutine.

    @Test
    fun setPresetCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        val preset = measurement.presets().first()
        expectCancelledBeforeItStarts { measurement.setPreset(MeasurementPageTargetPage(page), preset.id) }
        measurement.pageScale(page)
        viewer.destroy()
    }

    @Test
    fun setScaleCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts {
            measurement.setScale(MeasurementPageTargetPage(page), PdfMeasurement(subtype = "RL", ratio = "1 in = 1 ft"))
        }
        viewer.destroy()
    }

    @Test
    fun calibrateCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts {
            measurement.calibrate(
                MeasurementCalibrateInput(page = page, from = Point(x = 0.0, y = 0.0), to = Point(x = 72.0, y = 0.0), value = 1.0, unit = "in"),
            )
        }
        viewer.destroy()
    }

    @Test
    fun setUnitCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts { measurement.setUnit(MeasurementPageTargetPage(page), "cm") }
        viewer.destroy()
    }

    @Test
    fun setAreaUnitCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts { measurement.setAreaUnit(MeasurementPageTargetPage(page), "cm") }
        viewer.destroy()
    }

    @Test
    fun setPrecisionCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts { measurement.setPrecision(MeasurementPageTargetPage(page), 2.0) }
        viewer.destroy()
    }

    @Test
    fun clearScaleCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts { measurement.clearScale(MeasurementPageTargetPage(page)) }
        viewer.destroy()
    }

    @Test
    fun createMeasurementCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        measurement.ensureLoaded(page)
        expectCancelledBeforeItStarts {
            measurement.createMeasurement(
                CreateMeasurementInput(
                    kind = MeasurementKind.DISTANCE,
                    page = page,
                    points = listOf(Point(x = 10.0, y = 10.0), Point(x = 82.0, y = 10.0)),
                ),
            )
        }
        viewer.destroy()
    }

    @Test
    fun ensureLoadedCancelsWhenTheCallerIsCancelled() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        expectCancelledBeforeItStarts { measurement.ensureLoaded(page) }
        viewer.destroy()
    }

    @Test
    fun aScaleChangeIsObserved() = viewerTest {
        val (viewer, _, measurement, page) = opened()
        val events = subscribed(measurement.observeEvents())
        measurement.setPreset(MeasurementPageTargetPage(page), measurement.presets().first().id)
        val changed = events.nextMapped { it as? MeasurementEventScaleChanged }
        assertEquals(page, changed.page)
        viewer.destroy()
    }
}
