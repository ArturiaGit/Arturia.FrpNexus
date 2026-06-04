# FrpNexus UI Style Guide

This guide is the implementation-facing UI style source for FrpNexus. It extracts colors, typography, spacing, shape, component density, and technical panel treatments from `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md` and the `frpnexus_1` through `frpnexus_6` Stitch screens.

The final Avalonia UI must feel like a Windows 11 desktop operations tool using WinUI 3 / Fluent Design principles. Stitch HTML and Tailwind classes are references only; implement the UI with Avalonia XAML, styles, resource dictionaries, views, and MVVM-friendly view models.

## Source Mapping

Use these Stitch sources:

- Global tokens: `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md`
- Dashboard patterns: `stitch_frpnexus_design_system/frpnexus_1/code.html`
- Nodes patterns: `stitch_frpnexus_design_system/frpnexus_2/code.html`
- Tunnels patterns: `stitch_frpnexus_design_system/frpnexus_3/code.html`
- Configuration patterns: `stitch_frpnexus_design_system/frpnexus_4/code.html`
- Logs patterns: `stitch_frpnexus_design_system/frpnexus_5/code.html`
- Settings patterns: `stitch_frpnexus_design_system/frpnexus_6/code.html`
- Visual verification references: `stitch_frpnexus_design_system/frpnexus_*/screen.png`

When Stitch tokens and older docs differ, this guide is the normalized Avalonia implementation target. In particular, the current default SideNav is light Fluent NavigationView-style. The older dark sidebar note and any `#111827` sidebar resource should be treated as legacy or optional theme-experiment material, not the Phase 1 default.

## Design Principles

- Keep a WinUI 3 / Fluent-inspired Windows 11 desktop tool style.
- Prioritize clarity, stable hierarchy, and high information density.
- Use tonal surfaces, 1px borders, compact controls, and conservative shadows.
- Keep the UI Chinese-first. Technical terms such as `FRP`, `SSH`, `SFTP`, `TOML`, `TCP`, `UDP`, `HTTP`, `HTTPS`, `Token`, `frpc`, and `frps` may remain in English.
- Do not use marketing hero layouts, web admin dashboards, Material Design visuals, oversized low-density cards, decorative gradients, or WebView/HTML rendering.

## Color Tokens

Define these as Avalonia brushes in shared style resources. Suggested resource names use PascalCase and the `Frp` prefix to avoid collisions with Avalonia theme resources.

| Stitch token | Avalonia resource | Value | Usage |
|---|---|---:|---|
| `background` | `FrpBackgroundBrush` | `#F8F9FB` | App background and page canvas |
| `surface` | `FrpSurfaceBrush` | `#F8F9FB` | Light surface foundation |
| `surface-white` / `surface-container-lowest` | `FrpSurfaceWhiteBrush` | `#FFFFFF` | Panels, cards, inputs |
| `surface-container-low` | `FrpSurfaceLowBrush` | `#F2F4F6` | Toolbar backgrounds, subtle sections |
| `surface-container` | `FrpSurfaceContainerBrush` | `#ECEEF0` | Hover surface |
| `surface-container-high` | `FrpSurfaceHighBrush` | `#E6E8EA` | Stronger hover or nested surface |
| `surface-container-highest` / `surface-variant` | `FrpSurfaceVariantBrush` | `#E0E3E5` | Neutral badges and separators |
| `surface-dim` | `FrpSurfaceDimBrush` | `#D8DADC` | Dim surface and inactive overlays |
| `border-default` | `FrpBorderDefaultBrush` | `#D8DEE6` | Default 1px borders |
| `outline` | `FrpOutlineBrush` | `#737686` | Strong outlines and secondary strokes |
| `outline-variant` | `FrpOutlineVariantBrush` | `#C3C6D7` | Subtle outlines and scrollbar thumbs |
| `text-primary` | `FrpTextPrimaryBrush` | `#17202A` | Primary text |
| `text-secondary` | `FrpTextSecondaryBrush` | `#52606D` | Secondary text |
| `on-surface-variant` | `FrpTextMutedBrush` | `#434655` | Muted surface text |
| `on-background` / `on-surface` | `FrpTextStrongBrush` | `#191C1E` | Strong text on light background |
| `primary` | `FrpPrimaryBrush` | `#004AC6` | Primary actions and selected nav |
| `primary-container` | `FrpPrimaryContainerBrush` | `#2563EB` | Brand accent and status info alignment |
| `surface-tint` | `FrpPrimaryHoverBrush` | `#0053DB` | Primary hover |
| `on-primary` | `FrpOnPrimaryBrush` | `#FFFFFF` | Text on primary |
| `secondary-container` | `FrpSelectionSubtleBrush` | `#D9DFF5` | Active nav background |
| `inverse-surface` | `FrpInverseSurfaceBrush` | `#2D3133` | Dark panel border/surface |
| `inverse-on-surface` | `FrpInverseTextBrush` | `#EFF1F3` | Text on dark panel |
| `on-secondary-fixed` | `FrpTerminalSurfaceBrush` | `#141B2B` | Logs terminal background |
| custom from Stitch log/TOML panels | `FrpCodePanelBackgroundBrush` | `#0B1117` | TOML preview and compact log panel |

