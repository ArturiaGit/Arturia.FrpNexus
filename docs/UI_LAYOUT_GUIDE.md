# FrpNexus UI Layout Guide

This guide defines the page-level layout rules for the Avalonia desktop UI. Except for the fixed window target and TOML-first configuration direction, page-level visual and layout decisions follow `stitch_frpnexus_design_system`.

Stitch HTML, CSS, and Tailwind classes are references only. Implement layouts with Avalonia XAML, `Grid`, `DockPanel`, `StackPanel`, `ItemsControl`, `DataGrid`, reusable controls, styles, and resource dictionaries.

## Global Shell Layout

The app uses a fixed-fluid desktop shell.

- Window design range: `1100 x 720` to `1280 x 800`.
- Maximum design target: `1280 x 800`.
- Left SideNav width: `212px`.
- TopBar height: `52px`.
- Page margin: `24px`.
- Base spacing grid: `4px`.
- Common gaps: `8px`, `12px`, `16px`, `20px`, `24px`.
- Page content fills the remaining space after SideNav and TopBar.
- Page content must avoid browser-style mobile breakpoints and landing-page composition.

Suggested shell structure:

- Root `Grid`
  - Column 0: fixed `212px` SideNav.
  - Column 1: remaining content.
  - Row 0 in content column: fixed `52px` TopBar.
  - Row 1 in content column: page canvas with `24px` padding.

## SideNav Layout

The default main navigation is the light sidebar shown in the current Stitch screenshots and majority Stitch page HTML. This resolves the earlier dark-sidebar conflict: although older design notes mention `#111827`, the current FrpNexus default follows Stitch screenshots.

- Background: light app background or surface, normally `FrpBackgroundBrush`.
- Width: `212px`.
- Right border: `1px FrpBorderDefaultBrush`.
- Vertical padding: `24px`.
- Brand area: app name, optional version, compact logo mark.
- Navigation items:
  - Height: content-driven around `36px` to `40px`.
  - Horizontal padding: `16px`.
  - Vertical padding: `8px`.
  - Icon size: `20px`.
  - Icon/text gap: `12px`.
- Active item:
  - Text and icon use `FrpPrimaryBrush`.
  - Background uses subtle selected surface, such as `FrpSelectionSubtleBrush`.
  - Left indicator is `3px FrpPrimaryBrush`.
- Inactive item:
  - Text and icon use `FrpTextSecondaryBrush`.
  - Hover background uses `FrpSurfaceContainerBrush`.
- Footer area may contain account, profile, or local status actions.

Dark navigation is not the current default. Dark surfaces are reserved for logs, terminals, TOML previews, and other technical content panels.

## TopBar Layout

The TopBar is a compact command and status area.

- Height: `52px`.
- Background: `FrpSurfaceBrush` or `FrpSurfaceWhiteBrush`.
- Bottom border: `1px FrpBorderDefaultBrush`.
- Horizontal padding: `24px`.
- Left area: current page title, optional breadcrumb or contextual subtitle.
- Right area:
  - Connection status badge.
  - Compact icon buttons for notification, network, or sync status.
  - Optional page-level commands when they do not belong in the page toolbar.
- Icon buttons should be `28px` to `32px` square.
- TopBar must not grow taller because of long text; truncate or move secondary details into the page body.
- The right area should use stable layout such as `Grid` with `*,Auto` or an equivalent right-aligned container; do not rely on ambiguous docking when the right-side controls must stay flush right.
- The connection status badge should be fixed-height, vertically centered, and include a `6px` to `8px` status dot aligned with the text.
- TopBar icon buttons should remain `32px x 32px`, centered, and visibly interactive in normal, hover, and pressed states.

## Page Canvas Rules

