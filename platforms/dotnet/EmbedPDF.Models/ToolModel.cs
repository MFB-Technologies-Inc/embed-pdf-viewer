namespace EmbedPDF.Models;

/// <summary>
/// An observable model of a document's interaction tools, which a view
/// reads: which are registered, which is active, and which the user may use
/// now.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Tools"/>, <see cref="ActiveToolId"/> and <see cref="Usable"/>
/// are snapshots of the hub, read again after each state revision and each
/// <see cref="InteractionEvent.ToolChanged"/> event while
/// <see cref="RunAsync"/> runs, and after each action. A snapshot is one
/// revision behind the hub until its refresh lands; the
/// <see cref="InteractionCapability"/> reads answer with the hub's current
/// value. A refresh that started earlier never overwrites one that started
/// later, and that rule is the only arbitration between the reads. A failed
/// read, as when the document closes, empties the model and sets
/// <see cref="Error"/>; <see cref="RunAsync"/> ends with one more refresh,
/// so a closed document leaves the model empty.
/// </para>
/// <para>
/// <see cref="ActivateAsync"/> and <see cref="ActivateDefaultAsync"/> write
/// <see cref="ActiveToolId"/> themselves when the hub accepts, then start one
/// refresh for <see cref="Tools"/> and <see cref="Usable"/>. So
/// <see cref="ActiveToolId"/> holds the action's result when the action
/// returns, with or without <see cref="RunAsync"/> running, and
/// <see cref="Tools"/> and <see cref="Usable"/> can trail it by one read:
/// the action's refresh is dropped when a newer read supersedes it or the
/// action's token is cancelled, and the newer read, or the next one
/// <see cref="RunAsync"/> makes, brings them. After an action whose token was
/// cancelled, without <see cref="RunAsync"/> running, call
/// <see cref="RefreshAsync"/>.
/// </para>
/// <para>
/// <see cref="Submitting"/> is <c>true</c> while the hub call of an action is
/// out, and <c>false</c> again before the action's refresh starts. A second
/// action asked for while it is <c>true</c> is ignored.
/// </para>
/// <para>
/// <see cref="Usable"/> holds each registered tool whose
/// <c>CanUseToolAsync</c> is <c>true</c>. A tool registered or removed is a
/// state revision, so <see cref="Usable"/> follows it while
/// <see cref="RunAsync"/> runs. A tool's permission can start to answer
/// differently with no revision and no event: the document's rights after an
/// unlock or a signing, or a permission the app registered. Call
/// <see cref="RefreshAsync"/> after such a change.
/// </para>
/// <para>
/// <see cref="Error"/> is the latest failure of an action or a read. A
/// successful action clears it. A successful background refresh clears the
/// error of an earlier read and keeps the error of an action. A caller that
/// polls it can miss a failure one of those cleared.
/// </para>
/// </remarks>
public sealed class ToolModel : ObservableModel
{
    readonly InteractionCapability interaction;
    long observation;
    IReadOnlyList<InteractionTool> tools = [];
    ToolId? activeToolId;
    IReadOnlySet<ToolId> usable = new HashSet<ToolId>();
    PdfException? error;
    bool submitting;
    /// <summary>Whether <see cref="Error"/> came from a read, which the next successful read clears.</summary>
    bool readFailed;

