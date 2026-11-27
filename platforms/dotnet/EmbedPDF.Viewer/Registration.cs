using uniffi.epdf_ffi;

namespace EmbedPDF;

/// <summary>
/// A registered tool, handler, command or locale. <see cref="Unregister"/>
/// undoes it and keeps the native handle; <see cref="Dispose"/> undoes it and
/// releases the handle. Dropping this without calling either leaves the
/// registration in place.
/// </summary>
public sealed class Registration : IDisposable
{
    internal Registration(RegistrationHandle handle) => Handle = handle;

    internal RegistrationHandle Handle { get; }

    /// <summary>Undoes the registration. Idempotent; the kernel removes it after this returns.</summary>
    public void Unregister()
    {
        try
        {
            Handle.Unregister();
        }
        catch (ObjectDisposedException)
        {
            // Already released: nothing left to undo.
        }
    }

    /// <summary><see cref="Unregister"/>, then releases the native handle. Idempotent.</summary>
    public void Dispose()
    {
        Unregister();
        Handle.Dispose();
    }
}
