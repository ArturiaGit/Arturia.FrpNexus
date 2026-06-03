---
name: FrpNexus Design System
colors:
  surface: '#f8f9fb'
  surface-dim: '#d8dadc'
  surface-bright: '#f8f9fb'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f2f4f6'
  surface-container: '#eceef0'
  surface-container-high: '#e6e8ea'
  surface-container-highest: '#e0e3e5'
  on-surface: '#191c1e'
  on-surface-variant: '#434655'
  inverse-surface: '#2d3133'
  inverse-on-surface: '#eff1f3'
  outline: '#737686'
  outline-variant: '#c3c6d7'
  surface-tint: '#0053db'
  primary: '#004ac6'
  on-primary: '#ffffff'
  primary-container: '#2563eb'
  on-primary-container: '#eeefff'
  inverse-primary: '#b4c5ff'
  secondary: '#575e70'
  on-secondary: '#ffffff'
  secondary-container: '#d9dff5'
  on-secondary-container: '#5c6274'
  tertiary: '#525658'
  on-tertiary: '#ffffff'
  tertiary-container: '#6a6e70'
  on-tertiary-container: '#eef1f3'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dbe1ff'
  primary-fixed-dim: '#b4c5ff'
  on-primary-fixed: '#00174b'
  on-primary-fixed-variant: '#003ea8'
  secondary-fixed: '#dce2f7'
  secondary-fixed-dim: '#c0c6db'
  on-secondary-fixed: '#141b2b'
  on-secondary-fixed-variant: '#404758'
  tertiary-fixed: '#e0e3e5'
  tertiary-fixed-dim: '#c4c7c9'
  on-tertiary-fixed: '#181c1e'
  on-tertiary-fixed-variant: '#434749'
  background: '#f8f9fb'
  on-background: '#191c1e'
  surface-variant: '#e0e3e5'
  status-success: '#16A34A'
  status-warning: '#D97706'
  status-error: '#DC2626'
  status-info: '#2563EB'
  status-neutral: '#64748B'
  surface-white: '#FFFFFF'
  border-default: '#D8DEE6'
  text-primary: '#17202A'
  text-secondary: '#52606D'
typography:
  page-title:
    fontFamily: inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  section-title:
    fontFamily: inter
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
  group-label:
    fontFamily: inter
    fontSize: 14px
    fontWeight: '600'
    lineHeight: 22px
  body:
    fontFamily: inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 20px
  table-content:
    fontFamily: inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 20px
  helper-text:
    fontFamily: inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
  status-badge:
    fontFamily: inter
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
  code-block:
    fontFamily: jetbrainsMono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 4px
  tight: 8px
  md: 12px
  lg: 16px
  xl: 20px
  page-margin: 24px
  sidebar-width: 212px
  topbar-height: 52px
---

## Brand & Style

The design system is engineered for **FrpNexus**, a high-performance FRP desktop console. The brand personality is **Professional, Stable, and Technical**, prioritizing utility and system transparency over decorative elements. It is a "Workstation First" environment designed specifically for developers and system administrators who require high information density and operational reliability.

The design style is a faithful implementation of **Corporate / Modern (WinUI 3 / Fluent Design)**. It leverages a structured layout, subtle depth through layered surfaces, and a conservative use of motion to feel like a native extension of the Windows 11 ecosystem. The aesthetic is defined by its "Flat-plus" philosophy—relying on precise 1px borders and tonal shifts rather than heavy shadows to establish hierarchy. The interface is optimized for localized Chinese (SC) environments, ensuring vertical rhythm and legibility are maintained across technical data sets.

## Colors

