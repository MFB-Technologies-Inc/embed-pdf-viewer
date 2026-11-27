using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace EmbedPDF.Models;

/// <summary>
/// The base of every model: an <see cref="INotifyPropertyChanged"/> object
/// with one home <see cref="SynchronizationContext"/>.
/// </summary>
/// <remarks>
/// The home context is the one passed to the model's constructor, else
/// <see cref="SynchronizationContext.Current"/> at construction, else a
/// private context that runs the model's work one item at a time on the
/// thread pool. The model keeps its state there: each method moves to it
/// before it reads or writes, so a view may call one from any thread, and
/// every <see cref="PropertyChanged"/> is raised there. Construct a model on
/// the UI thread, or pass the UI thread's context, and its notifications
/// arrive where the view can apply them.
/// </remarks>
public abstract class ObservableModel : INotifyPropertyChanged
{
    private protected ObservableModel(SynchronizationContext? context) => Home = new ModelContext(context);

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The context this model raises <see cref="PropertyChanged"/> on.</summary>
    public SynchronizationContext Context => Home.Context;

    internal ModelContext Home { get; }

    /// <summary>Writes <paramref name="value"/> and notifies, only when it differs.</summary>
    private protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name!);
        return true;
    }

    /// <summary>Writes a list and notifies, only when its items differ.</summary>
    private protected bool SetList<T>(ref IReadOnlyList<T> field, IReadOnlyList<T> value, [CallerMemberName] string? name = null)
    {
        if (ReferenceEquals(field, value) || field.SequenceEqual(value)) return false;
        field = value;
        Raise(name!);
        return true;
    }

    private protected void Raise(string name)
    {
        Debug.Assert(Home.IsCurrent, $"{GetType().Name}.{name} changed off its home context");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
