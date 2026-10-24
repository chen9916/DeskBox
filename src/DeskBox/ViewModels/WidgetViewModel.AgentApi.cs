namespace DeskBox.ViewModels;

public partial class WidgetViewModel
{
    /// <summary>Agent API: current stack projection (manual + automatic).</summary>
    internal IEnumerable<WidgetStackItem> EnumerateStacksForAgent()
    {
        if (!UsesStackProjection)
        {
            return [];
        }

        RebuildStackDisplayItems();
        return _stackItems.Values.ToList();
    }

    /// <summary>
    /// Agent API: move a stack to a zero-based index among current top-level
    /// display units (stacks and loose items share that order).
    /// </summary>
    internal bool TryReorderStackForAgent(string stackKey, int orderIndex)
    {
        if (!UsesStackProjection || string.IsNullOrWhiteSpace(stackKey))
        {
            return false;
        }

        List<string> order = GetCurrentDisplayUnitOrder();
        if (!order.Contains(stackKey, StringComparer.Ordinal))
        {
            return false;
        }

        List<string> reordered = order
            .Where(key => !string.Equals(key, stackKey, StringComparison.Ordinal))
            .ToList();
        int clamped = Math.Clamp(orderIndex, 0, reordered.Count);
        reordered.Insert(clamped, stackKey);
        if (order.SequenceEqual(reordered, StringComparer.Ordinal))
        {
            return true;
        }

        _stackOrder = reordered;
        PersistStackDisplayOrder();
        return true;
    }
}
