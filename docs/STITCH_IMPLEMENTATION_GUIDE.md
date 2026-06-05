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

If Stitch conflicts with local UI/UX documentation, follow Stitch. The only explicit UI exceptions are the `1100 x 720` through `1280 x 800` window target and TOML as the default FRP configuration format. `docs/CURRENT_PHASE.md` still overrides all implementation scope decisions.

## Folder To Page Mapping

| Stitch folder | Meaning | Avalonia target |
|---|---|---|
| `frpnexus_1` | 仪表盘 | Dashboard page |
| `frpnexus_2` | 节点管理 | Nodes page |
| `frpnexus_3` | 隧道管理 | Tunnels page |
| `frpnexus_4` | 配置 | Configurations page |
| `frpnexus_5` | 日志 | Logs page |
| `frpnexus_6` | 设置 | Settings page |

The current Stitch-driven main navigation includes: 仪表盘, 节点, 隧道, 配置, 日志, 设置. Runtime management remains a product capability, but do not add a `运行` navigation item or page unless a future Stitch source adds it.

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
| Material Symbols | Material Symbols-style Avalonia icon resources or controls |
| CSS hover/active states | Avalonia pseudoclasses and dedicated control templates when Fluent defaults conflict |

Do not copy Tailwind class names into Avalonia resource names unless the name describes a real semantic token.

When translating controls, preserve Stitch interaction states as first-class requirements. Static composition is not sufficient if hover, pressed, disabled, cursor, border, foreground, or icon states diverge from Stitch.

If Avalonia Fluent defaults make a Stitch control unreadable or visually unstable, use explicit styles or `ControlTemplate` overrides. The verified Dashboard implementation uses this rule for full-width quick action buttons, lightweight text links such as `查看全部`, and compact TopBar icon buttons.

Input controls require the same translation discipline. `TextBox`, password inputs using `PasswordChar`, search fields, and `ComboBox` controls must preserve Stitch hover, focus, disabled, watermark, caret, cursor, and selection states. Do not accept black-background/white-text input states, unreadable `Watermark` text, or selection colors that feel detached from the Stitch light-surface system.

Dropdown controls require popup behavior translation, not only closed-control styling. `ComboBox` popups should close when the user clicks outside, preserve light WinUI 3 / Stitch item states for hover, selected, pressed, and selected-hover combinations, and keep all item text readable. Use dedicated Avalonia templates when Fluent defaults draw dark overlays that Stitch does not show.

Tables require layout translation, not just row styling. When a Stitch table is implemented with `DataGrid` or a custom `ItemsControl + Grid`, the header, row content, checkbox visual column, selected background, hover background, separators, and pagination area must stay aligned and full-width at both target window sizes.

Material Symbols-style icons should be translated into `PathIcon`, shared icon resources, or dedicated converters/controls. Do not use plain punctuation or text characters for key action icons when Stitch shows a real icon.

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

Older Stitch design prose mentions a persistent dark sidebar. Current Stitch screenshots and most page HTML define the default:

- Default SideNav must be light.
- `#111827` must not be used as the default main navigation background.
- Dark surfaces are reserved for log panels, terminal panels, TOML previews, and code panels.
- Existing dark sidebar tokens or resources should be treated as legacy or optional theme-experiment material until a future explicit theme task.

## Configuration Sample Decision

TOML remains the default FRP configuration format. If Stitch sample copy, paths, or log lines mention `config.ini` or INI snippets, translate them to TOML-oriented examples during Avalonia implementation.

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
- Tables keep 表头, rows, checkbox columns, selected backgrounds, separators, and pagination aligned and full-width at `1280 x 800` and `1100 x 720`.
- Custom `ItemsControl + Grid` tables stretch the `ItemsControl`, item container, row button, and row content `Grid`; long cells trim without breaking column alignment.
- Logs and TOML previews use dark technical panels.
- Search inputs, normal `TextBox` controls, password fields using `PasswordChar`, `Watermark` text, caret color, and `SelectionBrush` / selection foreground are manually checked in normal, hover, focus, and disabled states.
- Editable text controls use an I-beam cursor; `ComboBox` controls, popup items, icon buttons, and lightweight links use a hand cursor.
- Search or decorative icons inside inputs do not intercept text-input hit testing or prevent the I-beam cursor from appearing over the editable area.
- `ComboBox` popups close when clicking outside, and outside-click event pass-through is preserved when the surrounding desktop interaction expects it.
- `ComboBox` popup item `hover`, `selected`, `pressed`, and `selected + hover` states remain light-toned and readable; gray-black Fluent overlay states are not accepted.
- Primary, secondary, icon, and lightweight link buttons preserve readable `hover` and `pressed` states.
- Clickable text actions such as `查看全部` use a hand cursor and visible hover feedback.
- TopBar status badges have vertically centered dots and text; TopBar icon buttons keep stable background, border, and icon color.
- Dashboard quick actions fill the panel width and keep centered icon+label content.
- Dashboard log preview keeps colored bracketed log levels, a right-aligned realtime badge, and dark technical-panel contrast.
- Repeated UI patterns are extracted when reused.
- View models use `CommunityToolkit.Mvvm`.
- Any completed Todo item is reported to the user using the format in `docs/PROJECT_TODO.md`.