- Page canvas uses `24px` padding and `12px` to `24px` section gaps.
- Primary panels use `FrpSurfaceWhiteBrush`, `1px` border, `8px` radius, and restrained shadow.
- Tables and technical panels should fill available height instead of creating long page scroll whenever possible.
- Use `Grid` row/column definitions for stable layout rather than nested ad hoc stacks.
- Avoid cards inside cards unless the inner element is a real list, table, or editor frame.
- Keep information density close to Stitch: compact controls, `13px` body text, `40px` table rows.
- Data tables and custom list tables must keep the 表头, row content, 选中背景, and separators on one shared width system so columns do not drift between header and body.
- If a table is implemented with `ItemsControl + Grid`, stretch every layer that participates in row width: `ItemsControl`, item container, row button, and row content `Grid`.
- Use truncation for long cells before changing shared column definitions; selection backgrounds, hover backgrounds, and separators should continue to fill the full panel width.

## Dashboard Layout

Source: `stitch_frpnexus_design_system/frpnexus_1`.

Dashboard is an overview page with compact status, actions, recent state, errors, and log preview.

- Top row: four equal metric tiles.
  - Tile content: icon circle, helper label, large numeric value.
  - Grid: four columns at `1280 x 800`; keep all four visible at the design target.
- Middle row:
  - Left: quick actions panel around one third width.
  - Right: recent node status table around two thirds width.
- Bottom row:
  - Left: recent errors panel.
  - Right: dark system log preview panel.
- Log preview uses dark technical-panel styling and monospace text.
- Dashboard cards must stay compact; do not convert them into marketing-style KPI blocks.
- Confirmed Avalonia baseline:
  - Use a four-column metric row, a `4/8` middle split, and a `6/6` bottom split.
  - Use `16px` gaps between Dashboard panels in the current `1280 x 800` target.
  - Quick action buttons must stretch across the full panel width and keep centered icon+Chinese-label content.
  - Panel title rows with right-side actions, such as `近期节点状态` / `查看全部`, should use `Grid` columns `*,Auto` so the action remains at the right edge.
  - `查看全部` is an interactive lightweight link/button, not static text; it should show a hand cursor and hover feedback.
  - Recent-node status badges in this table are text-only capsules; do not add extra dots unless Stitch shows them.
  - The compact log preview uses `#0B1117`, monospace log rows, colored bracketed levels such as `[INFO]`, and a right-aligned realtime badge with a green dot.
  - Recent error rows use a red left border, low-opacity error tint, compact spacing, and no card nesting.

## Nodes Page Layout

Source: `stitch_frpnexus_design_system/frpnexus_2`.

Nodes use a list plus details pattern.

- Page body is a horizontal split.
- Left area: node list/table panel, fills available width.
- Right area: selected node details panel, fixed around `320px`.
- Main list panel:
  - Top toolbar with primary action, refresh action, and search.
  - Data grid with checkbox, name, IP address, node status, FRP service status, version.
  - Footer for count and pagination when needed.
  - The 表头, body rows, selected row background, hover background, and horizontal separators must use the same columns and fill the panel width.
  - Checkbox visual column, node name, IP address, status, FRP service, and version columns must stay aligned between header and row templates.
  - A `3px` selected-row indicator should sit at the left edge of the row without shifting the checkbox or data columns.
  - Long node names, IPs, and version strings should trim within their own cells instead of forcing later columns out of alignment.
  - When using an `ItemsControl + Grid` table, set `ItemsControl`, item container, row button, and row content `Grid` to `HorizontalAlignment="Stretch"` or an equivalent full-width layout.
- Details panel:
  - Header with node name and OS metadata.
  - Quick actions grid.
  - SSH connection information.
  - FRP details.
- IP, ports, paths, versions, and process values use technical font.

At the minimum window width, preserve the details panel if possible. If content becomes cramped, reduce table column width and truncate lower-priority text before hiding primary actions.

## Tunnels Page Layout

Source: `stitch_frpnexus_design_system/frpnexus_3`.

Tunnels are primarily a command bar plus data grid workflow.

- Top command bar:
  - Left: search, protocol filter, status filter.
  - Right: create tunnel primary action and utility actions.