Status colors:

| Token | Avalonia resource | Value | Usage |
|---|---|---:|---|
| `status-success` | `FrpStatusSuccessBrush` | `#16A34A` | Online, running, OK |
| `status-warning` | `FrpStatusWarningBrush` | `#D97706` | Warning, stopped, retrying |
| `status-error` | `FrpStatusErrorBrush` | `#DC2626` | Error, offline, failed |
| `status-info` | `FrpStatusInfoBrush` | `#2563EB` | Info, active process, neutral info |
| `status-neutral` | `FrpStatusNeutralBrush` | `#64748B` | Stopped, unknown, neutral |
| `error-container` | `FrpErrorContainerBrush` | `#FFDAD6` | Error background tint |

Use low-opacity status backgrounds for badges, such as success at 10% opacity with a 20% opacity border. In Avalonia, prefer dedicated tinted brushes if opacity resources are hard to maintain consistently.

## Typography

Stitch uses `Inter` and `JetBrains Mono`. Avalonia implementation must use a Windows and Chinese-first stack while preserving Stitch sizing and weights.

UI font family:

```text
Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC, system-ui, sans-serif
```

Technical font family:

```text
JetBrains Mono, Cascadia Code, Consolas, monospace
```

Use technical font for logs, command output, paths, IP addresses, ports, versions, fingerprints, and TOML previews.

| Style | Resource name | Size | Line height | Weight | Usage |
|---|---|---:|---:|---:|---|
| Page title | `FrpPageTitleTextStyle` | `20px` | `28px` | `600` | Topbar page title |
| Section title | `FrpSectionTitleTextStyle` | `16px` | `24px` | `600` | Panel and section headings |
| Group label | `FrpGroupLabelTextStyle` | `14px` | `22px` | `600` | Form groups, setting names |
| Body | `FrpBodyTextStyle` | `13px` | `20px` | `400` | Default UI copy |
| Table content | `FrpTableTextStyle` | `13px` | `20px` | `400` | Data grid rows |
| Helper text | `FrpHelperTextStyle` | `12px` | `18px` | `400` | Hints, metadata |
| Status badge | `FrpStatusBadgeTextStyle` | `12px` | `16px` | `500` | Status badges |
| Code block | `FrpCodeTextStyle` | `12px` | `18px` | `400` | Logs and TOML |

Do not scale font sizes with viewport width. Keep text compact and desktop-like.

## Spacing And Layout

Use a 4px base grid.

| Token | Resource name | Value | Usage |
|---|---|---:|---|
| `base` | `FrpSpacingBase` | `4px` | Micro spacing |
| `tight` | `FrpSpacingTight` | `8px` | Nav item padding, compact gaps |
| `md` | `FrpSpacingMedium` | `12px` | Field gaps, toolbar gaps |
| `lg` | `FrpSpacingLarge` | `16px` | Panel padding, button horizontal padding |
| `xl` | `FrpSpacingXLarge` | `20px` | Section gaps |
| `page-margin` | `FrpPageMargin` | `24px` | Page canvas padding |
| `sidebar-width` | `FrpSidebarWidth` | `212px` | Left navigation |
| `topbar-height` | `FrpTopbarHeight` | `52px` | Top command area |

Window targets:

- Recommended and maximum design target: `1280 x 800`
- Minimum usable window: `1100 x 720`
- Design and QA only within `1100 x 720` to `1280 x 800`

Structural layout:

- Left navigation: fixed `212px`
- Topbar: fixed `52px`
- Page canvas: fills remaining space with `24px` padding
- Common page gap: `12px` or `24px`, depending on section separation
- Tables: `40px` standard rows; compact rows can use `36px`
- Details side panel: Stitch nodes page uses about `320px`

## Shape, Border, And Elevation

Use restrained Fluent shapes.

