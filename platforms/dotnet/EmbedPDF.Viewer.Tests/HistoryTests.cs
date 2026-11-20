using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>Port of <c>HistoryTests.swift</c>.</summary>
public sealed class HistoryTests
{
    [Fact]
    public async Task UndoRedoTracksPageEditsAndDocumentsIndependently()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { History = true, PageEdit = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        var other = await viewer.OpenAsync(Fixture.Data("report"));
        using var history = await viewer.HistoryAsync(document);
        using var otherHistory = await viewer.HistoryAsync(other);
        using var edit = await viewer.PageEditAsync(document);
        var first = (await viewer.GetPagesAsync(document))[0].Page;
        var undone = Fixture.First(history.ObserveEvents(), e => e as HistoryEvent.Undone);
        _ = await edit.RotateAsync([first], PageEditTurn.Clockwise);
        Assert.True(await history.CanUndoAsync());
        Assert.False(await otherHistory.CanUndoAsync());
        var label = Assert.IsType<HistoryChangeLabel>(await history.GetUndoLabelAsync());
        Assert.NotEmpty(label.Key);
        await history.UndoAsync();
        Assert.False((await undone).Redo);
        Assert.Equal(0, (await viewer.GetPagesAsync(document))[0].Rotation);
        Assert.True(await history.CanRedoAsync());
        Assert.NotNull(await history.GetRedoLabelAsync());
        var redone = Fixture.First(history.ObserveEvents(), e => e as HistoryEvent.Undone);
        await history.RedoAsync();
        Assert.True((await redone).Redo);
        Assert.Equal(90, (await viewer.GetPagesAsync(document))[0].Rotation);
    }

    [Fact]
    public async Task SettingsAndEmptyHistoryDoNotRequireCompletionEvents()
    {
        await using var viewer = await PdfViewer.CreateAsync(new PdfViewerOptions { History = true });
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        using var history = await viewer.HistoryAsync(document);
        await history.UndoAsync();
        await history.RedoAsync();
        var changed = Fixture.First(history.ObserveEvents(), e => e as HistoryEvent.SettingsChanged);
        await history.UpdateSettingsAsync(new HistorySettingsPatch { Limit = 5 });
        Assert.Equal(5, (await history.GetSettingsAsync()).Limit);
        Assert.Equal(5, (await changed).Settings.Limit);
        await history.ResetSettingsAsync();
        Assert.Equal(100, (await history.GetSettingsAsync()).Limit);
        await history.ClearAsync();
        await viewer.CloseAsync(document);
        Assert.Equal(PdfErrorCode.InstanceClosed, (await Assert.ThrowsAsync<PdfException>(() => history.CanUndoAsync())).Code);
        Assert.True(await MetadataTests.Ends(history.ObserveEvents()));
    }

    [Fact]
    public async Task AbsentHistoryIsUnsupported()
    {
        await using var viewer = await PdfViewer.CreateAsync();
        var document = await viewer.OpenAsync(Fixture.Data("report"));
        Assert.Equal(PdfErrorCode.Unsupported, (await Assert.ThrowsAsync<PdfException>(() => viewer.HistoryAsync(document))).Code);
    }
}