- Main content: tunnels `DataGrid`.
- Required columns should cover tunnel name, protocol, local endpoint, remote endpoint, domain when applicable, bound node, status, and operations.
- Protocol and status should use compact badges.
- Code-like endpoint values use technical font.
- Row height should remain around `40px`.
- Avoid vertical grid lines; use horizontal separators and hover state.

## Configurations Page Layout

Source: `stitch_frpnexus_design_system/frpnexus_4`.

Configurations use an editor/preview split.

- Page body is a horizontal split.
- Left side: form and configuration sections.
- Right side: TOML preview.
- Left form:
  - Group fields by connection, common, tunnel, and advanced settings.
  - Keep labels compact and Chinese-first.
  - Prefer fixed-height inputs/selects around `34px`.
- Right TOML preview:
  - Dark code panel.
  - Header bar with file name or generated target.
  - Monospace text and optional line numbers.
  - Must remain readable without horizontal layout jitter.
- Page actions such as generate, validate, upload, and save belong in a command area, not scattered across form rows.

## Runtime Workflow Placement

Runtime management remains a product capability, but the current Stitch navigation has no dedicated `运行` page. Do not force a separate Runtime navigation item unless a future Stitch design adds one.

- Place runtime status, process control, and diagnostics affordances inside the current Stitch-driven page structure.
- Prefer 节点 for node-scoped quick actions and process state.
- Prefer 配置 for TOML generation and configuration-oriented deployment preparation.
- Prefer 日志 for runtime log inspection and streaming states.
- Use the same technical font, compact density, and dark technical panels shown in Stitch.

## Logs Page Layout

Source: `stitch_frpnexus_design_system/frpnexus_5`.

Logs use a toolbar plus full-height terminal panel.

- Page body is vertical.
- Top toolbar:
  - Search.
  - Node filter.
  - Process filter.
  - Level filter.
  - Auto-refresh toggle.
  - Copy and clear actions.
- Main terminal panel:
  - Fills remaining height.
  - Dark background: `FrpTerminalSurfaceBrush` or `FrpCodePanelBackgroundBrush`.
  - Header bar around `28px`.
  - Log line body uses technical font.
  - Footer status bar may show file name, line count, or encoding.
- Timestamp column should be stable around `160px`; level column around `60px`.
- Error rows may use a `3px` left status border.

## Settings Page Layout

Source: `stitch_frpnexus_design_system/frpnexus_6`.

Settings use centered grouped cards.

- Page canvas scrolls vertically when needed.
- Content max width should stay readable, around `760px` to `900px`.
- Groups are stacked vertically with `16px` to `24px` gaps.
- Each settings group has:
  - Section title.
  - Surface card.
  - Rows separated by subtle dividers.
  - Left icon or label area.
  - Description/helper text.
  - Right-aligned control when compact; below text when wide input is needed.
- Avoid dashboard-style metric cards on settings pages.

## Adaptive Behavior

Within `1100 x 720` to `1280 x 800`:

- Keep SideNav fixed at `212px`.
- Keep TopBar fixed at `52px`.
- Keep page margin at `24px` unless a specific page would overflow; then reduce internal panel gaps before changing shell metrics.
- Use truncation for long names, paths, domains, and endpoints.
- Prefer internal scrolling in tables, details panels, terminal panels, and TOML previews.
- Do not collapse SideNav into a mobile hamburger pattern in the current design target.
- Do not scale font sizes with viewport width.

## Layout Anti-Patterns

Do not implement:

- Dark main SideNav as the current default.
- Web landing pages, hero sections, or marketing layouts.
- Non-Stitch admin dashboard spacing.
- Large low-density cards that waste the `1280 x 800` workspace.
- Page-specific shell variants.
- Repeated one-off spacing values when a shared token exists.
- HTML/CSS/Tailwind/WebView-based layout.
- Runtime layout changes that introduce a navigation item or page not present in Stitch.