| Element | Radius |
|---|---:|
| Small controls and inline icon buttons | `4px` |
| Buttons, inputs, selects, neutral badges | `4px` to `6px` |
| Main panels, cards, dialogs, terminal panels | `8px` |
| Decorative circles only, such as status dots or avatar/icon circles | Full round |

Borders:

- Default border: `1px #D8DEE6`
- Strong outline: `1px #C3C6D7` or `#737686`
- Selected navigation indicator: `3px` vertical primary bar
- Data grids use horizontal separators only; avoid vertical grid lines.

Elevation:

- Primary panels: `0 1px 2px rgba(15, 23, 42, 0.08)`
- Settings cards may use `0 1px 2px rgba(15, 23, 42, 0.04)`
- Hovered settings cards may use `0 2px 6px rgba(15, 23, 42, 0.08)`
- Avoid heavy shadows and floating decorative sections.

## Components

### Side Navigation

Extracted from all Stitch pages.

- Width: `212px`
- Default background: `FrpBackgroundBrush` or another light surface token
- Right border: `1px FrpBorderDefaultBrush`
- Top/bottom padding: `24px`
- Item padding: horizontal `16px`, vertical `8px`
- Item gap: `12px`
- Icon size: `20px`
- Active item:
  - Text/icon: `FrpPrimaryBrush`
  - Background: `FrpSelectionSubtleBrush` at about 50%
  - Left indicator: `3px FrpPrimaryBrush`
- Inactive item:
  - Text/icon: `FrpTextSecondaryBrush`
  - Hover background: `FrpSurfaceContainerBrush`

Use Fluent-style icons in Avalonia. Material Symbols in Stitch are visual placeholders only.

SideNav color rule:

- The current default SideNav must be light, matching WinUI 3 / Fluent NavigationView behavior and the majority of Stitch `code.html` pages.
- Do not use `#111827` as the default main navigation background.
- If existing resources contain a dark sidebar token, treat it as legacy or future optional theme material until an explicit theme task migrates it.
- Dark panels remain correct for logs, terminal output, TOML preview, and code-oriented technical areas.

### Top App Bar

- Height: `52px`
- Background: `FrpSurfaceBrush` or `FrpSurfaceWhiteBrush`
- Bottom border: `1px FrpBorderDefaultBrush`
- Horizontal padding: `24px`
- Page title: `FrpPageTitleTextStyle`
- Right actions: compact icon buttons `32px x 32px`
- Connection status: status badge style with dot, Chinese text, and success color when ready.

### Command Bar

- Background: usually panel surface or page background.
- Height follows content; controls use `32px` to `36px`.
- Left side: search and filters.
- Right side: primary action or destructive/utility actions.
- Use `8px` to `12px` gaps.
- Use icon plus Chinese label for clear commands.

### Buttons

Primary button:

- Height: `36px`
- Horizontal padding: `16px`
- Background: `FrpPrimaryBrush`
- Hover: `FrpPrimaryHoverBrush`
- Text: `FrpOnPrimaryBrush`
- Radius: `4px` to `6px`
- Border: transparent or primary
- Font: body or group label depending on emphasis

Secondary button:

- Height: `32px`
- Horizontal padding: `12px` to `16px`
- Background: transparent or white surface
- Border: `1px FrpBorderDefaultBrush`
- Hover: `FrpSurfaceLowBrush` or `FrpSurfaceContainerBrush`
- Text: `FrpTextPrimaryBrush`

Icon buttons:

- Common size: `28px` or `32px`
- Radius: `4px`
- Icon size: `16px` to `20px`
- Hover background: `FrpSurfaceVariantBrush` or `FrpSurfaceContainerBrush`

Avoid pill-shaped primary buttons.

### Inputs And Selects

- Height: `34px`
- Background: `FrpSurfaceWhiteBrush`
- Border: `1px FrpBorderDefaultBrush`
- Radius: `4px`
- Horizontal padding: `12px`
- Search input left icon inset: `32px` to `36px`
- Focus: primary border plus subtle focus ring or bottom-heavy border.
- Placeholder: secondary text with reduced opacity.

### Data Grid And Lists

Extracted from nodes and tunnels pages.

- Header row: `40px`
- Body row: `40px`
- Row separators: `1px FrpBorderDefaultBrush`
- Header background: `FrpSurfaceLowBrush` or `FrpSurfaceWhiteBrush`
- Header text: group label/helper tone, secondary color
- Body text: table content style
- Hover row background: `FrpSurfaceLowBrush`
- Selected row:
  - Subtle primary background at about 5%
  - `3px` primary left indicator when selection needs emphasis
