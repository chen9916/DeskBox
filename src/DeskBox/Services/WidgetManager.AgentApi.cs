using DeskBox.Models;
using DeskBox.Platform;
using DeskBox.Services.AgentApi;
using DeskBox.ViewModels;
using DeskBox.Views;
using Windows.Graphics;

namespace DeskBox.Services;

public sealed partial class WidgetManager
{
    internal IReadOnlyList<AgentApiWidgetDto> ListFileWidgetsForAgent()
    {
        return _settingsService.Settings.Widgets
            .Where(widget =>
                widget.WidgetKind == WidgetKind.File &&
                !IsDeleted(widget.Id))
            .Select(ToAgentWidgetDto)
            .ToList();
    }

    internal async Task<AgentApiWidgetDto> CreateFileWidgetForAgentAsync(
        AgentApiCreateWidgetRequest request)
    {
        return await UiDispatch.RunAsync(async () =>
        {
            HashSet<string> before = _settingsService.Settings.Widgets
                .Select(widget => widget.Id)
                .ToHashSet(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(request.FolderPath))
            {
                await CreateFolderWidgetAsync(request.FolderPath.Trim());
            }
            else
            {
                await CreateManagedWidgetAsync(
                    string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim());
            }

            WidgetConfig created = _settingsService.Settings.Widgets.FirstOrDefault(widget =>
                    !before.Contains(widget.Id) &&
                    widget.WidgetKind == WidgetKind.File)
                ?? throw new AgentApiException(
                    500,
                    "File widget was created but could not be located in settings.");

            if (request.Width is not null || request.Height is not null)
            {
                if (request.Width is double w)
                {
                    created.Width = Math.Clamp(w, SettingsService.MinWidgetWidth, 1200);
                }

                if (request.Height is double h)
                {
                    created.Height = Math.Clamp(h, SettingsService.MinWidgetHeight, 1200);
                }

                _settingsService.UpdateWidget(created, notifySubscribers: false);
            }

            if (request.X is not null ||
                request.Y is not null ||
                request.Width is not null ||
                request.Height is not null)
            {
                await ApplyAgentBoundsIfHostReadyAsync(created.Id, request);
            }

            return ToAgentWidgetDto(created);
        });
    }

