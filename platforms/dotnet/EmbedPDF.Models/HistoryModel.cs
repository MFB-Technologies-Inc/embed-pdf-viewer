namespace EmbedPDF.Models;

/// <summary>An observable model of a document's undo history, which an undo and a redo button read.</summary>
/// <remarks>
/// <para>
/// <see cref="CanUndo"/>, <see cref="CanRedo"/>, the labels and the titles
/// are snapshots of the kernel, read again after each state revision and
/// history event while <see cref="RunAsync"/> runs, and after each action.
/// A snapshot is one revision behind the kernel until its refresh lands. A
/// refresh that started earlier never overwrites one that started later. A
/// failed read, as when the document closes, turns both buttons off and sets
/// <see cref="Error"/>; <see cref="RunAsync"/> ends with one more refresh.
/// </para>
/// <para>
/// <see cref="Error"/> is the latest failed action, failed read or reported
/// <see cref="HistoryEvent.UndoFailed"/>; an action clears it when it
/// starts, and an <see cref="HistoryEvent.Undone"/> clears it.
/// <see cref="Notice"/> says when an undo or a redo skipped changes it
/// could not apply. The titles are English ("Undo", "Redo", and "Undo " plus
/// the translated label when a translator is given); an app that words them
/// otherwise builds its own from <see cref="UndoLabel"/> and
/// <see cref="RedoLabel"/>.
/// </para>
/// </remarks>
public sealed class HistoryModel : ObservableModel
{
    readonly PdfViewer viewer;
    readonly HistoryCapability history;
    readonly I18nCapability? translator;
    long observation;
    bool canUndo;
    bool canRedo;
    HistoryChangeLabel? undoLabel;
    HistoryChangeLabel? redoLabel;
    string undoTitle = "Undo";
    string redoTitle = "Redo";
    bool submitting;
    string? notice;
    PdfException? error;

