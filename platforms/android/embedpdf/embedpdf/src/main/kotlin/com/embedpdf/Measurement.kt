package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_measurement.MeasurementEventListener
import uniffi.epdf_plugin_measurement.MeasurementHandle

// `PdfMeasurement`, `PdfNumberFormat`, `MeasureOrigin`, `MeasureFraction` and
// `MeasureLabelPosition` are in AnnotationSharedMembers.kt, beside the
// annotation wire that writes them.

public typealias MeasurementSettings = uniffi.epdf_plugin_measurement.MeasurementSettings

public typealias MeasurementConfig = uniffi.epdf_plugin_measurement.MeasurementConfig
public typealias MeasurementDefaultScale = uniffi.epdf_plugin_measurement.MeasurementDefaultScale
public typealias MeasurementDefaultScaleMetric = uniffi.epdf_plugin_measurement.MeasurementDefaultScale.Metric
public typealias MeasurementDefaultScaleImperial = uniffi.epdf_plugin_measurement.MeasurementDefaultScale.Imperial
public typealias MeasurementDefaultScaleMeasure = uniffi.epdf_plugin_measurement.MeasurementDefaultScale.Measure
public typealias ScalePreset = uniffi.epdf_plugin_measurement.ScalePreset
public typealias MeasurementScaleChangeOptions = uniffi.epdf_plugin_measurement.MeasurementScaleChangeOptions
public typealias MeasurementPageTarget = uniffi.epdf_plugin_measurement.MeasurementPageTarget
public typealias MeasurementPageTargetPage = uniffi.epdf_plugin_measurement.MeasurementPageTarget.Page
public typealias MeasurementPageTargetPages = uniffi.epdf_plugin_measurement.MeasurementPageTarget.Pages
public typealias MeasurementPageTargetAll = uniffi.epdf_plugin_measurement.MeasurementPageTarget.All
public typealias MeasurementPageScale = uniffi.epdf_plugin_measurement.MeasurementPageScale
public typealias MeasurementScaleSource = uniffi.epdf_plugin_measurement.MeasurementScaleSource
public typealias MeasurementErrorInfo = uniffi.epdf_plugin_measurement.MeasurementErrorInfo
public typealias MeasurementReading = uniffi.epdf_plugin_measurement.MeasurementReading
public typealias MeasurementReadingValue = uniffi.epdf_plugin_measurement.MeasurementReading.Value
public typealias MeasurementReadingUnavailable = uniffi.epdf_plugin_measurement.MeasurementReading.Unavailable
public typealias MeasurementReadout = uniffi.epdf_plugin_measurement.MeasurementReadout
public typealias MeasurementReadoutKind = uniffi.epdf_plugin_measurement.MeasurementReadoutKind
public typealias MeasurementUnavailable = uniffi.epdf_plugin_measurement.MeasurementUnavailable
public typealias MeasurementCalibrationRequest = uniffi.epdf_plugin_measurement.MeasurementCalibrationRequest
public typealias MeasurementCalibrateInput = uniffi.epdf_plugin_measurement.MeasurementCalibrateInput
public typealias MeasurementCalibrateOptions = uniffi.epdf_plugin_measurement.MeasurementCalibrateOptions
public typealias MeasurementKind = uniffi.epdf_plugin_measurement.MeasurementKind
public typealias CreateMeasurementInput = uniffi.epdf_plugin_measurement.CreateMeasurementInput
public typealias ScaleChangeReport = uniffi.epdf_plugin_measurement.ScaleChangeReport
public typealias ScaleChangeReportRecalibrated = uniffi.epdf_plugin_measurement.ScaleChangeReport.Recalibrated
public typealias ScaleChangeReportScaleFailed = uniffi.epdf_plugin_measurement.ScaleChangeReport.ScaleFailed
public typealias ScaleRecalibration = uniffi.epdf_plugin_measurement.ScaleRecalibration
public typealias ScaleSkippedAnnotation = uniffi.epdf_plugin_measurement.ScaleSkippedAnnotation
public typealias ScaleSkipReason = uniffi.epdf_plugin_measurement.ScaleSkipReason
public typealias ScaleFailedAnnotation = uniffi.epdf_plugin_measurement.ScaleFailedAnnotation
public typealias MeasurementEvent = uniffi.epdf_plugin_measurement.MeasurementEvent
public typealias MeasurementEventScaleChanged = uniffi.epdf_plugin_measurement.MeasurementEvent.ScaleChanged
public typealias MeasurementEventCalibrationRequested = uniffi.epdf_plugin_measurement.MeasurementEvent.CalibrationRequested
public typealias MeasurementEventCalibrationCompleted = uniffi.epdf_plugin_measurement.MeasurementEvent.CalibrationCompleted
public typealias MeasurementEventCalibrationDismissed = uniffi.epdf_plugin_measurement.MeasurementEvent.CalibrationDismissed

/**
 * A document's page scales, calibration and measurement readouts. Get one
 * with [PdfViewer.measurement]; the viewer needs
 * `ViewerConfig(interaction = true, annotation = "", measurement = MeasurementConfig())`.
 * It keeps the viewer alive. After its document closes, every call throws
 * `instance-closed`.
 *
 * A scale change, calibration or created measurement that started lands even
 * when the caller is cancelled, and the caller then sees its cancellation
 * rather than the reports (see the README's Cancellation section). Read
 * [pageScale] again after a cancelled one.
 */
