using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EmbedPDF;

/// <summary>
/// An <see cref="IStampLibraryStore"/> over one file per library in a
/// directory. Writes go to a temporary file in the same directory first and
/// replace the library's file in one move, so a crash mid-write leaves the
/// previous version.
/// </summary>
/// <remarks>
/// A library's file is named by the lowercase hex SHA-256 of its id, with
/// the <c>.library</c> extension, so ids that differ only in case, Windows
/// device names such as <c>CON</c>, ids with path characters and long ids
/// all get distinct, portable names inside <see cref="Directory"/>. The file
/// holds the id and then the library's PDF bytes, which is how
/// <see cref="List"/> reads ids back. Members throw <see cref="IOException"/>
/// and <see cref="UnauthorizedAccessException"/> as the file system does; a
/// file that is not a library this store wrote is skipped.
/// </remarks>
public sealed class FileStampLibraryStore : IStampLibraryStore
{
    const string Extension = ".library";
    const string Partial = ".partial";
    static readonly byte[] Magic = "EPDFSTAMPLIB1\n"u8.ToArray();

    /// <summary>The default directory: <c>EmbedPDF/stamps</c> under <see cref="Environment.SpecialFolder.LocalApplicationData"/>.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "EmbedPDF", "stamps");

    /// <summary>The directory holding the libraries.</summary>
    public string Directory { get; }

    /// <summary>A store in <paramref name="directory"/>, or in <see cref="DefaultDirectory"/> when it is <see langword="null"/>. Creates the directory.</summary>
    public FileStampLibraryStore(string? directory = null)
    {
        Directory = Path.GetFullPath(directory ?? DefaultDirectory);
        System.IO.Directory.CreateDirectory(Directory);
    }

    /// <inheritdoc/>
    public IReadOnlyList<StoredStampLibrary> List()
    {
        var libraries = new List<StoredStampLibrary>();
        foreach (var file in new DirectoryInfo(Directory).EnumerateFiles("*" + Extension)
            .Where(file => file.Name.EndsWith(Extension, StringComparison.Ordinal))
            .OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            if (Read(File.ReadAllBytes(file.FullName)) is { } library && file.Name == NameOf(library.Id)) libraries.Add(library);
        }
        return [.. libraries.OrderBy(l => l.Id, StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public void Put(string id, ReadOnlyMemory<byte> bytes)
    {
        var target = PathOf(id);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + Partial;
        var idBytes = Encoding.UTF8.GetBytes(id);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, idBytes.Length);
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(Magic);
                stream.Write(length);
                stream.Write(idBytes);
                stream.Write(bytes.Span);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <inheritdoc/>
    public void Delete(string id)
    {
        var path = PathOf(id);
        if (File.Exists(path)) File.Delete(path);
    }

    string PathOf(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Path.Combine(Directory, NameOf(id));
    }

    static string NameOf(string id) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + Extension;

    static StoredStampLibrary? Read(byte[] file)
    {
        var header = Magic.Length + 4;
        if (file.Length < header || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return null;
        var length = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(Magic.Length, 4));
        if (length < 0 || length > file.Length - header) return null;
        return new StoredStampLibrary(Encoding.UTF8.GetString(file, header, length), file.AsMemory(header + length));
    }
}