    internal async Task<AgentApiWidgetDto> PatchFileWidgetForAgentAsync(
        string widgetId,
        AgentApiPatchWidgetRequest request)
    {
        return await UiDispatch.RunAsync(async () =>
        {
            WidgetConfig config = RequireFileWidgetConfig(widgetId);

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                config.Name = request.Name.Trim();
                config.IsDefaultTitle = false;
                if (_fileWidgets.TryGetValue(widgetId, out FileWidgetSession? session))
                {
                    session.ViewModel.Name = config.Name;
                }

                _settingsService.UpdateWidget(config, notifySubscribers: false);
            }

            if (request.X is not null ||
                request.Y is not null ||
                request.Width is not null ||
                request.Height is not null)
            {
                await ApplyAgentBoundsIfHostReadyAsync(widgetId, new AgentApiCreateWidgetRequest
                {
                    X = request.X,
                    Y = request.Y,
                    Width = request.Width,
                    Height = request.Height
                });
            }
            else
            {
                await _settingsService.SaveAsync();
            }

            return ToAgentWidgetDto(RequireFileWidgetConfig(widgetId));
        });
    }

    internal async Task DeleteFileWidgetForAgentAsync(string widgetId)
    {
        await UiDispatch.RunAsync(async () =>
        {
            _ = RequireFileWidgetConfig(widgetId);
            await RemoveWidgetAsync(widgetId, WidgetRemovalAction.RemoveWidgetOnly);
        });
    }

    internal async Task<IReadOnlyList<AgentApiStackDto>> ListStacksForAgentAsync(string widgetId)
    {
        return await UiDispatch.RunAsync(() =>
        {
            WidgetViewModel viewModel = RequireFileWidgetSession(widgetId).ViewModel;
            EnsureStackProjectionReady(viewModel);
            return Task.FromResult<IReadOnlyList<AgentApiStackDto>>(
                viewModel.EnumerateStacksForAgent()
                    .Select(ToAgentStackDto)
                    .ToList());
        });
    }

    internal async Task<AgentApiStackDto> CreateStackForAgentAsync(
        string widgetId,
        AgentApiCreateStackRequest request)
    {
        return await UiDispatch.RunAsync(() =>
        {
            if (request.MemberPaths is null || request.MemberPaths.Count < 2)
            {
                throw new AgentApiException(
                    400,
                    "Creating a stack requires at least two memberPaths.");
            }

            WidgetViewModel viewModel = RequireFileWidgetSession(widgetId).ViewModel;
            EnsureStackProjectionReady(viewModel);

            HashSet<string> beforeKeys = viewModel.EnumerateStacksForAgent()
                .Select(stack => stack.StackKey)
                .ToHashSet(StringComparer.Ordinal);
            List<WidgetItem> members = ResolveStackMembers(viewModel, request.MemberPaths);
            if (!viewModel.CreateManualStack(members))
            {
                throw new AgentApiException(
                    400,
                    "Unable to create a manual stack from the provided member paths.");
            }

            WidgetStackItem created = viewModel.EnumerateStacksForAgent()
                .FirstOrDefault(stack =>
                    stack.IsManual &&
                    !beforeKeys.Contains(stack.StackKey))
                ?? throw new AgentApiException(500, "Manual stack was created but not found.");

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                viewModel.SetStackNameOverride(created.StackKey, request.Name.Trim());
            }

            return Task.FromResult(ToAgentStackDto(
                viewModel.EnumerateStacksForAgent()
                    .First(stack => string.Equals(
                        stack.StackKey,
                        created.StackKey,
                        StringComparison.Ordinal))));
        });
    }

    internal async Task<AgentApiStackDto> PatchStackForAgentAsync(
        string widgetId,
        string stackKey,
        AgentApiPatchStackRequest request)
    {
        return await UiDispatch.RunAsync(() =>
        {
            WidgetViewModel viewModel = RequireFileWidgetSession(widgetId).ViewModel;
            EnsureStackProjectionReady(viewModel);
            WidgetStackItem stack = RequireStack(viewModel, stackKey);

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                viewModel.SetStackNameOverride(stack.StackKey, request.Name.Trim());
            }

            if (request.Expanded is bool expanded)
            {
                viewModel.SetStackExpanded(stack, expanded);
            }

            if (request.OrderIndex is int orderIndex)
            {
                if (!viewModel.TryReorderStackForAgent(stack.StackKey, orderIndex))
                {
                    throw new AgentApiException(
                        400,
                        "Unable to reorder the stack to the requested orderIndex.");
                }
            }

            return Task.FromResult(ToAgentStackDto(RequireStack(viewModel, stackKey)));
        });
    }

    internal async Task DeleteStackForAgentAsync(string widgetId, string stackKey)
    {
        await UiDispatch.RunAsync(() =>
        {
            WidgetViewModel viewModel = RequireFileWidgetSession(widgetId).ViewModel;
            EnsureStackProjectionReady(viewModel);
            WidgetStackItem stack = RequireStack(viewModel, stackKey);
            if (!viewModel.DissolveStack(stack))
            {
                throw new AgentApiException(400, "Unable to delete/dissolve the stack.");
            }

            return Task.CompletedTask;
        });
    }

    private async Task ApplyAgentBoundsIfHostReadyAsync(
        string widgetId,
        AgentApiCreateWidgetRequest request)
    {
        WidgetConfig config = RequireFileWidgetConfig(widgetId);
        if (!_fileWidgets.TryGetValue(widgetId, out FileWidgetSession? session))
        {
            // Host not loaded yet — persist requested values into config and save.
            if (request.X is double x)
            {
                config.X = x;
            }

            if (request.Y is double y)
            {
                config.Y = y;
            }

            if (request.Width is double width)
            {
                config.Width = Math.Clamp(width, SettingsService.MinWidgetWidth, 1200);
            }

            if (request.Height is double height)
            {
                config.Height = Math.Clamp(height, SettingsService.MinWidgetHeight, 1200);
            }

            config.LastUserPlacementUtc = DateTimeOffset.UtcNow;
            _settingsService.UpdateWidget(config, notifySubscribers: false);
            await _settingsService.SaveAsync();
            return;
        }

        RectInt32 current = session.Host.CoordinatedMoveBounds;
        double scale = Math.Max(
            WidgetPositioningService.GetDpiScale(
                new RectInt32(current.X, current.Y, Math.Max(1, current.Width), Math.Max(1, current.Height))),
            0.01);

        int targetX = request.X is double rx ? (int)Math.Round(rx) : current.X;
        int targetY = request.Y is double ry ? (int)Math.Round(ry) : current.Y;
        int targetWidth = request.Width is double rw
            ? WidgetPositioningService.ToPhysicalPixels(rw, scale)
            : current.Width;
        int targetHeight = request.Height is double rh
            ? WidgetPositioningService.ToPhysicalPixels(rh, scale)
            : current.Height;

        session.Host.ApplyAgentRequestedBounds(
            new AgentWidgetPhysicalBounds(targetX, targetY, targetWidth, targetHeight));
        CaptureCurrentTopologyLayout(session.ViewModel.Config);
    }

    private WidgetConfig RequireFileWidgetConfig(string widgetId)
    {
        WidgetConfig? config = FindConfig(widgetId);
        if (config is null || IsDeleted(widgetId))
        {
            throw new AgentApiException(404, $"Widget '{widgetId}' was not found.");
        }

        if (config.WidgetKind != WidgetKind.File)
        {
            throw new AgentApiException(
                400,
                $"Widget '{widgetId}' is kind '{config.WidgetKind}', not File.");
        }

        return config;
    }

    private FileWidgetSession RequireFileWidgetSession(string widgetId)
    {
        _ = RequireFileWidgetConfig(widgetId);
        if (!_fileWidgets.TryGetValue(widgetId, out FileWidgetSession? session))
        {
            throw new AgentApiException(
                409,
                $"File widget '{widgetId}' is not currently loaded. Reveal it in DeskBox and retry.");
        }

        return session;
    }

    private static void EnsureStackProjectionReady(WidgetViewModel viewModel)
    {
        if (!viewModel.FileStacksEnabled)
        {
            viewModel.SetFileStacksEnabledOverride(true);
        }
    }

    private static List<WidgetItem> ResolveStackMembers(
        WidgetViewModel viewModel,
        IReadOnlyList<string> memberPaths)
    {
        var members = new List<WidgetItem>();
        foreach (string rawPath in memberPaths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(rawPath.Trim());
            WidgetItem? item = viewModel.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Path, fullPath, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                throw new AgentApiException(
                    404,
                    $"Item '{fullPath}' is not present in the file widget.");
            }

            members.Add(item);
        }

        return members;
    }

    private static WidgetStackItem RequireStack(WidgetViewModel viewModel, string stackKey)
    {
        WidgetStackItem? stack = viewModel.EnumerateStacksForAgent()
            .FirstOrDefault(candidate =>
                string.Equals(candidate.StackKey, stackKey, StringComparison.Ordinal));
        if (stack is null)
        {
            throw new AgentApiException(404, $"Stack '{stackKey}' was not found.");
        }

        return stack;
    }

    private static AgentApiWidgetDto ToAgentWidgetDto(WidgetConfig config) =>
        new()
        {
            Id = config.Id,
            Name = config.Name,
            Kind = config.WidgetKind.ToString(),
            X = config.X,
            Y = config.Y,
            Width = config.Width,
            Height = config.Height,
            FolderPath = config.MappedFolderPath,
            FollowsDefaultStoragePath = config.FollowsDefaultStoragePath,
            IsVisible = config.IsVisible,
            IsCollapsed = config.IsCollapsed,
            IsPositionLocked = config.IsPositionLocked,
            IsSizeLocked = config.IsSizeLocked
        };

    private static AgentApiStackDto ToAgentStackDto(WidgetStackItem stack) =>
        new()
        {
            StackKey = stack.StackKey,
            Name = stack.Name,
            IsManual = stack.IsManual,
            IsExpanded = stack.IsExpanded,
            MemberPaths = stack.Members.Select(member => member.Path).ToList()
        };
}