public class MeasurementCapability internal constructor(
    private val handle: MeasurementHandle,
    internal val viewer: PdfViewer,
) {
    /** Current settings shared by every document using this plugin registration. */
    public suspend fun settings(): MeasurementSettings = viewer.call { handle.settings() }

    /** Merges the supplied fields. Clear flags remove nullable values; omission preserves them. */
    public suspend fun updateSettings(patch: MeasurementConfig) {
        viewer.call { handle.updateSettings(patch) }
    }

    /** Restores the values supplied at registration, preserving registered host resources. */
    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    public suspend fun canMeasure(page: PageRef): Boolean = viewer.call { handle.canMeasure(page) }

    /** `ready` is false until [ensureLoaded] has read the page's viewports. */
    public suspend fun pageScale(page: PageRef): MeasurementPageScale = viewer.call { handle.pageScale(page) }

    public suspend fun isBusy(): Boolean = viewer.call { handle.isBusy() }

    public suspend fun lastReports(): List<ScaleChangeReport> = viewer.call { handle.lastReports() }

    public suspend fun presets(): List<ScalePreset> = viewer.call { handle.presets() }

    public suspend fun units(): List<String> = viewer.call { handle.units() }

    public suspend fun areaUnits(): List<String> = viewer.call { handle.areaUnits() }

    /**
     * A measurement annotation's value in its own scale. [annotationJson] is
     * the engine's `AnnotationRef` record, as [createMeasurement] returns it.
     */
    public suspend fun readout(annotationJson: String): MeasurementReading = viewer.call { handle.readout(annotationJson) }

    /**
     * A measurement annotation's value in its own scale, from the
     * annotation's ref: the one [AnnotationCapability.create] returns, or an
     * [AnnotationInfo.annotationRef]. The engine reads nothing else of the
     * annotation from the caller.
     */
    public suspend fun readout(ref: AnnotationRef): MeasurementReading =
        readout(AnnotationWire.annotation(ref).toString())

    /** A measurement annotation's value in its own scale, from the annotation as [AnnotationCapability] reads it. */
    public suspend fun readout(annotation: AnnotationInfo): MeasurementReading = readout(annotation.annotationRef)

    public suspend fun measureDistance(page: PageRef, from: Point, to: Point): MeasurementReading =
        viewer.call { handle.measureDistance(page, from, to) }

    public suspend fun measureArea(page: PageRef, vertices: List<Point>): MeasurementReading =
        viewer.call { handle.measureArea(page, vertices) }

    public suspend fun calibrationRequest(): MeasurementCalibrationRequest? = viewer.call { handle.calibrationRequest() }

    /** One report per page. */
    public suspend fun setScale(
        pages: MeasurementPageTarget,
        measure: PdfMeasurement,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.setScale(pages, measure, options, token) }

    public suspend fun calibrate(
        input: MeasurementCalibrateInput,
        options: MeasurementCalibrateOptions = MeasurementCalibrateOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.calibrate(input, options, token) }

    public suspend fun setUnit(
        pages: MeasurementPageTarget,
        unit: String,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.setUnit(pages, unit, options, token) }

    public suspend fun setAreaUnit(
        pages: MeasurementPageTarget,
        unit: String,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.setAreaUnit(pages, unit, options, token) }

    public suspend fun setPrecision(
        pages: MeasurementPageTarget,
        precision: Double,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.setPrecision(pages, precision, options, token) }

    public suspend fun setPreset(
        pages: MeasurementPageTarget,
        presetId: String,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.setPreset(pages, presetId, options, token) }

    /** Removes EmbedPDF's scale; another producer's stays. */
    public suspend fun clearScale(
        pages: MeasurementPageTarget,
        options: MeasurementScaleChangeOptions = MeasurementScaleChangeOptions(),
    ): List<ScaleChangeReport> = viewer.callCancellable { token -> handle.clearScale(pages, options, token) }

    public suspend fun startCalibration() {
        viewer.call { handle.startCalibration() }
    }

    public suspend fun dismissCalibration() {
        viewer.call { handle.dismissCalibration() }
    }

    /**
     * Creates a measurement annotation through the annotation plugin, and
     * returns the new annotation's `AnnotationRef` record as the engine's
     * JSON. Throws `not-ready` until [ensureLoaded] has read the page's scale.
     */
    public suspend fun createMeasurement(input: CreateMeasurementInput): String =
        viewer.callCancellable { token -> handle.createMeasurement(input, token) }

    public suspend fun canCalibrate(): Boolean = viewer.call { handle.canCalibrate() }

    /** Reads [page]'s viewports. Cancelling the calling coroutine cancels the read. */
    public suspend fun ensureLoaded(page: PageRef) {
        viewer.callCancellable { token -> handle.ensureLoaded(page, token) }
    }

    /**
     * Scale changes and calibration requests, completions and dismissals.
     * Completes when the document closes (at once if it already has) or the
     * viewer is destroyed. Each collection is a new subscription.
     */
    public fun observeEvents(): Flow<MeasurementEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : MeasurementEventListener {
                override fun onEvent(event: MeasurementEvent) {
                    channel.trySend(event)
                }

                override fun onEnd() {
                    channel.close()
                }
            },
        )
    }
}

/**
 * The measurement capability for [document]. Throws `unsupported` when the
 * viewer was built without the measurement plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.measurement(document: DocumentInfo): MeasurementCapability =
    MeasurementCapability(call { host.measurement(document.id) }, this)
