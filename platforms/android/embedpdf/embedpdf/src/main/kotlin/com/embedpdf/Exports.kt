package com.embedpdf

// The generated records the viewer's signatures pass through, under the
// `com.embedpdf` names an app imports. The `uniffi.*` declarations behind them
// are the bindings' and carry no compatibility promise of their own.

public typealias ViewerConfig = uniffi.epdf_viewer_host.ViewerConfig
public typealias DocumentInfo = uniffi.epdf_viewer_host.DocumentInfo
public typealias DocumentStatus = uniffi.epdf_viewer_host.DocumentStatus
public typealias PageInfo = uniffi.epdf_ffi.PageInfo
public typealias PageRef = uniffi.epdf_ffi.PageRef
public typealias Point = uniffi.epdf_ffi.Point
public typealias Size = uniffi.epdf_ffi.Size
public typealias Rect = uniffi.epdf_ffi.Rect
public typealias Quad = uniffi.epdf_ffi.Quad
public typealias PluginErrorInfo = uniffi.epdf_ffi.PluginErrorInfo
public typealias HostException = uniffi.epdf_ffi.HostException
public typealias HostFailure = uniffi.epdf_ffi.HostFailure
public typealias DocumentEvent = uniffi.epdf_viewer_host.DocumentEvent

/**
 * The transport for URL document sources, passed to [PdfViewer]'s constructor.
 * `fetch(url, completion)` reports once on its [DocumentFetchCompletion] and
 * returns the [DocumentFetchCancellation] that stops the request.
 * [HttpDocumentFetcher] is the default.
 */
public typealias DocumentFetcher = uniffi.epdf_viewer_host.DocumentFetcher
public typealias DocumentFetchCompletion = uniffi.epdf_viewer_host.DocumentFetchCompletion
public typealias DocumentFetchCancellation = uniffi.epdf_viewer_host.DocumentFetchCancellation
public typealias SaveMode = uniffi.epdf_viewer_host.SaveMode

public typealias DocumentEventOrigin = uniffi.epdf_ffi.DocumentEventOrigin
public typealias DocumentEventOriginKind = uniffi.epdf_ffi.DocumentEventOriginKind
public typealias DocumentEventTransaction = uniffi.epdf_ffi.DocumentEventTransaction

public typealias ViewerSettings = uniffi.epdf_viewer_host.ViewerSettings
public typealias ViewerSettingsPatch = uniffi.epdf_viewer_host.ViewerSettingsPatch
public typealias ViewerSettingsField = uniffi.epdf_viewer_host.ViewerSettingsField

/**
 * What a completion reports for [error]: a [PdfException] (or a generated
 * `ViewerException`) keeps its name, code and message, a cancellation is
 * `operation-cancelled`, and anything else is `operation-failed` under its
 * class name, with its message as the detail.
 */
public fun HostFailure(error: Throwable): HostFailure {
    val mapped = PdfException.from(error)
    return HostFailure(name = mapped.name, code = mapped.code.value, detail = mapped.detail)
}
