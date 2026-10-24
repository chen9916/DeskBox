using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DeskBox.Views;

public abstract partial class WidgetWindowBase
{
    /// <summary>
    /// Agent-API entry point: move/resize an expanded widget and persist layout
    /// through the same anchor + config path used by interactive drag/resize.
    /// </summary>
    internal AgentWidgetPhysicalBounds ApplyAgentRequestedBounds(
        AgentWidgetPhysicalBounds requested)
    {
        if (IsCompactBoundsStateActive || Config.IsCollapsed)
        {
            throw new InvalidOperationException(
                "Agent move/resize requires an expanded (non-capsule) widget surface.");
        }

        if (Config.IsPositionLocked &&
            (requested.X != GetActualWindowBounds().X ||
             requested.Y != GetActualWindowBounds().Y))
        {
            throw new InvalidOperationException(
                "Widget position is locked.");
        }

        if (Config.IsSizeLocked &&
            (requested.Width != GetActualWindowBounds().Width ||
             requested.Height != GetActualWindowBounds().Height))
        {
            throw new InvalidOperationException(
                "Widget size is locked.");
        }

        int requestedWidth = Math.Max(MinWidth, requested.Width);
        int requestedHeight = Math.Max(MinHeight, requested.Height);
        var requestedRect = new RectInt32(
            requested.X,
            requested.Y,
            requestedWidth,
            requestedHeight);
        RectInt32 workArea = DisplayArea.GetFromRect(
            requestedRect,
            DisplayAreaFallback.Nearest).WorkArea;
        int width = Math.Min(requestedWidth, Math.Max(1, workArea.Width));
        int height = Math.Min(requestedHeight, Math.Max(1, workArea.Height));
        int x = Math.Clamp(
            requested.X,
            workArea.X,
            workArea.X + Math.Max(0, workArea.Width - width));
        int y = Math.Clamp(
            requested.Y,
            workArea.Y,
            workArea.Y + Math.Max(0, workArea.Height - height));
        var safeBounds = new RectInt32(x, y, width, height);

        MoveWindowWithoutPersisting(safeBounds);
        RectInt32 actual = GetActualWindowBounds();
        CapturePositionAnchor(
            actual.X,
            actual.Y,
            actual.Width,
            actual.Height,
            preserveCurrentEdge: false);
        UpdateConfigBoundsFromPhysical(
            actual.X,
            actual.Y,
            actual.Width,
            actual.Height,
            persist: true);

        return new AgentWidgetPhysicalBounds(
            actual.X,
            actual.Y,
            actual.Width,
            actual.Height);
    }
}

internal readonly record struct AgentWidgetPhysicalBounds(
    int X,
    int Y,
    int Width,
    int Height);
