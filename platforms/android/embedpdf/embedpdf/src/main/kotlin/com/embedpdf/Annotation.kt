package com.embedpdf

import kotlinx.coroutines.flow.Flow
import uniffi.epdf_plugin_annotation.AnnotationDraftsHandle
import uniffi.epdf_plugin_annotation.AnnotationEventListener
import uniffi.epdf_plugin_annotation.AnnotationHandle
import uniffi.epdf_plugin_annotation.AnnotationSelectionHandle
import uniffi.epdf_plugin_annotation.AnnotationThreadsHandle
import uniffi.epdf_plugin_annotation.AnnotationToolsHandle

// `AnnotationRef`, `PdfLinkTarget` and `PageDestination` are declared in
// Link.kt.
//
// The plugin speaks the engine's records: a record carries its ref, page,
// subtype, rect, text members and authority typed, and the rest as JSON
// (`AnnotationRecord.fieldsJson`). Reads hand it out as `AnnotationInfo`,
// which types the timestamps, the subtype, the blend mode and each kind's
// members. Create fields, patches, tool defaults and selection values are the
// engine's field bags: typed as `AnnotationDraft` and `AnnotationPatch`
// (`AnnotationDraft.kt`), or passed as JSON objects for what the typed forms
// don't cover.
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

/**
 * A document's annotations. Get one with [PdfViewer.annotations]; the viewer
 * needs `ViewerConfig(interaction = true, annotation = "")` (the settings JSON
 * over the defaults; `""` for the defaults). It keeps the viewer alive. After
 * its document closes, every call throws `instance-closed`.
 *
 * Pointer editing goes through the interaction hub: forward samples with
 * [InteractionCapability.dispatchPointer] and the plugin's own handlers draw,
 * select and move annotations under the active tool.
 *
 * Field bags (create fields, patches, tool defaults, selection values) cross
 * as the engine's JSON objects. Each JSON entry point stays when a typed one
 * joins it, as the way to set what the typed form doesn't cover.
 */
