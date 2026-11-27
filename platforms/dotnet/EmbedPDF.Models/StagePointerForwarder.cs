namespace EmbedPDF.Models;

/// <summary>
/// Turns a reader's touches into pointer samples for the interaction hub.
/// Each sample carries the touch's viewport point and the page the stage
/// finds under it, and reaches the hub after every sample sent before it.
/// Moves that arrive while another call is in flight collapse into the
/// newest one, and a collapsed move stays with its own gesture: the hub gets
/// a gesture's down, its newest move, then its up or cancel, with no sample
/// of another gesture between them. Use from the model's home context.
/// </summary>
internal sealed class StagePointerForwarder(StageCapability stage, InteractionCapability interaction)
{
    /// <summary>
    /// Orders the samples. Each one leaves the home context while the stage
    /// finds its page, so without this an up could reach the hub before the
    /// down it closes.
    /// </summary>
    readonly SerialCalls calls = new();

    /// <summary>A move waiting in the chain. The call that sends it reads <see cref="Input"/> when it runs.</summary>
    sealed class WaitingMove(PointerInput input)
    {
        public PointerInput? Input { get; set; } = input;
    }

    /// <summary>
    /// The waiting move later moves still collapse into. A down, an up, a
    /// cancel or a tap lets go of it, so a move after one of them queues
    /// behind that call instead of joining a move queued ahead of it.
    /// </summary>
    WaitingMove? waitingMove;
    /// <summary>Where the newest touch was. A cancel has no location of its own.</summary>
    PointerInput lastInput;
    /// <summary>Whether a down has been asked for with no up or cancel after it.</summary>
    bool gestureOpen;
    /// <summary>Whether <see cref="Interrupt"/> closed the gesture. Until the next down or tap, a move, an up and a cancel send nothing.</summary>
    bool interrupted;

    /// <summary>
    /// Receives each page lookup's outcome: <c>null</c> for one that answered,
    /// the error for one that failed. A sample whose lookup failed still goes
    /// out, with no page.
    /// </summary>
    public Action<PdfException?>? LookedUp { get; set; }

    /// <summary>Runs as each sample is handed to the hub. Tests record them.</summary>
    public Action<PointerSample>? Dispatched { get; set; }

    public void Down(PointerInput input)
    {
        // An earlier gesture's waiting move stays queued ahead of its up.
        waitingMove = null;
        interrupted = false;
        gestureOpen = true;
        lastInput = input;
        _ = calls.Enqueue(() => SendAsync(PointerPhase.Down, input));
    }

    public void Move(PointerInput input)
    {
        if (interrupted) return;
        lastInput = input;
        if (waitingMove is not null)
        {
            waitingMove.Input = input;
            return;
        }
        var move = new WaitingMove(input);
        waitingMove = move;
        _ = calls.Enqueue(async () =>
        {
            // Let go before the send suspends: a move that arrives during it
            // queues a call of its own.
            if (ReferenceEquals(waitingMove, move)) waitingMove = null;
            if (move.Input is not { } location) return;
            await SendAsync(PointerPhase.Move, location);
        });
    }

    /// <summary>A move still waiting is dispatched first, so the gesture ends where the touch last was.</summary>
    public void Up(PointerInput input)
    {
        if (interrupted) return;
        waitingMove = null;
        gestureOpen = false;
        lastInput = input;
        _ = calls.Enqueue(() => SendAsync(PointerPhase.Up, input));
    }

    /// <summary>Drops this gesture's move if one still waits and sends a cancel at the last location.</summary>
    public void Cancel()
    {
        if (interrupted) return;
        if (waitingMove is not null) waitingMove.Input = null;
        waitingMove = null;
        gestureOpen = false;
        var input = lastInput;
        _ = calls.Enqueue(() => SendAsync(PointerPhase.Cancel, input));
    }

    /// <summary>
    /// Cancels the open gesture, if there is one, and mutes the rest of its
    /// touch: until the next down or tap, a move, an up and a cancel send
    /// nothing. Does nothing with no gesture open.
    /// </summary>
    public void Interrupt()
    {
        if (!gestureOpen) return;
        Cancel();
        interrupted = true;
    }

    /// <summary>
    /// A down and an up at one point, both with a click count of 1. A gesture
    /// still open is cancelled first, as <see cref="Cancel"/> does, so the hub
    /// gets no second down inside it.
    /// </summary>
    public void Tap(PointerInput input)
    {
        if (gestureOpen) Cancel();
        waitingMove = null;
        interrupted = false;
        gestureOpen = false;
        lastInput = input;
        _ = calls.Enqueue(async () =>
        {
            var page = await PageHitAsync(input);
            Dispatch(Sample(PointerPhase.Down, input, page, 1));
            Dispatch(Sample(PointerPhase.Up, input, page, 1));
        });
    }

    /// <summary>Returns once every sample asked for so far has been handed to the hub.</summary>
    public Task FinishedAsync() => calls.Enqueue(() => { });

    async Task SendAsync(PointerPhase phase, PointerInput input) =>
        Dispatch(Sample(phase, input, await PageHitAsync(input), null));

    async Task<PointerPageHit?> PageHitAsync(PointerInput input)
    {
        try
        {
            // Performance: every sample awaits this lookup before its
            // dispatch, so a sample costs a round trip to the stage on top of
            // the dispatch. The page under a drag rarely changes between
            // moves; mapping the point through the last hit's page while the
            // camera stands still would save the lookup.
            var hit = await stage.GetPageAtAsync(input.Point);
            LookedUp?.Invoke(null);
            return hit is null ? null
                : new PointerPageHit(hit.Page, hit.Point) { Scale = hit.Scale, Rotation = hit.Rotation, Zoom = hit.Zoom };
        }
        catch (Exception failure)
        {
            LookedUp?.Invoke(ModelErrors.Control(failure));
            return null;
        }
    }

    static PointerSample Sample(PointerPhase phase, PointerInput input, PointerPageHit? page, int? clickCount) =>
        new(phase, input.Point, input.Modifiers) { Page = page, ClickCount = clickCount, PointerType = input.Kind };

    void Dispatch(PointerSample sample)
    {
        interaction.DispatchPointer(sample);
        Dispatched?.Invoke(sample);
    }
}
