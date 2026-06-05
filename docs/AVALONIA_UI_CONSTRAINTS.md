# Avalonia UI Implementation Constraints

## Design Sources

Use these sources when implementing the Avalonia desktop UI:

- Product and phase boundaries: `docs/CURRENT_PHASE.md`, then `docs/PRODUCT.md`.
- UI authority, except for window size and TOML default: `stitch_frpnexus_design_system/frpnexus_*/screen.png`, `stitch_frpnexus_design_system/frpnexus_*/code.html`, and `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md`.
- Implementation guides: `docs/STITCH_IMPLEMENTATION_GUIDE.md`, `docs/UI_STYLE_GUIDE.md`, `docs/UI_LAYOUT_GUIDE.md`, and `docs/UIUX.md`.

If UI/UX visual, layout, navigation, icon, component-density, or page-organization guidance conflicts with Stitch, follow Stitch. The explicit exceptions are:

- Window design and QA remain limited to `1100 x 720` through `1280 x 800`.
- TOML remains the default FRP configuration format. INI examples from Stitch must be translated to TOML-oriented copy or paths during implementation.

## Page Mapping

Map Stitch designs to Avalonia pages as follows:

| Stitch folder | Screen | Avalonia page |
|---|---|---|
| `frpnexus_1` | 仪表盘 | Dashboard / 仪表盘 |
| `frpnexus_2` | 节点管理 | Nodes / 节点 |
| `frpnexus_3` | 隧道管理 | Tunnels / 隧道 |
| `frpnexus_4` | 配置 | Configurations / 配置 |
| `frpnexus_5` | 日志 | Logs / 日志 |
| `frpnexus_6` | 设置 | Settings / 设置 |

Current main navigation follows the six Stitch pages: 仪表盘, 节点, 隧道, 配置, 日志, 设置. Runtime management remains a product capability, but it should be represented inside the existing Stitch-driven page structure unless a dedicated Stitch runtime page is added later.

Page implementation should proceed through the mapping in `docs/STITCH_IMPLEMENTATION_GUIDE.md` and the page layout rules in `docs/UI_LAYOUT_GUIDE.md`. Stitch pages are the primary source for page composition, but the final implementation must be translated into Avalonia controls, styles, resources, views, and view models.

## Translation Rules

Translate the Stitch design into Avalonia primitives:

- HTML layout -> Avalonia panels and grids
- Tailwind spacing -> Avalonia margins, padding, and row/column definitions
- CSS colors -> resource dictionary brushes
- CSS radius -> Avalonia corner radius resources
- CSS shadows -> Avalonia box shadow or omitted when unnecessary
- HTML sections -> Avalonia `UserControl` views
- Repeated rows/cards -> item controls with view models
- Stitch interaction states -> Avalonia styles, pseudoclasses, and `ControlTemplate` overrides when needed
- Material Symbols-style icons -> Avalonia `PathIcon`, shared icon resources, or dedicated converters/controls

Do not implement:

- HTML rendering
- CSS rendering
- Tailwind runtime
- WebView
- Browser-based UI

Avalonia state translation rule:

- Do not assume `Avalonia.Themes.Fluent` default control templates preserve Stitch states.
- When a default template overrides `Background`, `BorderBrush`, `Foreground`, icon color, caret color, selection color, watermark color, or pressed/hover visuals in a way that conflicts with Stitch, override it in resource dictionaries or dedicated styles.
- For important icon+text commands, child `PathIcon` and `TextBlock` content must follow the parent control foreground across normal, hover, pressed, and disabled states.
- Do not use ordinary text characters as substitutes for key Material Symbols-style icons when a `PathIcon` or shared icon resource is needed for visual fidelity.
- This rule applies to buttons, `TextBox`, password inputs, `ComboBox`, list rows, and table rows. Do not rely on Fluent defaults when they make a Stitch control visually unstable or unreadable.
- Inputs must not show non-Stitch black-background/white-text hover or focus states, unreadable `Watermark` text, invisible caret, or high-contrast selection colors that break the light input surface.
- If input states drift from Stitch, use resource dictionaries, dedicated styles, or `ControlTemplate` overrides to control `Background`, `Foreground`, `BorderBrush`, `CaretBrush`, `SelectionBrush`, `SelectionForegroundBrush`, and watermark presentation.
- `ComboBox` requires full state ownership when Fluent defaults leak non-Stitch overlays. If Setter-only styling cannot control the closed control, popup, selected item, hover item, or pressed item, use a dedicated `ControlTemplate` for the control and item containers.
- `ComboBox` popups should support desktop light-dismiss behavior. Use `IsLightDismissEnabled` and `OverlayDismissEventPassThrough` or equivalent Avalonia behavior when clicking outside should both close the popup and continue to the clicked target.
- Cursor semantics are part of Stitch fidelity: editable text surfaces use an I-beam cursor, while selectable controls, popup items, icon buttons, and lightweight links use a hand cursor.
- Decorative icons inside text inputs must not block input hit testing or prevent the editable area from showing the expected text cursor.
- Custom list/table rows must preserve full-width hover and selected backgrounds. When using `ItemsControl + Grid`, stretch the item container and row content instead of letting rows shrink to their text content.