public class AnnotationCapability internal constructor(
    private val handle: AnnotationHandle,
    /**
     * The viewer this capability belongs to. A model that follows the
     * capability reads the viewer's state revisions through it.
     */
    public val viewer: PdfViewer,
) {
    /** Current annotation settings, including the open per-tool engine field bags. */
    public suspend fun settings(): String = viewer.call { handle.settings() }

    /** Merges a settings JSON object using the shared Rust settings store. */
    public suspend fun updateSettings(changesJson: String) {
        viewer.call { handle.updateSettings(changesJson) }
    }

    public suspend fun resetSettings() {
        viewer.call { handle.resetSettings() }
    }

    // Each sub-capability keeps the viewer alive, as this one does, so one
    // held on its own still answers. Nothing the viewer owns holds a
    // capability, so this makes no cycle; a handler the kernel keeps (a file
    // picker) that captures one does, as it would capturing this one.

    /** Select, group, rotate and restyle the selected annotations. */
    public val selection: AnnotationSelectionCapability
        get() = AnnotationSelectionCapability(handle.selection(), viewer)

    /** The authoring tools, their defaults and the snap settings. */
    public val tools: AnnotationToolsCapability
        get() = AnnotationToolsCapability(handle.tools(), viewer)

    /** Creation drafts, text editing, markup from text, stamps and placement. */
    public val drafts: AnnotationDraftsCapability
        get() = AnnotationDraftsCapability(handle.drafts(), viewer)

    /** Comment threads and the links attached to annotations. */
    public val threads: AnnotationThreadsCapability
        get() = AnnotationThreadsCapability(handle.threads(), viewer)

    public suspend fun get(ref: AnnotationRef): AnnotationInfo? = viewer.call { handle.get(ref) }?.let(::AnnotationInfo)

    public suspend fun list(filter: AnnotationFilter? = null): List<AnnotationInfo> =
        viewer.call { handle.list(filter) }.map(::AnnotationInfo)

    public suspend fun selected(): List<AnnotationInfo> = viewer.call { handle.selected() }.map(::AnnotationInfo)

    /**
     * A self-contained annotation bundle, including resource bytes encoded as
     * `$bytes`. Leaving out both filters selects all. Supplied pages and refs
     * contribute their union; an empty list contributes no members and does
     * not override the other filter.
     */
    public suspend fun export(refs: List<AnnotationRef>? = null, pages: List<PageRef>? = null): String =
        viewer.callCancellable { token -> handle.export(refs, pages, token) }

    /**
     * Copies a bundle using the engine's `AnnotationImportOptions` JSON
     * vocabulary.
     *
     * A copy that started lands even when the caller is cancelled, and the
     * caller then sees its cancellation rather than the result (see the
     * README's Cancellation section).
     */
    public suspend fun import(bundleJson: String, optionsJson: String = "{}"): AnnotationImportResult =
        viewer.callCancellable { token -> handle.import(bundleJson, optionsJson, token) }

    /** The topmost annotation under a page-space point (`getAt`). */
    public suspend fun hitTest(point: Point, page: PageRef): AnnotationInfo? =
        viewer.call { handle.hitTestAt(page, point) }?.let(::AnnotationInfo)

    /** Whether a change of this annotation is still on its way to the engine. */
    public suspend fun isPending(ref: AnnotationRef): Boolean = viewer.call { handle.isPending(ref) }

    /** The records mirror's status, as metadata, form and signature report it. */
    public suspend fun status(): ResourceStatus = viewer.call { handle.status() }

    /** Re-reads every annotation from the engine and waits for it. */
    public suspend fun refresh() {
        viewer.callCancellable { token -> handle.refresh(token) }
    }

    /** Waits until every load and page re-read in flight has settled. */
    public suspend fun whenSynced() {
        viewer.callCancellable { token -> handle.whenSynced(token) }
    }

    /**
     * Resolves with the created annotation's ref once the engine confirmed
     * it. [fieldsJson] is the engine's field bag for the subtype
     * (`{"subtype": "square", "box": …}`): the way to create a subtype, or set
     * a field, the typed drafts don't cover. [tool] picks the tool whose
     * defaults it starts from; [select] selects it afterwards, and leaving it
     * out is `false`, as in the engine (`AnnotationHandle::create` in
     * `crates/plugins/annotation/src/ffi/handle.rs`). Throws `invalid-input`
     * for a value the engine refuses, naming the field.
     *
     * A create that started lands even when the caller is cancelled, and the
     * caller then sees its cancellation rather than the ref (see the
     * README's Cancellation section).
     */
    public suspend fun create(fieldsJson: String, page: PageRef, tool: ToolId? = null, select: Boolean = false): AnnotationRef =
        viewer.callCancellable { token -> handle.create(page, fieldsJson, tool?.value, select, token) }

    /**
     * Resolves with the created annotation's ref once the engine confirmed
     * it. [draft] is one of the typed drafts: [InkAnnotationDraft],
     * [HighlightAnnotationDraft], [UnderlineAnnotationDraft],
     * [StrikeoutAnnotationDraft], [SquigglyAnnotationDraft],
     * [NoteAnnotationDraft], [FreeTextAnnotationDraft],
     * [SquareAnnotationDraft], [CircleAnnotationDraft], [LineAnnotationDraft],
     * [PolylineAnnotationDraft], [PolygonAnnotationDraft],
     * [CaretAnnotationDraft], [LinkAnnotationDraft] or
     * [RedactionAnnotationDraft]. [tool] picks the tool whose defaults it
     * starts from; [select] selects it afterwards. Throws `invalid-input` for
     * a value the engine refuses, naming the field, or for a number JSON
     * can't hold.
     *
     * A create that started lands even when the caller is cancelled, as with
     * the JSON form.
     */
    public suspend fun create(draft: AnnotationDraft, page: PageRef, tool: ToolId? = null, select: Boolean = false): AnnotationRef =
        create(draft.json(), page, tool, select)

    /**
     * Changes an annotation. [patchJson] is the engine's field bag: members it
     * leaves out keep their values, and `null` clears one. Throws
     * `invalid-input` for a value the engine refuses, including another
     * subtype's fields.
     *
     * A change that started lands even when the caller is cancelled (see the
     * README's Cancellation section).
     */
    public suspend fun update(ref: AnnotationRef, patchJson: String) {
        viewer.callCancellable { token -> handle.update(ref, patchJson, token) }
    }

    /**
     * Changes an annotation with the typed patch for its subtype:
     * [InkAnnotationPatch], [HighlightAnnotationPatch],
     * [UnderlineAnnotationPatch], [StrikeoutAnnotationPatch],
     * [SquigglyAnnotationPatch], [NoteAnnotationPatch],
     * [FreeTextAnnotationPatch], [SquareAnnotationPatch],
     * [CircleAnnotationPatch], [LineAnnotationPatch],
     * [PolylineAnnotationPatch], [PolygonAnnotationPatch],
     * [CaretAnnotationPatch], [LinkAnnotationPatch],
     * [RedactionAnnotationPatch], [StampAnnotationPatch] or
     * [FileAttachmentAnnotationPatch]. Members the patch leaves out keep
     * their values. Throws `invalid-input` for a value the engine refuses,
     * including a patch of another subtype's fields.
     *
     * A change that started lands even when the caller is cancelled, as with
     * the JSON form.
     */
    public suspend fun update(ref: AnnotationRef, patch: AnnotationPatch) {
        update(ref, patch.json())
    }

    /**
     * A delete that started lands even when the caller is cancelled (see the
     * README's Cancellation section).
     */
    public suspend fun delete(ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.delete(ref, token) }
    }

    /**
     * Moves [refs] together, in the order given, to [position] among their
     * page's annotations. The annotations and the neighbour a position names
     * must be on one page (`invalid-input` otherwise), and the neighbour must
     * exist (`not-found`).
     *
     * A move that started lands even when the caller is cancelled (see the
     * README's Cancellation section).
     */
    public suspend fun reorder(refs: List<AnnotationRef>, position: AnnotationPosition) {
        viewer.callCancellable { token -> handle.reorder(refs, position.json, token) }
    }

    public suspend fun canRead(): Boolean = viewer.call { handle.canRead() }

    public suspend fun canCreate(): Boolean = viewer.call { handle.canCreate() }

    public suspend fun canUpdate(ref: AnnotationRef): Boolean = viewer.call { handle.canUpdate(ref) }

    public suspend fun canDelete(ref: AnnotationRef): Boolean = viewer.call { handle.canDelete(ref) }

    /**
     * Installs the file picker a placing tool (a stamp, an attachment) asks
     * for its file. `null` removes it; without one, such a tool declines the
     * click. A file picked after the user switched tools is dropped. The
     * viewer keeps the picker until it is replaced or the document closes.
     */
    public suspend fun setFilePicker(picker: AnnotationFilePicker?) {
        viewer.call { handle.setFilePicker(picker) }
    }

    /**
     * Confirmed changes, whoever made them, and selection, draft, editing and
     * comment-thread changes. Completes when the document closes (at once if
     * it already has) or the viewer is destroyed. An [AnnotationEventCreated]
     * or [AnnotationEventUpdated] event's [AnnotationChangedEvent.annotationInfo]
     * is its annotation as an [AnnotationInfo]. Each collection is a new
     * subscription.
     */
    public fun observeEvents(): Flow<AnnotationEvent> = viewer.registry.stream { channel ->
        handle.subscribe(
            object : AnnotationEventListener {
                override fun onEvent(event: AnnotationEvent) {
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
 * The selected annotations of one document: from
 * [AnnotationCapability.selection].
 */
public class AnnotationSelectionCapability internal constructor(
    private val handle: AnnotationSelectionHandle,
    internal val viewer: PdfViewer,
) {
    /** Selects [refs], replacing the selection, or adding to it with [adding]. */
    public suspend fun select(refs: List<AnnotationRef>, adding: Boolean = false) {
        viewer.call { handle.select(refs, adding) }
    }

    /** Every annotation, or every one on [page]. */
    public suspend fun selectAll(page: PageRef? = null) {
        viewer.call { handle.selectAll(page) }
    }

    /** Every annotation in a page-space [rect]. */
    public suspend fun select(rect: Rect, page: PageRef) {
        viewer.call { handle.selectInRect(page, rect) }
    }

    public suspend fun clear() {
        viewer.call { handle.clear() }
    }

    public suspend fun refs(): List<AnnotationRef> = viewer.call { handle.refs() }

    public suspend fun props(): AnnotationSelectionProps = viewer.call { handle.props() }

    /**
     * [patchJson] is the engine's field bag; each member takes the fields its
     * kind has. A change that started lands even when the caller is
     * cancelled (see the README's Cancellation section).
     */
    public suspend fun update(patchJson: String): AnnotationBatchResult =
        viewer.callCancellable { token -> handle.update(patchJson, token) }

    /**
     * Changes every selected annotation with a typed patch. Each member takes
     * the fields its kind has, as with the JSON form, and a change that
     * started lands even when the caller is cancelled.
     */
    public suspend fun update(patch: AnnotationPatch): AnnotationBatchResult = update(patch.json())

    /**
     * A delete that started lands even when the caller is cancelled (see the
     * README's Cancellation section).
     */
    public suspend fun delete(): AnnotationBatchResult = viewer.callCancellable { token -> handle.delete(token) }

    /**
     * Sets, or with `null` clears, the selected annotations' links, as each
     * one's authority allows. A change that started lands even when the
     * caller is cancelled (see the README's Cancellation section).
     */
    public suspend fun updateLink(target: PdfLinkTarget?): AnnotationBatchResult =
        viewer.callCancellable { token -> handle.updateLink(target, token) }

    /**
     * A rotation that started lands even when the caller is cancelled (see
     * the README's Cancellation section).
     */
    public suspend fun rotate(degrees: Double) {
        viewer.callCancellable { token -> handle.rotateBy(degrees, token) }
    }

    public suspend fun resetRotation() {
        viewer.callCancellable { token -> handle.resetRotation(token) }
    }

    /** Turns [format] on or off across the selected free text. */
    public suspend fun toggle(format: AnnotationTextFormat) {
        viewer.callCancellable { token -> handle.toggleTextFormat(format, token) }
    }

    public suspend fun group() {
        viewer.callCancellable { token -> handle.group(token) }
    }

    public suspend fun ungroup() {
        viewer.callCancellable { token -> handle.ungroup(token) }
    }

    public suspend fun canGroup(): Boolean = viewer.call { handle.canGroup() }

    public suspend fun canUngroup(): Boolean = viewer.call { handle.canUngroup() }
}

/** The authoring tools of one document: from [AnnotationCapability.tools]. */
public class AnnotationToolsCapability internal constructor(
    private val handle: AnnotationToolsHandle,
    internal val viewer: PdfViewer,
) {
    /** Activate one through [InteractionCapability.activateTool]. */
    public suspend fun tools(): List<AnnotationTool> = viewer.call { handle.tools() }

    public suspend fun tool(id: ToolId): AnnotationTool? = viewer.call { handle.tool(id.value) }

    /** The engine's field bag a new annotation from this tool starts with, as JSON. */
    public suspend fun defaults(tool: ToolId): String = viewer.call { handle.defaults(tool.value) }

    /**
     * The tool's defaults as the patch of its subtype, read by that patch's
     * companion: `defaults(ToolId.INK, InkAnnotationPatch)`. What
     * [updateDefaults] wrote, read back typed.
     *
     * A default the tool doesn't set is left out of the patch (`null` or
     * [Nullable.Unchanged]), and one set to nothing is [Nullable.Clear]: the
     * engine leaves the first out of its JSON and writes `null` for the
     * second. A default the patch has no member for is left unread; the JSON
     * overload has every one. `common.nm` is the name the defaults hold,
     * which only the JSON overload can put there.
     *
     * Throws `invalid-input` when the defaults don't read as the patch: a
     * member of the patch holds a value of another shape, as it can after the
     * JSON overload stored one. Asking for another subtype's patch reads only
     * the members the two subtypes share, and throws only when a shared key
     * holds a value of another shape.
     */
    public suspend fun <P : AnnotationPatch> defaults(tool: ToolId, type: AnnotationPatch.Reader<P>): P {
        val json = defaults(tool)
        // Performance: the engine writes the defaults as a JSON string and
        // this parses it again on every call; nothing is kept between calls.
        // A style panel that reads a tool's defaults on every recomposition
        // keeps the typed value and reads it again when the defaults change.
        return try {
            type.fromJson(json)
        } catch (e: PdfException) {
            throw PdfException(
                PdfException.Code.INVALID_INPUT,
                "the defaults of ${tool.value} don't read as this patch: ${e.detail}",
                "DecodingError",
                e,
            )
        }
    }

    /**
     * Merges [changesJson], a field bag, over the tool's defaults, which every
     * annotation the tool creates starts from. A create keeps only the
     * defaults its subtype declares, and drops the rest without a word. A
     * `reply` or `groupId` stored here is the default for an annotation
     * created with this tool. A create without `tool` takes no defaults.
     */
    public suspend fun updateDefaults(changesJson: String, tool: ToolId) {
        viewer.call { handle.updateDefaults(tool.value, changesJson) }
    }

    /**
     * Merges [changes] over the tool's defaults, which every annotation the
     * tool creates starts from. Pass the patch of the tool's subtype
     * ([InkAnnotationPatch] for [ToolId.INK]); a create keeps only the
     * defaults its subtype declares, and drops the rest without a word.
     *
     * The patch's `common` is stored too. A `reply` or `groupId` set there is
     * the default for an annotation created with this tool: it becomes a reply
     * to that annotation or a member of that group unless its draft sets its
     * own value. A create without `tool` takes no defaults.
     */
    public suspend fun updateDefaults(changes: AnnotationPatch, tool: ToolId) {
        updateDefaults(changes.json(), tool)
    }

    /** What the tool's style panel shows. */
    public suspend fun properties(tool: ToolId): AnnotationSelectionProps = viewer.call { handle.properties(tool.value) }
}

/**
 * Creation drafts, text editing, markup, stamps and placement for one
 * document: from [AnnotationCapability.drafts].
 */
public class AnnotationDraftsCapability internal constructor(
    private val handle: AnnotationDraftsHandle,
    internal val viewer: PdfViewer,
) {
    /** The polygon or polyline being drawn. */
    public suspend fun creationDraft(): AnnotationCreationDraft? = viewer.call { handle.creationDraft() }

    /**
     * Commits the polygon or polyline being drawn. `null` when there was
     * nothing to finish. A commit that started lands even when the caller is
     * cancelled (see the README's Cancellation section).
     */
    public suspend fun finishCreationDraft(): AnnotationRef? = viewer.callCancellable { token -> handle.finishCreationDraft(token) }

    public suspend fun finishInkDraft() {
        viewer.call { handle.finishInkDraft() }
    }

    /**
     * A shape made click by click waits for its next click: a polygon or
     * polyline corner, a callout knee or box, a distance line placement. That
     * click is the shape's, also where it lands on an existing annotation.
     */
    public suspend fun hasCreationAwaitingClick(): Boolean = viewer.call { handle.hasCreationAwaitingClick() }

    /** Drops every gesture in progress: drafts, ghosts, placement previews. */
    public suspend fun cancel() {
        viewer.call { handle.cancel() }
    }

    public suspend fun beginTextEdit(ref: AnnotationRef) {
        viewer.call { handle.beginTextEdit(ref) }
    }

    /**
     * Starts editing the free text under a page-space point. [scale],
     * [rotation] and [zoom] describe the view the point came from. `false`
     * when no free text is there.
     */
    public suspend fun beginTextEdit(
        point: Point,
        page: PageRef,
        scale: Double? = null,
        rotation: Double? = null,
        zoom: Double? = null,
    ): Boolean = viewer.call { handle.beginTextEditAt(page, point, scale, rotation, zoom) }

    /**
     * Sends what was typed and stops editing. Text sent before the caller was
     * cancelled lands (see the README's Cancellation section).
     */
    public suspend fun endTextEdit() {
        viewer.callCancellable { token -> handle.endTextEdit(token) }
    }

    public suspend fun editingRef(): AnnotationRef? = viewer.call { handle.editingRef() }

    /** Keystroke-level text, sent once typing pauses or at [endTextEdit]. */
    public suspend fun draftContents(text: String, ref: AnnotationRef) {
        viewer.call { handle.draftContents(ref, text) }
    }

    /**
     * [range] is in UTF-16 code units, as a `String` index is: `2 until 5`
     * selects three. `null` clears it. A bound below zero, or a reversed
     * range, throws `invalid-input`.
     */
    public suspend fun setTextSelection(range: IntRange?, ref: AnnotationRef) {
        val start = range?.first?.toWireUInt("text selection start")
        val end = range?.let { (it.last.toLong() + 1).toWireUInt("text selection end") }
        if (range != null && range.last < range.first - 1) {
            throw PdfException(PdfException.Code.INVALID_INPUT, "text selection $range is reversed", name = "RangeError")
        }
        viewer.call { handle.setTextSelection(ref, start, end) }
    }

    /**
     * Highlight, underline, squiggly or strikeout over text quads
     * ([quadsJson], the engine's `Quad[]`). [preset] picks a tool's defaults
     * other than the subtype's own. The new annotation arrives on
     * [AnnotationCapability.observeEvents].
     */
    public suspend fun createMarkup(subtype: AnnotationSubtype, quadsJson: String, page: PageRef, preset: ToolId? = null) {
        viewer.call { handle.createMarkup(page, subtype.value, quadsJson, preset?.value) }
    }

    /** An insert caret at the end of a run of text. */
    public suspend fun createCaret(anchor: AnnotationTextEndAnchor, page: PageRef) {
        viewer.call { handle.createCaret(page, anchor) }
    }

    /** A strikeout over [quadsJson] paired with an insert caret at [anchor]. */
    public suspend fun createReplaceText(quadsJson: String, anchor: AnnotationTextEndAnchor, page: PageRef, preset: ToolId? = null) {
        viewer.call { handle.createReplaceText(page, quadsJson, anchor, preset?.value) }
    }

    public suspend fun previewMarkup(subtype: AnnotationSubtype, pages: List<AnnotationPageQuads>, preset: ToolId? = null) {
        viewer.call { handle.previewMarkup(subtype.value, pages, preset?.value) }
    }

    public suspend fun clearMarkupPreview() {
        viewer.call { handle.clearMarkupPreview() }
    }

    /**
     * Marks the current text selection (needs `ViewerConfig.selection`) with
     * a markup tool, one annotation per page. Empty when nothing is selected.
     * [clear] drops the text selection afterwards; leaving it out is `true`,
     * as in the engine (`AnnotationDraftsHandle::create_from_selection` in
     * `crates/plugins/annotation/src/ffi/drafts.rs`).
     *
     * Marks that started land even when the caller is cancelled (see the
     * README's Cancellation section).
     */
    public suspend fun createFromSelection(tool: ToolId, clear: Boolean = true): List<AnnotationRef> =
        viewer.callCancellable { token -> handle.createFromSelection(tool.value, clear, token) }

    /** Arms a stamp: the next click on a page places it. */
    public suspend fun armStamp(source: AnnotationStampSource, targetWidth: Double? = null) {
        viewer.callCancellable { token -> handle.armStamp(source, targetWidth, token) }
    }

    public suspend fun disarmStamp() {
        viewer.call { handle.disarmStamp() }
    }

    public suspend fun hasArmedStamp(): Boolean = viewer.call { handle.hasArmedStamp() }

    /**
     * Places a stamp centred on a point, without arming. A stamp that started
     * lands even when the caller is cancelled (see the README's Cancellation
     * section).
     */
    public suspend fun placeStamp(
        source: AnnotationStampSource,
        placement: AnnotationStampPlacement,
        targetWidth: Double? = null,
    ): AnnotationRef = viewer.callCancellable { token -> handle.placeStamp(source, placement, targetWidth, token) }

    /**
     * `false` when no stamp is armed. The new annotation arrives on
     * [AnnotationCapability.observeEvents].
     */
    public suspend fun placeArmedStamp(point: Point, page: PageRef, displayRotation: Double? = null): Boolean =
        viewer.call { handle.placeArmedStamp(page, point, displayRotation) }

    /**
     * Asks the file picker for the active tool's file and places it at
     * [point]. `false` when the active tool takes no picked file or no picker
     * is installed.
     */
    public suspend fun requestStamp(point: Point, page: PageRef, displayRotation: Double? = null): Boolean =
        viewer.call { handle.requestStampAt(page, point, displayRotation) }

    public suspend fun hoverGhost(tool: ToolId, point: Point, page: PageRef, displayRotation: Double? = null, zoom: Double? = null) {
        viewer.call { handle.hoverGhostAt(tool.value, page, point, displayRotation, zoom) }
    }

    public suspend fun clearGhost() {
        viewer.call { handle.clearGhost() }
    }

    /** A sibling's drag-to-place gesture with one of this plugin's tools. */
    public suspend fun previewPlacement(tool: ToolId, from: Point, to: Point, page: PageRef) {
        viewer.call { handle.previewPlacement(tool.value, page, from, to) }
    }

    public suspend fun clearPlacementPreview() {
        viewer.call { handle.clearPlacementPreview() }
    }

    /**
     * Click-places with the active tool (a note, a stamp). `false` when the
     * tool places nothing on click.
     */
    public suspend fun place(point: Point, page: PageRef, displayRotation: Double? = null, zoom: Double? = null): Boolean =
        viewer.call { handle.placeAt(page, point, displayRotation, zoom) }

    /**
     * A file attachment annotation at [point], embedding [file]. Build the file
     * from bytes with its constructor, or from a document the user picked
     * with `AnnotationAttachmentFile(resolver, uri)`. An attachment that
     * started lands even when the caller is cancelled (see the README's
     * Cancellation section).
     */
    public suspend fun createAttachment(file: AnnotationAttachmentFile, point: Point, page: PageRef): AnnotationRef =
        viewer.callCancellable { token -> handle.createAttachment(page, point, file, token) }

    /** An attachment annotation's embedded bytes. */
    public suspend fun readAttachment(ref: AnnotationRef): ByteArray =
        viewer.callCancellable { token -> handle.readAttachment(ref, token) }
}

/**
 * Comment threads and annotation links of one document: from
 * [AnnotationCapability.threads].
 *
 * Each change that started lands even when the caller is cancelled, and the
 * caller then sees its cancellation rather than the result (see the README's
 * Cancellation section).
 */
public class AnnotationThreadsCapability internal constructor(
    private val handle: AnnotationThreadsHandle,
    internal val viewer: PdfViewer,
) {
    public suspend fun threads(): List<AnnotationCommentThread> = viewer.call { handle.threads() }

    /** The thread any member of it belongs to. */
    public suspend fun thread(ref: AnnotationRef): AnnotationCommentThread? = viewer.call { handle.thread(ref) }

    public suspend fun reply(ref: AnnotationRef, text: String): AnnotationRef =
        viewer.callCancellable { token -> handle.reply(ref, text, token) }

    public suspend fun setText(text: String, ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.setText(ref, text, token) }
    }

    public suspend fun setStatus(status: AnnotationThreadStatus, ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.setStatus(ref, status, token) }
    }

    public suspend fun setMarked(marked: Boolean, ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.setMarked(ref, marked, token) }
    }

    public suspend fun delete(ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.delete(ref, token) }
    }

    public suspend fun deleteThread(ref: AnnotationRef): AnnotationThreadDeleteResult =
        viewer.callCancellable { token -> handle.deleteThread(ref, token) }

    public suspend fun permissions(ref: AnnotationRef): AnnotationCommentPermissions = viewer.call { handle.permissions(ref) }

    /**
     * The link attached to an annotation (a shape that acts as a link), as the
     * engine's `LinkTarget` JSON.
     */
    public suspend fun link(ref: AnnotationRef): String? = viewer.call { handle.link(ref) }

    /**
     * The link attached to an annotation, or `null` when it has none. Throws
     * `operation-failed` for a target [AnnotationLinkTarget] can't hold
     * (another file, a launch, a script); read those with [link].
     */
    public suspend fun linkTarget(ref: AnnotationRef): AnnotationLinkTarget? = link(ref)?.let(AnnotationLinkTarget::fromJson)

    /** [targetJson] is the engine's `LinkTarget`. */
    public suspend fun setLink(targetJson: String, ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.setLink(ref, targetJson, token) }
    }

    public suspend fun setLink(target: AnnotationLinkTarget, ref: AnnotationRef) {
        setLink(target.json(), ref)
    }

    public suspend fun clearLink(ref: AnnotationRef) {
        viewer.callCancellable { token -> handle.clearLink(ref, token) }
    }
}

/**
 * The annotation capability for [document]. Throws `unsupported` when the
 * viewer was built without the annotation plugin, whatever the document's
 * state.
 */
public suspend fun PdfViewer.annotations(document: DocumentInfo): AnnotationCapability =
    AnnotationCapability(call { host.annotation(document.id) }, this)
