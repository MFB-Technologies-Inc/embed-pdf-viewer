namespace EmbedPDF;

/// <summary>
/// An <see cref="IStampLibraryStore"/> over one PDF file per library in a
/// directory. Writes go to a temporary file in the same directory first and
/// replace the library's file in one move, so a crash mid-write leaves the
/// previous version.
/// </summary>
/// <remarks>
/// A library id becomes a file name with every character outside
/// <c>A-Z</c>, <c>a-z</c>, <c>0-9</c>, <c>-</c> and <c>_</c> percent-escaped,
/// so no id names a path outside <see cref="Directory"/> and every id reads
/// back as it was written. Members throw <see cref="IOException"/> and
/// <see cref="UnauthorizedAccessException"/> as the file system does.
/// </remarks>
public sealed class FileStampLibraryStore : IStampLibraryStore
{
    const string Extension = ".pdf";
    const string Partial = ".partial";

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
    public IReadOnlyList<StoredStampLibrary> List() =>
        [.. new DirectoryInfo(Directory).EnumerateFiles("*" + Extension)
            .Where(file => file.Name.EndsWith(Extension, StringComparison.Ordinal))
            .OrderBy(file => file.Name, StringComparer.Ordinal)
            .Select(file => new StoredStampLibrary(Unescape(file.Name[..^Extension.Length]), File.ReadAllBytes(file.FullName)))];

    /// <inheritdoc/>
    public void Put(string id, ReadOnlyMemory<byte> bytes)
    {
        var target = PathOf(id);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + Partial;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
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
        return Path.Combine(Directory, Escape(id) + Extension);
    }

    static string Escape(string id)
    {
        var text = new System.Text.StringBuilder(id.Length);
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(id))
        {
            if (b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'-' or (byte)'_') text.Append((char)b);
            else text.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return text.ToString();
    }

    static string Unescape(string name) => Uri.UnescapeDataString(name);
}
