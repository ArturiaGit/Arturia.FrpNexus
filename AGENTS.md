# FrpNexus Agent Instructions

This repository contains the product and UI design source of truth for FrpNexus.

Before implementing any feature, Codex must read:

1. `docs/PRODUCT.md`
2. `docs/CURRENT_PHASE.md`
3. `docs/CODING_STANDARDS.md`
4. `docs/PROJECT_TODO.md`

Before implementing or modifying any UI, Codex must also read:

1. `docs/UIUX.md`
2. `docs/AVALONIA_UI_CONSTRAINTS.md`
3. `docs/UI_STYLE_GUIDE.md`
4. `docs/UI_LAYOUT_GUIDE.md`
5. `docs/STITCH_IMPLEMENTATION_GUIDE.md`
6. `stitch_frpnexus_design_system/frpnexus_design_system/DESIGN.md`

## Hard Rules

- Build FrpNexus as an Avalonia desktop application, not a web application.
- Use Avalonia XAML, styles, resource dictionaries, views, and MVVM-friendly view models.
- Avalonia MVVM implementation must use the `CommunityToolkit.Mvvm` package for observable state and commands.
- Converters, custom controls, behaviors, resources, and reusable UI infrastructure must be placed in intentional feature or infrastructure folders, not casually added to the project root.
- Reusable controls, styles, and resources must be extracted into shared UI infrastructure instead of duplicated across pages.
- Application services must be defined behind interfaces and resolved through `Microsoft.Extensions.DependencyInjection`.
- Local structured data must use SQLite by default and be accessed through `Application` interfaces with `Infrastructure` implementations.
- Logging must use `Serilog`: console output records `Information` and above, while local file logs record `Warning` and above.
- Do not implement UI by rendering HTML, CSS, Tailwind, or WebView.
- Treat Stitch `code.html` files as layout and text references only.
- Treat Stitch `screen.png` files as visual references for spacing, hierarchy, and composition.
- The UI must be Chinese-first. Technical terms such as `FRP`, `SSH`, `SFTP`, `TOML`, `TCP`, `UDP`, `HTTP`, `HTTPS`, `Token`, `frpc`, and `frps` may remain in English.
- The visual style must follow WinUI 3 / Fluent Design for a Windows 11-style desktop tool.
- The app must adapt between `1100 x 720` and `1280 x 800`; `1280 x 800` is the maximum design target.
- Do not create marketing pages, landing pages, SaaS dashboards, or Material Design-style web admin screens.
- Stay within the current phase in `docs/CURRENT_PHASE.md`.
- Before implementation work, check `docs/PROJECT_TODO.md`; after completing each Todo step, report the completed item, files changed, verification performed, and next recommended step to the user.

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
3. `docs/UI_STYLE_GUIDE.md`
4. `docs/UI_LAYOUT_GUIDE.md`
5. `docs/STITCH_IMPLEMENTATION_GUIDE.md`
6. `docs/UIUX.md`
7. `docs/PRODUCT.md`
8. `FrpNexus_UIUX_Design_Brief_v1.0.md`
9. `FrpNexus_Product_Plan_v1.0.md`
10. Stitch HTML and screenshots
