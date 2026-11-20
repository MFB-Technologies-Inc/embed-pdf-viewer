package com.embedpdf

// `AnnotationRef`, `PdfLinkTarget` and `PageDestination` are declared in
// Link.kt.
//
// The plugin speaks the engine's records: a record carries its ref, page,
// subtype, rect, text members and authority typed, and the rest as JSON
// (`AnnotationRecord.fieldsJson`). Reads hand it out as `AnnotationInfo`,
// which types the timestamps, the subtype and the blend mode. Create fields,
// patches, tool defaults and selection values are the engine's field bags,
// passed as JSON objects.
public typealias CreationDraftAnchor = uniffi.epdf_ffi.CreationDraftAnchor
public typealias AnnotationRecord = uniffi.epdf_plugin_annotation.AnnotationRecord
public typealias AnnotationCreationDraft = uniffi.epdf_plugin_annotation.AnnotationCreationDraft
public typealias AnnotationProperty = uniffi.epdf_plugin_annotation.AnnotationProperty
public typealias AnnotationFilter = uniffi.epdf_plugin_annotation.AnnotationFilter
public typealias AnnotationBatchResult = uniffi.epdf_plugin_annotation.AnnotationBatchResult
public typealias AnnotationBatchFailure = uniffi.epdf_plugin_annotation.AnnotationBatchFailure
public typealias AnnotationImportResult = uniffi.epdf_plugin_annotation.AnnotationImportResult
public typealias AnnotationRefMapping = uniffi.epdf_plugin_annotation.AnnotationRefMapping
public typealias AnnotationEvent = uniffi.epdf_plugin_annotation.AnnotationEvent
public typealias AnnotationEventResynced = uniffi.epdf_plugin_annotation.AnnotationEvent.Resynced
public typealias AnnotationEventCreated = uniffi.epdf_plugin_annotation.AnnotationEvent.Created
public typealias AnnotationEventUpdated = uniffi.epdf_plugin_annotation.AnnotationEvent.Updated
public typealias AnnotationEventDeleted = uniffi.epdf_plugin_annotation.AnnotationEvent.Deleted
public typealias AnnotationEventReordered = uniffi.epdf_plugin_annotation.AnnotationEvent.Reordered
public typealias AnnotationEventSelectionChanged = uniffi.epdf_plugin_annotation.AnnotationEvent.SelectionChanged
public typealias AnnotationEventDraftChanged = uniffi.epdf_plugin_annotation.AnnotationEvent.DraftChanged
public typealias AnnotationEventEditingChanged = uniffi.epdf_plugin_annotation.AnnotationEvent.EditingChanged
public typealias AnnotationEventHoverChanged = uniffi.epdf_plugin_annotation.AnnotationEvent.HoverChanged
public typealias AnnotationEventThreadChanged = uniffi.epdf_plugin_annotation.AnnotationEvent.ThreadChanged
public typealias AnnotationEventWriteFailed = uniffi.epdf_plugin_annotation.AnnotationEvent.WriteFailed
public typealias AnnotationChangedEvent = uniffi.epdf_plugin_annotation.AnnotationChangedEvent
public typealias AnnotationDeletedEvent = uniffi.epdf_plugin_annotation.AnnotationDeletedEvent
public typealias AnnotationResyncedPages = uniffi.epdf_plugin_annotation.AnnotationResyncedPages
public typealias AnnotationResyncedPagesAll = uniffi.epdf_plugin_annotation.AnnotationResyncedPages.All
public typealias AnnotationResyncedPagesPages = uniffi.epdf_plugin_annotation.AnnotationResyncedPages.Pages
public typealias AnnotationThreadChange = uniffi.epdf_plugin_annotation.AnnotationThreadChange
public typealias AnnotationSelectionProps = uniffi.epdf_plugin_annotation.AnnotationSelectionProps
public typealias AnnotationTextFormat = uniffi.epdf_plugin_annotation.AnnotationTextFormat
public typealias AnnotationTool = uniffi.epdf_plugin_annotation.AnnotationTool
public typealias AnnotationStampSource = uniffi.epdf_plugin_annotation.AnnotationStampSource
public typealias AnnotationStampPlacement = uniffi.epdf_plugin_annotation.AnnotationStampPlacement
public typealias AnnotationAttachmentFile = uniffi.epdf_plugin_annotation.AnnotationAttachmentFile
public typealias AnnotationTextEndAnchor = uniffi.epdf_plugin_annotation.AnnotationTextEndAnchor
public typealias AnnotationPageQuads = uniffi.epdf_plugin_annotation.AnnotationPageQuads
public typealias AnnotationComment = uniffi.epdf_plugin_annotation.AnnotationComment
public typealias AnnotationCommentThread = uniffi.epdf_plugin_annotation.AnnotationCommentThread
public typealias AnnotationCommentPermissions = uniffi.epdf_plugin_annotation.AnnotationCommentPermissions
public typealias AnnotationThreadStatus = uniffi.epdf_plugin_annotation.AnnotationThreadStatus
public typealias AnnotationThreadDeleteResult = uniffi.epdf_plugin_annotation.AnnotationThreadDeleteResult
public typealias AnnotationThreadDeleteFailure = uniffi.epdf_plugin_annotation.AnnotationThreadDeleteFailure
public typealias AnnotationFilePromptRequest = uniffi.epdf_plugin_annotation.AnnotationFilePromptRequest
public typealias AnnotationPickedFile = uniffi.epdf_plugin_annotation.AnnotationPickedFile

/**
 * The host's file picker: asked for a file when a tool places one (a stamp
 * image, an attachment). `null` is the user cancelling. `pick` is called
 * off the main thread and may suspend for as long as the user takes; build
 * the answer from bytes with [AnnotationPickedFile]'s constructor, or from a
 * document the user picked with `AnnotationPickedFile(resolver, uri)`.
 */
public typealias AnnotationFilePicker = uniffi.epdf_plugin_annotation.AnnotationFilePicker