- Avoid vertical dividers.
- Code-like cells such as IP, port, version, path, and domain use technical font.

### Status Badges

- Font: `12px / 16px / 500`
- Height: content-driven, usually `20px` to `22px`
- Padding: horizontal `8px`, vertical `2px` to `4px`
- Radius: can be full for small status chips, despite primary buttons avoiding pills.
- Optional dot: `6px` to `8px`
- Border: same status color at about 20% opacity.
- Use:
  - Success: 在线, 运行中, 已就绪
  - Warning: 已停止, 负载较高, 重试中
  - Error: 离线, 异常, 授权失败
  - Neutral: 未安装, 未知, 已停止 when not warning
  - Info: informational process states

### Cards And Panels

- Background: `FrpSurfaceWhiteBrush`
- Border: `1px FrpBorderDefaultBrush`
- Radius: `8px`
- Padding: usually `16px`; settings rows can use `16px`
- Shadow: subtle `0 1px 2px rgba(15, 23, 42, 0.08)`
- Do not nest cards inside cards unless the inner element is a real list/table frame.

### Settings Cards

Extracted from settings page.

- Section heading: `FrpSectionTitleTextStyle`
- Card rows separated by `1px` divider using default border at reduced opacity
- Row padding: `16px`
- Left icon size: about `20px`
- Title: group label
- Description: helper text
- Controls aligned to the right or below the description for larger fields.

### Log Panel

Extracted from dashboard and logs pages.

- Background: `#0B1117` for compact log preview or `#141B2B` for terminal-style full log page.
- Text: `FrpInverseTextBrush`
- Font: technical font `12px / 18px`
- Panel radius: `8px`
- Border: dark outline `#2D3133` or outline at low opacity
- Terminal header height: about `28px`
- Log line:
  - Timestamp width about `160px`
  - Level width about `60px`
  - Hover background: white at about 5%
  - Error line: status error tint plus `3px` left border
- Scrollbar: dark subtle track and thumb when supported by platform styling.

### TOML Preview

Extracted from configuration page.

- Background: `#0B1117`
- Header background: `#141B2B`
- Border: `#2D3133`
- Text: `FrpInverseTextBrush`
- Font: technical font
- Line number gutter:
  - Width: about `40px`
  - Background: `#0A0F14`
  - Text: muted dark code tone
- Syntax colors:
  - Key: `#8F3A2C`, medium weight
  - String: `#16A34A`
  - Number: `#2563EB`
  - Section/comment: `#64748B`, section medium/semi-bold

### Empty States, Info Bars, And Dialogs

Use Fluent-inspired quiet surfaces.

- Empty states should be compact and actionable, not hero-like.
- Info bars use status tint backgrounds and 1px status borders.
- Dialogs use surface white, radius `8px`, default border, and conservative shadow.
- Dialog buttons follow primary/secondary button rules.

## UI Library Policy

The project may use third-party Avalonia UI libraries only as control capability supplements.

- The visual system in this guide remains authoritative.
- Third-party themes must not replace the FrpNexus color, typography, spacing, shape, and density rules.
- Controls from a UI library must still bind to `CommunityToolkit.Mvvm` view models through Avalonia binding and commands.
- Do not put business logic in third-party control code-behind.
- If a library's default style conflicts with this guide, override it through resource dictionaries or do not use that control.

Default baseline:

- Use `Avalonia.Themes.Fluent` plus FrpNexus resource dictionaries.
- Evaluate additional libraries only when a real control gap exists.

## Avalonia Resource Organization

Recommended resource dictionaries:

- `Styles/DesignTokens.axaml`: colors, spacing, typography, radius, border thickness
- `Styles/Controls.axaml`: shared Button, TextBox, ComboBox, DataGrid, Toggle, CheckBox styles
- `Styles/Navigation.axaml`: sidebar and topbar styles if they grow large
- `Styles/Status.axaml`: status badges, dots, info bars
- `Styles/CodePanels.axaml`: log panel and TOML preview styles

Use resource names that describe semantic purpose, not one-off page usage. Do not hardcode Stitch hex colors repeatedly in page XAML.

## Prohibited Style Drift

Do not introduce:

- Material Design visual language.
- SaaS dashboard cards with oversized whitespace.
- Marketing hero sections.
- Purple/blue gradient decoration.
- Web responsive mobile layouts.
- Fully custom color palettes that bypass these tokens.
- Page-specific typography scales.
- HTML, CSS, Tailwind, or WebView-based rendering.
