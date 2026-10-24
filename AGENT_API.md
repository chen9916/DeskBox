# DeskBox Agent API

Localhost HTTP API so an AI agent can create, move, resize, and delete **file widgets** and **file stacks** without touching the GUI.

The API reuses DeskBox's existing `WidgetManager` / `WidgetViewModel` create-move-resize-delete paths and the same `widget-layout.json` persistence. It does not invent a second layout store.

## Where widget configs and layout state live

| Concern | Location |
| --- | --- |
| Widget inventory (configs, groups, topology layouts, deleted ids) | `data/widget-layout.json` via `WidgetLayoutStore` / `WidgetLayoutSettingsSlice` |
| Global app settings (defaults, feature toggles, appearance) | `data/settings.json` via `SettingsService` / `AppSettings` |
| Runtime facade | `AppSettings.Widgets` → `WidgetLayout.Widgets` (`List<WidgetConfig>`) |
| Per-widget bounds | `WidgetConfig.X/Y` (physical px), `Width/Height` (logical), anchors / monitor binding |
| File stacks (manual membership, names, order, disabled auto-stacks) | `WidgetConfig.Metadata` keys owned by `WidgetFileStackSettings` |
| Live windows | `WidgetManager` (`_fileWidgets` sessions + content hosts) |

Data root: `%LocalAppData%\DeskBox\` (or the development root when `DeskBoxDataPathService` is redirected).

## Enable / port

- Bound to **`127.0.0.1` only** (loopback). Non-loopback clients receive 403.
- Default port: **`17865`**
- Disable: set env `DESKBOX_AGENT_API=0` (also `false` / `off` / `disabled`)
- Override port: `DESKBOX_AGENT_API_PORT=17866`
- Starts after widget restore; stops during app shutdown.

## Endpoints

Base URL: `http://127.0.0.1:17865`

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Liveness |
| `GET` | `/v1/widgets` | List file widgets |
| `POST` | `/v1/widgets` | Create managed or folder-mapped file widget |
| `PATCH` | `/v1/widgets/{id}` | Move / resize / rename |
| `DELETE` | `/v1/widgets/{id}` | Delete widget (keeps managed folder contents) |
| `GET` | `/v1/widgets/{id}/stacks` | List stacks in a loaded file widget |
| `POST` | `/v1/widgets/{id}/stacks` | Create a manual stack from member paths (≥2) |
| `PATCH` | `/v1/widgets/{id}/stacks/{stackKey}` | Rename, expand/collapse, reorder |
| `DELETE` | `/v1/widgets/{id}/stacks/{stackKey}` | Dissolve manual stack (or disable auto stack) |

JSON uses camelCase. Stacks have **no desktop bounds**; `orderIndex` reorders them inside the widget. Widget `x`/`y` are physical screen pixels; `width`/`height` are logical pixels (same units as `WidgetConfig`).

## Example curls

```bash
# Health
curl -s http://127.0.0.1:17865/health

# List file widgets
curl -s http://127.0.0.1:17865/v1/widgets

# Create a managed file widget
curl -s -X POST http://127.0.0.1:17865/v1/widgets \
  -H "Content-Type: application/json" \
  -d '{"name":"Agent Box"}'

# Create a folder-mapped widget, then place/size it
curl -s -X POST http://127.0.0.1:17865/v1/widgets \
  -H "Content-Type: application/json" \
  -d '{"folderPath":"D:\\\\Projects","x":200,"y":120,"width":320,"height":420}'

# Move + resize an existing widget
curl -s -X PATCH http://127.0.0.1:17865/v1/widgets/WIDGET_ID \
  -H "Content-Type: application/json" \
  -d '{"x":400,"y":200,"width":360,"height":480}'

# Delete a widget
curl -s -X DELETE http://127.0.0.1:17865/v1/widgets/WIDGET_ID

# Create a manual stack from two items already in the widget
curl -s -X POST http://127.0.0.1:17865/v1/widgets/WIDGET_ID/stacks \
  -H "Content-Type: application/json" \
  -d '{"memberPaths":["D:\\\\Projects\\\\a.txt","D:\\\\Projects\\\\b.txt"],"name":"Docs"}'

# Reorder / rename / expand a stack
curl -s -X PATCH http://127.0.0.1:17865/v1/widgets/WIDGET_ID/stacks/Manual%3Aabc123 \
  -H "Content-Type: application/json" \
  -d '{"name":"Docs","expanded":true,"orderIndex":0}'

# Dissolve a stack
curl -s -X DELETE http://127.0.0.1:17865/v1/widgets/WIDGET_ID/stacks/Manual%3Aabc123
```

## Notes for agents

1. DeskBox must be **running** — the API is in-process and updates live UI + durable layout.
2. Stack operations require the file widget **host to be loaded** (visible/restored). If you get HTTP 409, reveal the widget first.
3. Creating a stack needs the member files to already appear inside that widget.
4. Position/size locks (`isPositionLocked` / `isSizeLocked`) reject conflicting PATCH fields.
