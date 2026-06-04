# Stitch Implementation Guide

This guide explains how Codex must translate `stitch_frpnexus_design_system` into the real Avalonia desktop application. Stitch is the visual and structural reference, not the runtime technology.

## Source Priority

When implementing UI from Stitch, use these sources in this order:

1. `screen.png`: visual fidelity, density, hierarchy, spacing, and composition.
2. `code.html`: page structure, Chinese copy, control order, table columns, sample data, component patterns, and token usage.
3. `frpnexus_design_system/DESIGN.md`: colors, typography, spacing, radius, component heights, sidebar width, topbar height.
4. `docs/UI_STYLE_GUIDE.md`: normalized Avalonia resource names and final style decisions.
5. `docs/UI_LAYOUT_GUIDE.md`: normalized page layout decisions.
6. `docs/AVALONIA_UI_CONSTRAINTS.md`: implementation boundaries and prohibited outcomes.

If Stitch conflicts with current project constraints, follow `docs/CURRENT_PHASE.md`, `docs/AVALONIA_UI_CONSTRAINTS.md`, `docs/UI_STYLE_GUIDE.md`, and `docs/UI_LAYOUT_GUIDE.md`.

## Folder To Page Mapping

| Stitch folder | Meaning | Avalonia target |
|---|---|---|
| `frpnexus_1` | 仪表盘 | Dashboard page |
| `frpnexus_2` | 节点管理 | Nodes page |
| `frpnexus_3` | 隧道管理 | Tunnels page |
| `frpnexus_4` | 配置 | Configurations page |
| `frpnexus_5` | 日志 | Logs page |
| `frpnexus_6` | 设置 | Settings page |
| none yet | 运行 | Runtime page derived from the same visual system |

The product navigation must include: 仪表盘, 节点, 隧道, 配置, 运行, 日志, 设置.

## Translation Rules

Translate Stitch into Avalonia concepts deliberately.

| Stitch expression | Avalonia implementation |
|---|---|
| HTML page shell | `Window`, shell `Grid`, `UserControl` pages |
| `nav` SideNav | reusable shell navigation control or styles |
| `header` TopAppBar | shell top bar control or row in shell view |
| Tailwind grid/flex | Avalonia `Grid`, `DockPanel`, `StackPanel`, `WrapPanel` when appropriate |
| Tailwind spacing classes | shared spacing resources and `Margin` / `Padding` values |
| CSS colors | semantic brushes in `ResourceDictionary` |
| CSS typography | shared text styles and font resources |
| CSS radius/border | shared corner radius and border resources |
| HTML table | Avalonia `DataGrid` or reusable list/table control |
| Repeated cards/rows | `ItemsControl`, `DataTemplate`, reusable controls |
| Form inputs | Avalonia `TextBox`, `ComboBox`, `CheckBox`, `ToggleSwitch` or equivalent |
| JavaScript UI state | ViewModel state, binding, and `CommunityToolkit.Mvvm` commands |
| Material Symbols | Avalonia icon library, vector icon resources, or Fluent-style icon controls |

Do not copy Tailwind class names into Avalonia resource names unless the name describes a real semantic token.

## Prohibited Implementation

Never implement Stitch by using:

- HTML rendering.
- CSS rendering.
- Tailwind runtime.
- WebView.
- Embedded browser controls.
- JavaScript-driven UI behavior.
- Screenshot-as-UI shortcuts.

All UI must be implemented as Avalonia XAML, styles, resource dictionaries, views, controls, view models, and bindings.

## Avalonia Structure Rules

When translating Stitch:

- Put shared colors, spacing, typography, radius, borders, and component sizes in shared style resources.
- Put reusable controls under `Controls`, or under feature folders when feature-specific.
- Put converters under `Converters`, or under feature folders when feature-specific.
- Put behaviors and attached properties under dedicated UI infrastructure folders.
- Keep page views and page view models in matching feature/module structures.
- Keep command logic in view models using `CommunityToolkit.Mvvm`.
- Keep services behind interfaces and resolve them through DI.
- Do not place reusable UI infrastructure in the desktop project root.

## Visual Interpretation Rules

Use `screen.png` for:

- Overall composition.
- Density and visual weight.
- Page hierarchy.
- Navigation placement.
- Panel shape.
- Toolbar rhythm.
- Table/list rhythm.
- Status badge treatment.
- Technical panel treatment.

Use `code.html` for:

- Chinese labels and section names.
- Control order.
- Sample data shape.
- Table column names.
- State examples.
- Component dimensions when they are explicit.
- Layout hints such as split panels, fixed widths, and row heights.

Use `DESIGN.md` for:

- Color tokens.
- Typography scale.
- Spacing scale.
- Radius and shape values.
- Shell dimensions.
- Component density.

## SideNav Color Decision

Older Stitch design prose mentions a persistent dark sidebar. Current implementation guidance overrides that for Phase 1:

- Default SideNav must be light and Fluent NavigationView-like.
- `#111827` must not be used as the default main navigation background.
- Dark surfaces are reserved for log panels, terminal panels, TOML previews, and code panels.
- Existing dark sidebar tokens or resources should be treated as legacy or optional theme-experiment material until a future explicit theme task.

## Implementation Checklist

Before implementing a page:

- Read `docs/CURRENT_PHASE.md`.
- Read `docs/PROJECT_TODO.md`.
- Read `docs/AVALONIA_UI_CONSTRAINTS.md`.
- Read `docs/UI_STYLE_GUIDE.md`.
- Read `docs/UI_LAYOUT_GUIDE.md`.
- Read this guide.
- Open the mapped Stitch `screen.png` and `code.html`.
- Identify reusable controls, styles, resources, and view model state before editing XAML.
- Confirm the work stays within the current phase.

## Post-Implementation Visual Checklist

After implementing a page or shell section:

- The page uses Avalonia, not HTML/CSS/WebView.
- Shell metrics match `212px` SideNav, `52px` TopBar, and `24px` page margin.
- SideNav is light by default.
- Page content fits between `1100 x 720` and `1280 x 800`.
- Text is Chinese-first and does not overflow buttons, tabs, nav items, table headers, or badges.
- Tables use compact row height and horizontal separators.
- Logs and TOML previews use dark technical panels.
- Repeated UI patterns are extracted when reused.
- View models use `CommunityToolkit.Mvvm`.
- Any completed Todo item is reported to the user using the format in `docs/PROJECT_TODO.md`.