## Avalonia Structure

When building the app, prefer:

- Shared style resources for colors, typography, spacing, and control sizing.
- A shell view containing left navigation, top command/header area, and page content.
- One view per main module.
- View models for page state and commands.
- `CommunityToolkit.Mvvm` for view model observable properties and commands.
- Placeholder data only while current phase is UI skeleton.

Do not put infrastructure logic such as SSH, SFTP, remote command execution, or FRP process management directly in views.

## Desktop Architecture Organization

Use deliberate Avalonia desktop project structure instead of placing reusable files in the root directory.

Required organization rules:

- View models should inherit from a shared base view model and use `CommunityToolkit.Mvvm` types and source generators such as `ObservableObject`, `[ObservableProperty]`, `RelayCommand`, and `AsyncRelayCommand`.
- Page views belong under a `Views` structure that mirrors the main modules or feature areas.
- Page view models belong under a matching `ViewModels` structure.
- Value converters belong under a dedicated `Converters` folder, or under a feature folder when they are feature-specific.
- Custom controls belong under a dedicated `Controls` folder, or under a feature folder when they are feature-specific.
- Behaviors, attached properties, and interaction helpers belong under dedicated folders such as `Behaviors`, `AttachedProperties`, or a clearly named UI infrastructure folder.
- Resource dictionaries belong under `Styles`, `Themes`, or `Resources`, grouped by responsibility such as colors, typography, controls, icons, and page-specific styles.

Avoid:

- Adding converters, controls, behaviors, or reusable resources directly to the desktop project root.
- Mixing page-specific UI helpers with global UI infrastructure.
- Creating one large catch-all file for unrelated converters or controls.
- Implementing command logic in code-behind when it belongs in a view model command.

## Visual Fidelity Rules

Use `screen.png` files to preserve:

- Overall composition
- Density
- Page hierarchy
- Navigation placement
- Panel shape
- Table/list rhythm
- Status badge appearance
- Log and code panel treatment

Use `code.html` files to preserve:

- Extracted UI tokens and component style patterns
- Chinese copy
- Control order
- Section names
- Table columns
- Example data
- Button labels

Do not render or embed Stitch HTML/CSS. Treat `code.html` as a source for Avalonia resource translation only.

The final UI must preserve interactive behavior, not just static screenshot composition. Verify hover, pressed, disabled, cursor, alignment, and icon-color states when translating Stitch controls.

When Stitch files conflict with older local UI documentation, use the current Stitch screenshots and HTML. For the SideNav, the current Stitch screenshots use a light sidebar by default; the older persistent dark sidebar note in `DESIGN.md` is legacy or optional theme material.

Use `DESIGN.md` to preserve:

- Color tokens
- Typography scale
- Spacing scale
- Radius values
- Component heights
- Sidebar width
- Topbar height

## Required UI Behavior

At minimum, the UI skeleton must include:

- Chinese left navigation.
- `1280 x 800` desktop-friendly shell.
- Adaptive layout down to `1100 x 720`.
- Dashboard overview cards and recent status.
- Nodes list/detail pattern.
- Tunnels list and protocol/status display.
- Configuration editor with TOML preview.
- Logs page with filter/search controls and monospace log area.
- Settings page with grouped setting sections.
- Runtime management affordances where Stitch currently places related node, configuration, and log workflows.

## Prohibited UI Outcomes

Do not produce:

- Marketing landing pages.
- English-only UI.
- Material Design-style admin dashboards.
- Oversized cards with low information density.
- Purple/blue gradient hero-style decoration.
- Web-like responsive mobile layouts.
- HTML/CSS/WebView implementation.
