// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>
/// The wrapper of <see cref="IReceivedItemsHelper"/> to implement a <see cref="IReadOnlyCollection{T}"/> of item names.
/// </summary>
/// <param name="helper">The received items.</param>
[CLSCompliant(false)]
public sealed class ItemNameEnumerable(IReceivedItemsHelper helper) : IReadOnlyCollection<string>
{
    /// <inheritdoc />
    public int Count => helper.AllItemsReceived.Count;

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => helper.AllItemsReceived.Select(x => x.ItemName).GetEnumerator();
}
