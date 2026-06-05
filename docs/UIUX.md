# FrpNexus UI/UX Constraints

## Design Direction

FrpNexus is a desktop application. The UI must feel like a professional Windows desktop operations tool, not a website or SaaS dashboard.

Except for the fixed window target and TOML-first configuration direction, the visual, layout, component-density, navigation, icon, copy, and page-organization standard is `stitch_frpnexus_design_system`.

The visual direction follows the current Stitch design, which is a Windows 11-style desktop operations console:

- Windows 11-style desktop layout.
- Clear hierarchy.
- Light tonal surfaces.
- 1px borders.
- Conservative shadows.
- Stitch-defined navigation, command bars, dialogs, info bars, lists, tables, toggles, progress indicators, and Material Symbols-style icons.

## Language

The interface must be Chinese-first.

Use Chinese for:

- Navigation
- Buttons
- Labels
- Empty states
- Error messages
- Dialogs
- Status text
- Tooltips

Keep technical terms in English or original casing when appropriate:

- `FRP`
- `SSH`
- `SFTP`
- `TOML`
- `TCP`
- `UDP`
- `HTTP`
- `HTTPS`
- `Token`
- `frpc`
- `frps`

## Window And Adaptiveness

FrpNexus is designed for desktop windows.

- Maximum design target: `1280 x 800`
- Recommended design size: `1280 x 800`
- Minimum usable window: `1100 x 720`
- The UI must adapt between `1100 x 720` and `1280 x 800`
- Do not design for `1440 x 900`, `1920 x 1080`, or ultrawide screens as primary targets

At `1280 x 800`, the app must show:

- Main navigation
- Page title
- Primary actions
- Main content
- Important status information

## Layout

Use a desktop fixed-fluid structure:

- Fixed left navigation width: `212px`
- Top command/header area height: `52px`
- Main content area fills remaining space
- Page margin: `24px`
- Base spacing grid: `4px`

Avoid:

- Marketing hero sections
- Floating decorative sections
- Nested cards inside cards
- Web grid-heavy layouts
- Material Design visual language

## Typography

Interface font stack:

- `Segoe UI`
- `Microsoft YaHei UI`
- `PingFang SC`
- `Noto Sans CJK SC`
- `system-ui`
- `sans-serif`

Technical font stack:

- `JetBrains Mono`
- `Cascadia Code`
- `Consolas`
- `monospace`

Use monospace fonts for logs, commands, paths, ports, IP addresses, and TOML previews.

Base UI text should be compact and desktop-like:

- Page title: 20px / 28px / 600
- Section title: 16px / 24px / 600
- Body and table text: 13px / 20px / 400
- Helper text: 12px / 18px / 400
- Code and logs: 12px / 18px / 400

## Tokens

Core colors:

- App background: `#F6F8FA`
- Sidebar background: current Stitch screenshots use a light sidebar surface; the older `#111827` dark sidebar note is legacy or optional theme material, not the default.
- Surface: `#FFFFFF`
- Subtle surface: `#F3F6F8`
- Default border: `#D8DEE6`
- Strong border: `#C4CCD6`
- Primary text: `#17202A`
- Secondary text: `#52606D`
- Muted text: `#8792A0`
- Primary brand: `#2563EB`
- Primary hover: `#1D4ED8`
- Focus ring: `#93C5FD`

Status colors:

- Success: `#16A34A`
- Warning: `#D97706`
- Error: `#DC2626`
- Info: `#2563EB`
- Neutral: `#64748B`

Shapes:

- Small controls: `4px`
- Buttons, inputs, badges: `6px`
- Panels, cards, dialogs: `8px`
- Do not use pill-shaped buttons for primary desktop controls

Control sizes:

- Primary button: `36px`
- Secondary button: `32px`
- Input: `34px`
- Compact input: `30px`
- Table row: `40px`
- Compact table row: `36px`
- Top bar: `52px`
- Sidebar: `212px`

## Icon Direction

Follow the icon style, icon placement, and icon meaning in the Stitch HTML and screenshots.

Material Symbols-style icons are allowed in the final Avalonia UI when they best preserve Stitch fidelity.
