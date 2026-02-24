using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace mPrismaMapsWPF.Helpers;

/// <summary>
/// An ObservableCollection whose bulk-mutation methods fire a single Reset notification
/// instead of one notification per item, eliminating the O(N) WPF layout/binding cycles
/// that occur when adding or replacing large numbers of items.
/// </summary>
public class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Appends all <paramref name="items"/> to the collection and raises a single Reset
    /// notification.  Prefer this over repeated <c>Add</c> calls when adding many items.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items)
            Items.Add(item);   // Items is the backing List<T>; bypasses per-item notifications
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Replaces all existing items with <paramref name="items"/> and raises a single Reset
    /// notification.  Equivalent to <c>Clear</c> + <c>AddRange</c> but with one notification
    /// instead of two.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