This design system utilizes a structured palette that supports both Light and Dark modes, though it defaults to Light for the main content area. A defining characteristic is the **persistent dark sidebar** (#111827), which remains constant across themes to ground the navigation and maintain a "console" aesthetic.

- **Primary (Brand Blue):** Used for primary actions, active navigation states, and focus indicators.
- **Backgrounds:** The app background uses a cool neutral gray (#F6F8FA), while interactive surfaces like cards and panels use pure white (#FFFFFF) to create a clear "layer" effect.
- **Status Colors:** These are functionally mapped to FRP states. Use Green for online/running, Amber for port conflicts or warnings, and Red for connection failures or errors.
- **Borders:** Subtle 1px borders (#D8DEE6) are the primary method of separation, ensuring the UI remains clean even at high information density.

## Typography

The typography system is rigid and utility-focused. For desktop environments, it prioritizes **Inter** (as a high-quality substitute for Segoe UI/Microsoft YaHei UI logic) for general UI and **JetBrains Mono** for technical data, logs, and configuration previews.

- **Legibility:** All sizes are optimized for the 13px base common in professional Windows applications.
- **Monospace Integration:** Any element representing data—such as IP addresses, ports, or TOML/INI config snippets—must use the `code-block` style to ensure character alignment.
- **Language Support:** When rendering Chinese characters, ensure the fallback stack includes *Microsoft YaHei UI* or *PingFang SC* to maintain visual weight parity with the Latin characters.

## Layout & Spacing

The layout follows a **fixed-fluid hybrid model** characteristic of desktop applications. It uses a **4px base grid** for all internal spacing.

- **Structural Layout:** A fixed-width sidebar (212px) on the left provides primary navigation. The main content area is fluid, expanding to fill the remaining horizontal space.
- **Rhythm:** Internal control padding uses 8px (`tight`). Spacing between distinct form fields or settings blocks uses 12px (`md`). Page margins are strictly set to 24px (`page-margin`) to provide breathing room.
- **Desktop Focus:** This system does not use a traditional 12-column web grid. Instead, it relies on content-responsive containers with a maximum width for readability (typically 1200px) centered within the fluid main panel.

## Elevation & Depth

This design system conveys hierarchy through **Tonal Layers** and **Low-Contrast Outlines**.

- **Surface Tiers:** The `bg.app` (#F6F8FA) acts as the foundation. Content is placed on `bg.surface` (#FFFFFF) panels. This creates a natural "stacked" look without the need for shadows.
- **Shadows:** Avoid heavy, dark shadows. Use only a subtle 1px ambient shadow (`0 1px 2px rgba(15, 23, 42, 0.08)`) for primary panels to lift them slightly from the background.
- **Interactions:** Popovers and context menus use a more pronounced shadow to indicate temporary overlay status, but should maintain a high blur and low opacity to remain clean.
- **Borders:** All interactive elements (inputs, buttons, cards) must have a 1px border. In Light mode, this border is slightly darker than the surface; in Dark mode, it is used to define the element's edge against the deep background.

## Shapes

The shape language is modern and refined, following the Windows 11 Fluent aesthetic. 

- **Standard Elements:** Buttons, inputs, and chips use `rounded` (8px / 0.5rem) to provide a soft, professional feel.
- **Large Containers:** Main content cards and dialogs also use `rounded` (8px), which is the system's ceiling. 
- **Strictness:** Do not use pill-shaped (fully rounded) buttons; maintain the 8px radius to preserve the "professional tool" identity.

## Components

- **Buttons:** Primary buttons use `brand.primary` with white text. Secondary buttons use a transparent background with a `border.default`. Heights are strictly 36px (Primary) or 32px (Secondary).
- **Input Fields:** Use 1px borders with a subtle bottom-heavy emphasis on focus. Height should be 34px. 
- **Status Badges:** Small, 12px text capsules with low-saturation background tints from the status palette (e.g., light green background for success).
- **Navigation View:** The sidebar should use a "selected" indicator—a 3px vertical blue bar on the left edge of the active menu item.
- **Info Bars:** Used for global system notifications (e.g., "FRP Client Started"). These should span the top of the content area with a background color corresponding to the status type.
- **Data Grids:** Tables for proxy lists should use 40px row heights with 1px horizontal dividers only. No vertical dividers between columns to maintain a clean, modern look.
- **Monospace Logs:** A dedicated terminal-style component for FRP logs, using a dark background (#0B1117) regardless of the global theme.