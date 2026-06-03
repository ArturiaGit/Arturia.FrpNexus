# Avalonia UI Implementation Constraints

## Design Sources

Use these sources when implementing the Avalonia desktop UI:

- Product source: `docs/PRODUCT.md`
- UI/UX source: `docs/UIUX.md`
- Design system tokens: `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md`
- Visual references: `stitch_frpnexus_design_system/frpnexus_*/screen.png`
- Layout and copy references: `stitch_frpnexus_design_system/frpnexus_*/code.html`

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

If the implementation has a separate Runtime page, design it using the same visual system and place it between 配置 and 日志 in navigation.

## Translation Rules

Translate the Stitch design into Avalonia primitives:

- HTML layout -> Avalonia panels and grids
- Tailwind spacing -> Avalonia margins, padding, and row/column definitions
- CSS colors -> resource dictionary brushes
- CSS radius -> Avalonia corner radius resources
- CSS shadows -> Avalonia box shadow or omitted when unnecessary
- HTML sections -> Avalonia `UserControl` views
- Repeated rows/cards -> item controls with view models

Do not implement:

- HTML rendering
- CSS rendering
- Tailwind runtime
- WebView
- Browser-based UI

## Avalonia Structure

When building the app, prefer:

- Shared style resources for colors, typography, spacing, and control sizing.
- A shell view containing left navigation, top command/header area, and page content.
- One view per main module.
- View models for page state and commands.
- Placeholder data only while current phase is UI skeleton.

Do not put infrastructure logic such as SSH, SFTP, remote command execution, or FRP process management directly in views.

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

- Chinese copy
- Control order
- Section names
- Table columns
- Example data
- Button labels

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

## Prohibited UI Outcomes

Do not produce:

- Marketing landing pages.
- English-only UI.
- Material Design-style admin dashboards.
- Oversized cards with low information density.
- Purple/blue gradient hero-style decoration.
- Web-like responsive mobile layouts.
- HTML/CSS/WebView implementation.

