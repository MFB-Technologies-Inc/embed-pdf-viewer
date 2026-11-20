using uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// A registered tool, handler, command or locale. <see cref="Unregister"/>
/// undoes it, and so does <see cref="Dispose"/>. Dropping this without
/// calling either leaves the registration in place: there is no finalizer.
/// </summary>
public sealed class Registration : IDisposable
{
    readonly RegistrationHandle handle;

    internal Registration(RegistrationHandle handle) => this.handle = handle;

    /// <summary>Undoes the registration. Idempotent; the kernel removes it after this returns.</summary>
    public void Unregister()
    {
        try
        {
            handle.Unregister();
        }
        catch (ObjectDisposedException)
        {
            // Already released: nothing left to undo.
        }
    }

    /// <summary><see cref="Unregister"/>.</summary>
    public void Dispose() => Unregister();
}
