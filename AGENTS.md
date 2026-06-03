# FrpNexus Agent Instructions

This repository contains the product and UI design source of truth for FrpNexus.

Before implementing any feature, Codex must read:

1. `docs/PRODUCT.md`
2. `docs/CURRENT_PHASE.md`

Before implementing or modifying any UI, Codex must also read:

1. `docs/UIUX.md`
2. `docs/AVALONIA_UI_CONSTRAINTS.md`
3. `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md`

## Hard Rules

- Build FrpNexus as an Avalonia desktop application, not a web application.
- Use Avalonia XAML, styles, resource dictionaries, views, and MVVM-friendly view models.
- Do not implement UI by rendering HTML, CSS, Tailwind, or WebView.
- Treat Stitch `code.html` files as layout and text references only.
- Treat Stitch `screen.png` files as visual references for spacing, hierarchy, and composition.
- The UI must be Chinese-first. Technical terms such as `FRP`, `SSH`, `SFTP`, `TOML`, `TCP`, `UDP`, `HTTP`, `HTTPS`, `Token`, `frpc`, and `frps` may remain in English.
- The visual style must follow WinUI 3 / Fluent Design for a Windows 11-style desktop tool.
- The app must adapt between `1100 x 720` and `1280 x 800`; `1280 x 800` is the maximum design target.
- Do not create marketing pages, landing pages, SaaS dashboards, or Material Design-style web admin screens.
- Stay within the current phase in `docs/CURRENT_PHASE.md`.

## Gitflow Rules

Before making any repository-tracked code or documentation change, Codex must inspect the current Git branch and working tree state when a Git repository exists.

Gitflow guidance is defined in `docs/GITFLOW.md` and uses the standard `main` / `develop` model:

- `main` is the stable release branch.
- `develop` is the daily integration branch.
- `feature/<scope>` branches start from `develop` and merge back into `develop`.
- `release/<version>` branches start from `develop` and merge back into both `main` and `develop`.
- `hotfix/<scope>` branches start from `main` and merge back into both `main` and `develop`.

Codex may read Git status, inspect diffs, and suggest branch names, but must not create branches, commit, merge, push, or tag without explicit user approval.

When the user explicitly approves a commit, Codex must write the Git commit message in Chinese by default. Technical terms such as `FRP`, `SSH`, `SFTP`, `TOML`, `Avalonia`, branch names, file names, and version numbers may remain in English.

If this directory is not initialized as a Git repository, Codex must not initialize Git or create `main` / `develop` branches unless the user explicitly requests it.

## Source Priority

When sources conflict, follow this order:

1. `docs/CURRENT_PHASE.md`
2. `docs/AVALONIA_UI_CONSTRAINTS.md`
3. `docs/UIUX.md`
4. `docs/PRODUCT.md`
5. `FrpNexus_UIUX_Design_Brief_v1.0.md`
6. `FrpNexus_Product_Plan_v1.0.md`
7. Stitch HTML and screenshots