    /// <param name="viewer">The viewer the document belongs to, for its state revisions. The model does not dispose it.</param>
    /// <param name="history">The document's history capability. The model does not dispose it.</param>
    /// <param name="translator">Translates the change labels into the titles; <c>null</c> leaves the plain verbs.</param>
    /// <param name="context">The home context; see <see cref="ObservableModel"/>.</param>
    public HistoryModel(PdfViewer viewer, HistoryCapability history, I18nCapability? translator = null, SynchronizationContext? context = null)
        : base(context)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(history);
        this.viewer = viewer;
        this.history = history;
        this.translator = translator;
    }

    /// <summary>Whether there is a change to undo.</summary>
    public bool CanUndo { get => canUndo; private set => Set(ref canUndo, value); }

    /// <summary>Whether there is a change to redo.</summary>
    public bool CanRedo { get => canRedo; private set => Set(ref canRedo, value); }

    /// <summary>What an undo would revert.</summary>
    public HistoryChangeLabel? UndoLabel => undoLabel;

    /// <summary>What a redo would apply again.</summary>
    public HistoryChangeLabel? RedoLabel => redoLabel;

    /// <summary>The undo button's title.</summary>
    public string UndoTitle { get => undoTitle; private set => Set(ref undoTitle, value); }

    /// <summary>The redo button's title.</summary>
    public string RedoTitle { get => redoTitle; private set => Set(ref redoTitle, value); }

    /// <summary>Whether an undo or a redo is out.</summary>
    public bool Submitting { get => submitting; private set => Set(ref submitting, value); }

    /// <summary>Set when an undo or a redo skipped changes it could not apply.</summary>
    public string? Notice { get => notice; private set => Set(ref notice, value); }

    /// <summary>The latest failure.</summary>
    public PdfException? Error { get => error; private set => Set(ref error, value); }

    internal SnapshotRefresh Snapshots { get; } = new();

    /// <summary>
    /// Observes until the document closes or <paramref name="cancellationToken"/>
    /// is cancelled, and then returns without throwing.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        var session = ++observation;
        try
        {
            Task RefreshIfCurrent() => observation == session ? RefreshAsync(cancellationToken) : Task.CompletedTask;
            await CapabilityObservation.ObserveAsync(
                viewer, history.ObserveEvents, RefreshIfCurrent, RefreshIfCurrent,
                item => observation == session ? ReceiveAsync(item, cancellationToken) : Task.CompletedTask,
                cancellationToken);
            if (observation == session) await RefreshAsync(cancellationToken);
        }
        finally
        {
            if (observation == session) Snapshots.Invalidate();
        }
    }

    /// <summary>Reads the history once and applies the result unless a newer read overtakes it.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await Home.SwitchTo();
        await Snapshots.RunAsync(async () =>
        {
            var readUndo = await history.CanUndoAsync();
            var readRedo = await history.CanRedoAsync();
            var undo = await history.GetUndoLabelAsync();
            var redo = await history.GetRedoLabelAsync();
            var undoText = await TitleAsync(undo, redo: false);
            var redoText = await TitleAsync(redo, redo: true);
            return (readUndo, readRedo, undo, redo, undoText, redoText);
        }, snapshot =>
        {
            // Most revisions are a scroll or a zoom: write only what changed.
            CanUndo = snapshot.readUndo;
            CanRedo = snapshot.readRedo;
            if (!Structural.SameLabel(undoLabel, snapshot.undo))
            {
                undoLabel = snapshot.undo;
                Raise(nameof(UndoLabel));
            }
            if (!Structural.SameLabel(redoLabel, snapshot.redo))
            {
                redoLabel = snapshot.redo;
                Raise(nameof(RedoLabel));
            }
            UndoTitle = snapshot.undoText;
            RedoTitle = snapshot.redoText;
        }, failure =>
        {
            CanUndo = false;
            CanRedo = false;
            Error = ModelErrors.Control(failure);
        }, cancellationToken);
    }

    /// <summary>Undoes the latest change. Ignored while another action is out; a failure lands in <see cref="Error"/>.</summary>
    public Task UndoAsync() => SubmitAsync(() => history.UndoAsync());

    /// <summary>Redoes the latest undone change. Ignored while another action is out; a failure lands in <see cref="Error"/>.</summary>
    public Task RedoAsync() => SubmitAsync(() => history.RedoAsync());

    internal async Task ReceiveAsync(HistoryEvent item, CancellationToken cancellationToken = default)
    {
        switch (item)
        {
            case HistoryEvent.UndoFailed failed:
                Error = new PdfException("PluginError", failed.Error.Code, failed.Error.Message);
                Notice = null;
                break;
            case HistoryEvent.Undone undone:
                Error = null;
                Notice = undone.Skipped == 0 ? null
                    : undone.Redo ? "Some changes could not be redone." : "Some changes could not be undone.";
                break;
        }
        await RefreshAsync(cancellationToken);
    }

    async Task SubmitAsync(Func<Task> action)
    {
        await Home.SwitchTo();
        if (submitting) return;
        Submitting = true;
        Error = null;
        Notice = null;
        try
        {
            try { await action(); }
            catch (Exception failure) { Error = ModelErrors.Control(failure); }
            await RefreshAsync();
        }
        finally
        {
            Submitting = false;
        }
    }

    async Task<string> TitleAsync(HistoryChangeLabel? label, bool redo)
    {
        var verb = redo ? "Redo" : "Undo";
        if (label is null || translator is null) return verb;
        var parameters = label.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value switch
            {
                HistoryLabelValue.Number number => (I18nParam)new I18nParam.Number(number.Value),
                HistoryLabelValue.Text text => new I18nParam.Text(text.Value),
                _ => new I18nParam.Text(pair.Value.ToString() ?? ""),
            });
        var description = await translator.TranslateAsync(label.Key, parameters, "change");
        return $"{verb} {description}";
    }
}
