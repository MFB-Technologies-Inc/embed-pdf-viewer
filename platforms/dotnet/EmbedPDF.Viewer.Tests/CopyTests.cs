using System.Net;
using Xunit;

namespace EmbedPDF.Viewer.Tests;

/// <summary>The byte paths copy a document as few times as they can.</summary>
public sealed class CopyTests
{
    [Fact]
    public void AWholeArrayIsPassedWithoutACopy()
    {
        var bytes = new byte[] { 1, 2, 3 };
        Assert.Same(bytes, Bytes.Array(bytes));
        var slice = Bytes.Array(bytes.AsMemory(1));
        Assert.NotSame(bytes, slice);
        Assert.Equal(new byte[] { 2, 3 }, slice);
    }

    [Fact]
    public async Task ASeekableStreamIsReadFromItsPositionIntoOneBuffer()
    {
        var data = Enumerable.Range(0, 100_000).Select(i => (byte)i).ToArray();
        using var stream = new MemoryStream(data) { Position = 10 };
        var read = await Bytes.ReadToEndAsync(stream, default);
        Assert.Equal(data.Skip(10), read);
    }

    sealed class TrickleStream(byte[] data) : Stream
    {
        int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(Math.Min(count, 7), data.Length - position);
            Array.Copy(data, position, buffer, offset, n);
            position += n;
            return n;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task ANonSeekableStreamIsReadWhole()
    {
        var data = Fixture.Data("report");
        Assert.Equal(data, await Bytes.ReadToEndAsync(new TrickleStream(data), default));
        await using var viewer = await PdfViewer.CreateAsync();
        Assert.Equal(14, (await viewer.OpenAsync(new TrickleStream(data))).PageCount);
    }

    [Fact]
    public async Task TheFetcherReadsBodiesWithAndWithoutALength()
    {
        var body = Fixture.Data("report");
        await using var server = Loopback.Start(async context =>
        {
            if (context.Request.Url!.AbsolutePath.EndsWith("chunked.pdf", StringComparison.Ordinal))
            {
                context.Response.SendChunked = true;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
            else
            {
                await Loopback.Reply(context, 200, body);
            }
        });
        var fetcher = new HttpClientDocumentFetcher();
        Assert.Equal(body, (await fetcher.FetchAsync(server.Url("sized.pdf").ToString(), default)).Content);
        Assert.Equal(body, (await fetcher.FetchAsync(server.Url("chunked.pdf").ToString(), default)).Content);
    }

    [Fact]
    public void TheSharedClientRecyclesPooledConnections() =>
        Assert.Equal(TimeSpan.FromMinutes(2), HttpClientDocumentFetcher.SharedHandler.PooledConnectionLifetime);
}
