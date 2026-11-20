using Xunit;

namespace EmbedPDF.Viewer.Tests;

public sealed class FileStampLibraryStoreTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "embedpdf-store-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void PutListAndDeleteRoundTrip()
    {
        var store = new FileStampLibraryStore(Path.Combine(root, "store"));
        store.Put("b", new byte[] { 2 });
        store.Put("a", new byte[] { 1 });
        store.Put("a", new byte[] { 1, 1 });
        var listed = store.List();
        Assert.Equal(["a", "b"], listed.Select(l => l.Id));
        Assert.Equal(new byte[] { 1, 1 }, listed[0].Bytes.ToArray());
        store.Delete("a");
        store.Delete("a");
        Assert.Equal(["b"], store.List().Select(l => l.Id));
        Assert.Single(Directory.GetFiles(store.Directory));
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("c:d")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a\\b")]
    [InlineData("名前 100%")]
    public void AHostileIdStaysInTheDirectory(string id)
    {
        var directory = Path.Combine(root, "store");
        var store = new FileStampLibraryStore(directory);
        store.Put(id, new byte[] { 7 });
        Assert.Equal([id], store.List().Select(l => l.Id));
        Assert.Equal([directory], Directory.GetFileSystemEntries(root));
        var file = Assert.Single(Directory.GetFiles(directory));
        Assert.Equal(directory, Path.GetDirectoryName(file));
        store.Delete(id);
        Assert.Empty(store.List());
    }

    [Fact]
    public void OnlyLibraryFilesAreListed()
    {
        var store = new FileStampLibraryStore(Path.Combine(root, "store"));
        File.WriteAllBytes(Path.Combine(store.Directory, "notes.txt"), [1]);
        File.WriteAllBytes(Path.Combine(store.Directory, "half.pdf.0123.partial"), [1]);
        store.Put("kept", new byte[] { 3 });
        Assert.Equal(["kept"], store.List().Select(l => l.Id));
    }

    [Fact]
    public void TheDefaultDirectoryIsUnderLocalApplicationData()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "EmbedPDF", "stamps"), FileStampLibraryStore.DefaultDirectory);
    }
}
