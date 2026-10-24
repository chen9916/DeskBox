namespace DeskBox.Services.AgentApi;

/// <summary>HTTP status + body returned by the agent router.</summary>
internal sealed record AgentApiResponse(int StatusCode, string JsonBody, string ContentType = "application/json; charset=utf-8");

internal sealed class AgentApiErrorBody
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

internal sealed class AgentApiHealthBody
{
    public string Status { get; set; } = "ok";
    public string Service { get; set; } = "deskbox-agent";
    public int Port { get; set; }
}

internal sealed class AgentApiWidgetListBody
{
    public List<AgentApiWidgetDto> Widgets { get; set; } = [];
}

internal sealed class AgentApiWidgetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "File";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string? FolderPath { get; set; }
    public bool FollowsDefaultStoragePath { get; set; }
    public bool IsVisible { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsPositionLocked { get; set; }
    public bool IsSizeLocked { get; set; }
}

internal sealed class AgentApiCreateWidgetRequest
{
    /// <summary>Optional display name for a managed file widget.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// When set, create a folder-mapped widget at this path instead of a managed widget.
    /// </summary>
    public string? FolderPath { get; set; }

    /// <summary>Optional initial physical X (screen pixels). Applied after create when the host is loaded.</summary>
    public double? X { get; set; }

    /// <summary>Optional initial physical Y (screen pixels).</summary>
    public double? Y { get; set; }

    /// <summary>Optional logical width.</summary>
    public double? Width { get; set; }

    /// <summary>Optional logical height.</summary>
    public double? Height { get; set; }
}

internal sealed class AgentApiPatchWidgetRequest
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? Name { get; set; }
}

internal sealed class AgentApiStackListBody
{
    public string WidgetId { get; set; } = string.Empty;
    public List<AgentApiStackDto> Stacks { get; set; } = [];
}

internal sealed class AgentApiStackDto
{
    public string StackKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsManual { get; set; }
    public bool IsExpanded { get; set; }
    public List<string> MemberPaths { get; set; } = [];
}

internal sealed class AgentApiCreateStackRequest
{
    /// <summary>Absolute file paths that should become stack members (at least 2).</summary>
    public List<string> MemberPaths { get; set; } = [];

    /// <summary>Optional display name override for the new manual stack.</summary>
    public string? Name { get; set; }
}

internal sealed class AgentApiPatchStackRequest
{
    /// <summary>Optional rename.</summary>
    public string? Name { get; set; }

    /// <summary>Optional expand/collapse.</summary>
    public bool? Expanded { get; set; }

    /// <summary>
    /// Zero-based display order among current stacks/loose units. Moving a stack
    /// reorders it inside the owning file widget (stacks have no desktop bounds).
    /// </summary>
    public int? OrderIndex { get; set; }
}
