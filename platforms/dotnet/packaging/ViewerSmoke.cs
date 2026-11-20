// The EmbedPDF.Runtime consumer check (package.py smoke-runtime). It builds
// against the EmbedPDF and EmbedPDF.Runtime packages from a local feed, with
// no native library path in the environment.
// Exit 0: the host loaded and read the document. Exit 3: the resolver found
// no host for this runtime and said so with its typed exception.
using EmbedPDF;

try
{
    var pages = await NativeRuntime.ReadPageCountAsync(await File.ReadAllBytesAsync(args[0]));
    if (pages <= 0) throw new InvalidOperationException("PDF has no pages");
    Console.WriteLine($"PASS: rid={NativeRuntime.RuntimeIdentifier} pages={pages}");
    return 0;
}
catch (NativeRuntimeNotFoundException error)
{
    Console.WriteLine($"{error.GetType().Name}: rid={error.RuntimeIdentifier}: {error.Message}");
    return 3;
}