    /// <param name="interaction">The document's interaction capability. The model does not dispose it.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public ToolModel(InteractionCapability interaction, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        this.interaction = interaction;
    }

    /// <summary>The registered tools, in the hub's order.</summary>
    public IReadOnlyList<InteractionTool> Tools => tools;

    /// <summary><c>null</c> before the first read lands and after a failed one.</summary>
    public ToolId? ActiveToolId { get => activeToolId; private set => Set(ref activeToolId, value); }

    /// <summary>The registered tools the user may use now.</summary>
    public IReadOnlySet<ToolId> Usable => usable;

    /// <summary>The latest failure of an action or a read.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    /// <summary>Whether the hub call of an <see cref="ActivateAsync"/> or <see cref="ActivateDefaultAsync"/> is out.</summary>
    public bool Submitting { get => submitting; private set => Set(ref submitting, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>Runs with <see cref="Submitting"/> set, right before an action's hub call. Tests read the buttons' state here.</summary>
    internal Func<Task>? HubCallStarting { get; set; }

    /// <summary>
    /// Observes until the document closes or <paramref name="cancellationToken"/>
    /// is cancelled, and then returns without throwing. A later call
    /// supersedes an earlier one still running.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        var session = ++observation;
        try
        {
            Task RefreshIfCurrent() => observation == session ? RefreshAsync(cancellationToken) : Task.CompletedTask;
            await CapabilityObservation.ObserveAsync(
                interaction.Viewer, interaction.ObserveEvents,
                RefreshIfCurrent,
                // Performance: runs for every coalesced state revision, and
                // most are a scroll or a zoom that leaves the tools alone. The
                // revision stream does not say what changed, so each one costs
                // a full read (see RefreshAsync).
                RefreshIfCurrent,
                // Gestures and cursor moves leave the tools alone.
                item => item is InteractionEvent.ToolChanged ? RefreshIfCurrent() : Task.CompletedTask,
                cancellationToken);
            if (observation == session) await RefreshAsync(cancellationToken);
        }
        finally
        {
            if (observation == session) Snapshots.Invalidate();
        }
    }

    /// <summary>Reads the hub once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var readTools = await interaction.GetToolsAsync();
            var active = await interaction.GetActiveToolIdAsync();
            // Performance: one CanUseToolAsync round trip to the kernel thread
            // per registered tool, one after another, on every coalesced state
            // revision. With the annotation plugin's tools that is about 20
            // calls per read, and a permission the app registered is asked at
            // that rate. A single engine call that answers for every tool
            // removes the round trips.
            var readUsable = new HashSet<ToolId>();
            foreach (var tool in readTools)
            {
                if (await interaction.CanUseToolAsync(tool.Id)) readUsable.Add(tool.Id);
            }
            return (readTools, active, readUsable);
        }, snapshot =>
        {
            SetTools(snapshot.readTools);
            ActiveToolId = snapshot.active;
            SetUsable(snapshot.readUsable);
            if (readFailed)
            {
                readFailed = false;
                Error = null;
            }
        }, failure =>
        {
            SetTools([]);
            ActiveToolId = null;
            SetUsable(new HashSet<ToolId>());
            Error = ModelErrors.Control(failure);
            readFailed = true;
        }, cancellationToken);
    }

    /// <summary>
    /// Makes <paramref name="id"/> the active tool. <see cref="ActiveToolId"/>
    /// is <paramref name="id"/> when this returns after the hub accepted. An id
    /// nobody registered sets <see cref="Error"/> to <c>not-found</c> and
    /// leaves <see cref="ActiveToolId"/>. Cancelling
    /// <paramref name="cancellationToken"/> drops the action's refresh.
    /// </summary>
    public Task ActivateAsync(ToolId id, CancellationToken cancellationToken = default) =>
        SubmitAsync(async () =>
        {
            await interaction.ActivateToolAsync(id, cancellationToken);
            return id;
        }, cancellationToken);

    /// <summary>
    /// Returns to the hub's default tool. <see cref="ActiveToolId"/> is that
    /// tool when this returns after the hub accepted.
    /// </summary>
    public Task ActivateDefaultAsync(CancellationToken cancellationToken = default) =>
        SubmitAsync(async () =>
        {
            await interaction.ActivateDefaultToolAsync(cancellationToken);
            // Performance: one more round trip, inside the time Submitting is
            // set, because the hub call does not say which tool is the default.
            return await interaction.GetActiveToolIdAsync(cancellationToken);
        }, cancellationToken);

    /// <summary>Whether a picker shows <paramref name="id"/>'s button as on: it is <see cref="ActiveToolId"/>.</summary>
    public bool IsSelected(ToolId id) => activeToolId == id;

    /// <summary>
    /// Whether a picker disables <paramref name="id"/>'s button:
    /// <see cref="Submitting"/> is <c>true</c>, or the tool is not in
    /// <see cref="Usable"/> and not active. The active tool's button stays
    /// enabled when its tool stops being usable, so a tap on it can always
    /// return to the default tool. An id nobody registered is disabled.
    /// </summary>
    public bool IsDisabled(ToolId id) => submitting || (!usable.Contains(id) && !IsSelected(id));

    /// <summary>
    /// A tap on <paramref name="id"/>'s button in a picker: the active tool's
    /// button returns to the default tool, any other activates its tool.
    /// </summary>
    public async Task ButtonTappedAsync(ToolId id, CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        if (IsSelected(id)) await ActivateDefaultAsync(cancellationToken);
        else await ActivateAsync(id, cancellationToken);
    }

    /// <summary>
    /// Runs one action's hub call with <see cref="Submitting"/> set, writes the
    /// active tool it returns, and then refreshes. Nothing suspends between
    /// that write and the start of the refresh, so a read that started before
    /// the hub answered is superseded and cannot put the old tool back.
    /// </summary>
    async Task SubmitAsync(Func<Task<ToolId>> action, CancellationToken cancellationToken)
    {
        await Home.SwitchTo();
        if (submitting) return;
        Submitting = true;
        // The hub call can complete before it returns when the engine answers
        // at once. Yielding first keeps the action in flight until the home
        // context's next turn, as Swift's hop off the main actor does, so a
        // second tap made in the same turn is ignored rather than undoing
        // this one.
        await Task.Yield();
        if (HubCallStarting is { } starting) await starting();
        ToolId active;
        try
        {
            active = await action();
        }
        catch (Exception failure)
        {
            Submitting = false;
            Error = ModelErrors.Control(failure);
            readFailed = false;
            return;
        }
        Submitting = false;
        Error = null;
        readFailed = false;
        ActiveToolId = active;
        // Performance: a full read after every action, and while RunAsync runs
        // the action's own ToolChanged event starts a second one, the later
        // superseding the earlier when they overlap.
        await RefreshAsync(cancellationToken);
    }

    void SetTools(IReadOnlyList<InteractionTool> value)
    {
        if (Structural.SameTools(tools, value)) return;
        tools = value;
        Raise(nameof(Tools));
    }

    void SetUsable(HashSet<ToolId> value)
    {
        if (value.SetEquals(usable)) return;
        usable = value;
        Raise(nameof(Usable));
    }
}
