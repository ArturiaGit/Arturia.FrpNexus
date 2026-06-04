# FrpNexus Project Todo

This document turns the product plan, current phase, and Stitch UI direction into executable Todo items. It is a coordination document for Codex and the user, not a substitute for `docs/CURRENT_PHASE.md`.

Current phase: `Phase 2: SQLite Local Persistence + Settings Persistence`.

## Todo Status

Use these markers:

- `[ ]` not started.
- `[~]` in progress.
- `[x]` completed.
- `[blocked]` blocked by missing decision, dependency, or explicit user approval.

## Codex Reporting Rule

After completing each Todo item, Codex must report to the user:

- Completed Todo item.
- Files changed.
- Verification performed.
- Next recommended Todo item.

Suggested report format:

```text
完成 Todo：<item>
涉及文件：<files>
验证方式：<checks>
下一步建议：<next item>
```

If a Todo requires Git branch creation, commit, merge, push, or tag, Codex must follow `docs/GITFLOW.md` and ask for explicit approval first.

## Phase 1 Todo

Phase 1 focuses on documentation, Avalonia skeleton, static UI pages based on Stitch, placeholder view models, and future service interfaces. It must not implement real remote orchestration yet.

### Foundation

- [x] Confirm current Git branch and working tree before repository-tracked changes.
- [x] Read required project documents: `docs/PRODUCT.md`, `docs/CURRENT_PHASE.md`, `docs/CODING_STANDARDS.md`, `docs/PROJECT_TODO.md`.
- [x] For UI work, read `docs/UIUX.md`, `docs/AVALONIA_UI_CONSTRAINTS.md`, `docs/UI_STYLE_GUIDE.md`, `docs/UI_LAYOUT_GUIDE.md`, `docs/STITCH_IMPLEMENTATION_GUIDE.md`, and Stitch source files.
- [x] Maintain Avalonia project skeleton and solution structure.
- [x] Keep layer boundaries aligned with `Core`, `Application`, `Infrastructure`, and `Desktop`.
- [x] Keep Desktop composition root using `Microsoft.Extensions.DependencyInjection`.

Acceptance:

- Required docs are respected.
- No phase-forbidden feature is implemented.
- Project structure remains intentional and buildable.

### Design Tokens And Shared UI

- [x] Create or maintain shared design token resources for colors, typography, spacing, radius, borders, and sizing.
- [x] Ensure default SideNav style is light Fluent NavigationView-style.
- [x] Reserve dark resources for logs, TOML previews, terminals, and code panels.
- [x] Extract reusable status badge styling.
- [x] Extract reusable command bar, panel/card, table/list, and technical panel styles when they appear across pages.
- [x] Keep converters, controls, behaviors, styles, themes, and resources in dedicated folders.

Acceptance:

- Reusable visuals are not duplicated page by page.
- `#111827` is not used as the default main navigation background.
- Shared resources follow `docs/UI_STYLE_GUIDE.md`.

### Shell

- [x] Implement main shell layout with fixed `212px` SideNav.
- [x] Implement fixed `52px` TopBar.
- [x] Implement page content host with `24px` page margin.
- [x] Add Chinese navigation items: 仪表盘, 节点, 隧道, 配置, 运行, 日志, 设置.
- [x] Add active, hover, disabled, and normal navigation states.
- [x] Add connection status area and compact top-right action buttons.

Acceptance:

- Shell fits from `1100 x 720` to `1280 x 800`.
- Navigation remains Chinese-first.
- Shell is Avalonia XAML and MVVM-friendly.

### Dashboard Page

- [x] Implement static Dashboard page from `frpnexus_1`.
- [x] Add four metric tiles.
- [x] Add quick actions panel.
- [x] Add recent node status table.
- [x] Add recent errors panel.
- [x] Add dark system log preview.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- Layout follows `docs/UI_LAYOUT_GUIDE.md`.
- Sample state includes online, offline, running, warning, and error examples.

### Nodes Page

- [x] Implement static Nodes page from `frpnexus_2`.
- [x] Add toolbar with add, refresh, and search.
- [x] Add node table with status and FRP service columns.
- [x] Add selected node details panel around `320px`.
- [x] Add SSH info, FRP info, and quick actions.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- List plus details layout is preserved.
- Technical values use monospace styling.

### Tunnels Page

- [x] Implement static Tunnels page from `frpnexus_3`.
- [x] Add command bar with search and filters.
- [x] Add create tunnel primary action.
- [x] Add tunnel table with protocol, endpoint, node, status, and operations.
- [x] Show TCP, UDP, HTTP, and HTTPS examples.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- Tunnels are readable in the target window range.
- Protocol and status badges follow shared styles.

### Configurations Page

- [x] Implement static Configurations page from `frpnexus_4`.
- [x] Add left-side grouped configuration form.
- [x] Add right-side TOML preview panel.
- [x] Add generate, validate, save, and upload placeholder commands.
- [x] Use TOML as the primary configuration direction.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- TOML preview uses dark technical panel style.
- Configuration UI does not prioritize INI.

### Runtime Page

- [x] Implement static Runtime page derived from the same visual system.
- [x] Add process list or runtime status table.
- [x] Add start, stop, restart, and refresh placeholder commands.
- [x] Add selected runtime detail panel or technical output area.
- [x] Show running, stopped, warning, error, and unknown examples.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- Runtime remains static UI in Phase 1.
- No real remote process control is implemented yet.

### Logs Page

- [x] Implement static Logs page from `frpnexus_5`.
- [x] Add search, node, process, and level filters.
- [x] Add auto-refresh toggle.
- [x] Add copy and clear placeholder actions.
- [x] Add full-height dark terminal log panel.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- Terminal panel fills available height.
- Log rows preserve timestamp and level alignment.

### Settings Page

- [x] Implement static Settings page from `frpnexus_6`.
- [x] Add centered grouped settings cards.
- [x] Add local paths, logging, theme/style, update, and advanced placeholder settings where appropriate.
- [x] Keep sensitive settings display safe and non-revealing.
- [x] Back the page with placeholder ViewModel data.

Acceptance:

- Settings use grouped rows, not dashboard cards.
- Sensitive values are not shown in sample data.

### Application Interfaces And Placeholders

- [x] Add service interfaces for node management, SSH connection testing, SFTP upload, FRP release lookup/download, TOML generation, runtime control, log reading, and settings.
- [x] Keep concrete implementations as placeholders or fakes when needed in Phase 1.
- [x] Ensure view models depend on interfaces, not concrete services.
- [x] Ensure async-capable operations expose `CancellationToken` in future-facing contracts.

Acceptance:

- Interfaces prepare the MVP direction without implementing real remote orchestration prematurely.
- Dependencies are resolved through DI.

### Verification

- [x] Run `dotnet restore` when package changes require it.
- [x] Run `dotnet build` after code/XAML changes.
- [x] Run relevant tests after ViewModel, Core, or Application behavior changes.
- [x] For UI layout work, visually inspect the app at `1100 x 720` and `1280 x 800` when feasible.
- [x] Report each completed Todo using the reporting rule above.

Acceptance:

- Verification result is included in the user update.
- Any skipped verification is explicitly explained.

## Phase 2 Route Todo

Phase 2 focuses on local SQLite persistence and local MVP behavior after the UI skeleton is stable. Real SSH, SFTP, FRP download, remote runtime control, and remote log streaming are intentionally deferred because `docs/CURRENT_PHASE.md` prohibits those capabilities in the active phase.

- [x] Implement SQLite local persistence through `Application` interfaces and `Infrastructure` implementations.
- [x] Implement settings persistence.
- [x] Implement configuration version persistence.
- [x] Implement runtime record persistence.
- [x] Implement deployment record persistence.
- [x] Implement TOML generation and validation workflow.
- [x] Add unit tests for Core, Application services, and ViewModel behavior.

### Phase 2 First Iteration Todo

- [x] Add `Arturia.FrpNexus.Infrastructure` project to the solution.
- [x] Add SQLite dependency using `Microsoft.Data.Sqlite`.
- [x] Add database path provider using `%LocalAppData%/Arturia/FrpNexus/data/frpnexus.db`.
- [x] Add SQLite connection factory and schema initializer.
- [x] Implement `ISettingsService` through Infrastructure.
- [x] Register Infrastructure services through Desktop DI.
- [x] Keep Desktop, views, and view models from directly accessing SQLite.
- [x] Add tests for database path, first-run defaults, settings save/read, safe setting fields, and DI.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Second Iteration Todo

- [x] Wire Settings UI to SQLite persistence through `ISettingsService`.
- [x] Bind Settings page controls to `SettingsPageViewModel` state instead of static XAML values.
- [x] Bind `保存应用` to an async save command.
- [x] Keep path picker, directory open, key import, and cache cleanup actions as placeholders.
- [x] Add tests for loading settings, saving ordinary settings, sensitive-field boundaries, and DI resolution.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Third Iteration Todo

- [x] Add SQLite node persistence through `INodeManagementService`.
- [x] Add `nodes` table for ordinary node profile fields.
- [x] Keep SSH passwords, tokens, and private key contents out of node persistence.
- [x] Register SQLite node management through Infrastructure DI.
- [x] Load Nodes page data from `INodeManagementService` instead of static XAML-era sample data.
- [x] Seed safe sample nodes only when the local node database is empty.
- [x] Add Infrastructure tests for node table initialization, save/read, delete, and sensitive-field boundaries.
- [x] Add ViewModel tests for node loading and empty-database sample state.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Fourth Iteration Todo

- [x] Add SQLite tunnel persistence through `ITunnelManagementService`.
- [x] Add `tunnels` table for ordinary tunnel profile fields.
- [x] Register SQLite tunnel management through Infrastructure DI.
- [x] Load Tunnels page data from `ITunnelManagementService` instead of static sample data.
- [x] Seed safe TCP, UDP, HTTP, and HTTPS examples only when the local tunnel database is empty.
- [x] Keep create, edit, delete, validation, remote check, and deployment actions as placeholders.
- [x] Add Infrastructure tests for tunnel table initialization, save/read, delete, and MVP protocols.
- [x] Add ViewModel tests for tunnel loading and empty-database sample state.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Fifth Iteration Todo

- [x] Add local create, edit, and delete commands for Tunnels page.
- [x] Keep tunnel CRUD limited to SQLite local records through `ITunnelManagementService`.
- [x] Add a MVVM-friendly inline editor panel for local tunnel records.
- [x] Validate required tunnel fields and local port range before saving.
- [x] Require local delete confirmation before removing a tunnel record.
- [x] Keep remote port checks, TOML generation, upload, and deployment out of this iteration.
- [x] Add ViewModel tests for create, edit, delete, validation, and MVP protocols.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Sixth Iteration Todo

- [x] Add local create, edit, and delete commands for Nodes page.
- [x] Keep node CRUD limited to SQLite local records through `INodeManagementService`.
- [x] Add a MVVM-friendly inline editor panel for local node records.
- [x] Validate required node fields and SSH port range before saving.
- [x] Require local delete confirmation before removing a node record.
- [x] Keep SSH connection testing, SFTP upload, FRP download, and remote process actions out of this iteration.
- [x] Keep SSH passwords, tokens, and private key contents out of node form and persistence models.
- [x] Add ViewModel tests for create, edit, delete, validation, and sensitive-field boundaries.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Seventh Iteration Todo

- [x] Add a local TOML generation service through `ITomlConfigurationService`.
- [x] Generate FRP proxy TOML for TCP, UDP, HTTP, and HTTPS without remote side effects.
- [x] Validate generated TOML locally with Chinese user-facing error messages.
- [x] Wire Configurations page fields to `ConfigurationsPageViewModel` state.
- [x] Bind `生成 TOML` and `验证语法` to MVVM commands.
- [x] Keep upload, remote validation, and deployment actions out of this iteration.
- [x] Add Application tests for TOML generation and validation.
- [x] Add ViewModel tests for generation, validation, and invalid input feedback.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Eighth Iteration Todo

- [x] Add configuration version persistence through `IConfigurationVersionService`.
- [x] Add `configuration_versions` table for ordinary configuration metadata and TOML content.
- [x] Register SQLite configuration version management through Infrastructure DI.
- [x] Wire Configurations page `保存配置` to local SQLite persistence.
- [x] Load saved local configuration versions into the Configurations page.
- [x] Keep upload, remote validation, SSH, SFTP, FRP download, and deployment actions out of this iteration.
- [x] Keep SSH passwords, tokens, and private key contents out of configuration version models.
- [x] Add Infrastructure tests for configuration version table initialization, save/read, delete, and sensitive-field boundaries.
- [x] Add ViewModel tests for saving and loading local configuration versions.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Ninth Iteration Todo

- [x] Add runtime record persistence through `IRuntimeRecordService`.
- [x] Add `runtime_processes` table for ordinary runtime process snapshot fields.
- [x] Register SQLite runtime record management through Infrastructure DI.
- [x] Load Runtime page process records from `IRuntimeRecordService` instead of static-only sample data.
- [x] Seed safe sample runtime records only when the local runtime database is empty.
- [x] Keep start, stop, restart, remote refresh, SSH, SFTP, FRP download, and log streaming out of this iteration.
- [x] Keep SSH passwords, tokens, and private key contents out of runtime record models.
- [x] Add Infrastructure tests for runtime table initialization, save/read, delete, and sensitive-field boundaries.
- [x] Add ViewModel tests for runtime loading and empty-database sample state.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Tenth Iteration Todo

- [x] Add deployment record persistence through `IDeploymentRecordService`.
- [x] Add `deployment_records` table for ordinary deployment step snapshot fields.
- [x] Register SQLite deployment record management through Infrastructure DI.
- [x] Load Runtime page deployment steps from `IDeploymentRecordService` instead of static-only sample data.
- [x] Seed safe sample deployment steps only when the local deployment database is empty.
- [x] Keep SSH, SFTP, FRP download, upload, remote start/stop/restart, and log streaming out of this iteration.
- [x] Keep SSH passwords, tokens, and private key contents out of deployment record models.
- [x] Add Infrastructure tests for deployment table initialization, save/read, delete, and sensitive-field boundaries.
- [x] Add ViewModel tests for deployment step loading and empty-database sample state.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

### Phase 2 Closure Todo

- [x] Document Phase 2 completed scope in `docs/PHASE_2_SUMMARY.md`.
- [x] Document Phase 2 remote-operation boundaries and Phase 3 handoff.
- [x] Keep Phase 2 closure aligned with `docs/CURRENT_PHASE.md`.
- [x] Run `dotnet build`.
- [x] Run full `dotnet test`.

## Phase 3 Route Todo

Phase 3 should focus on runtime operations and diagnostics. The detailed implementation sequence is defined in `docs/PHASE_3_PLAN.md`.

- [ ] Confirm Phase 3 entry gate in `docs/PHASE_3_PLAN.md`.
- [ ] Review and approve `docs/CREDENTIAL_STRATEGY.md` before real SSH work.
- [ ] Implement SSH connection testing service.
- [ ] Implement SFTP upload service.
- [ ] Implement FRP release download and binary selection.
- [ ] Implement remote start, stop, and restart.
- [ ] Implement remote process status detection.
- [ ] Implement remote log reading and streaming.
- [x] Add deployment records.
- [x] Add runtime records.
- [ ] Improve diagnostics and recoverable error states.
- [ ] Add integration-style tests around controlled service boundaries where feasible.

### Phase 3 First Iteration Todo

- [x] Commit Phase 2 local persistence closure before starting remote work.
- [ ] Update `docs/CURRENT_PHASE.md` to Phase 3 after explicit user confirmation.
- [ ] Confirm Phase 3 entry gate from `docs/PHASE_3_PLAN.md`.
- [ ] Review and approve `docs/CREDENTIAL_STRATEGY.md`.
- [ ] Review `ISshConnectionService`; replace `nodeName`-only input if real SSH requires credential context.
- [ ] Add a session-only credential input path that does not persist passwords, tokens, private key contents, or passphrases.
- [ ] Implement SSH connection testing in Infrastructure behind `ISshConnectionService`.
- [ ] Wire Nodes page `测试连接` to an async ViewModel command.
- [ ] Persist only safe connection result metadata, such as status and timestamp.
- [ ] Convert SSH failures into Chinese user-facing status text.
- [ ] Add unit tests with fake SSH adapters.
- [ ] Keep normal tests offline and deterministic.
- [ ] Add opt-in integration-test hooks only if explicitly configured.
- [ ] Verify sensitive fields are not stored in SQLite, logs, or test snapshots.
- [ ] Run `dotnet build`.
- [ ] Run full `dotnet test`.

## Phase 4 Route Todo

Phase 4 should refine packaging, reliability, and broader product polish.

- [ ] Add release packaging.
- [ ] Add update strategy if needed.
- [ ] Improve accessibility and keyboard navigation.
- [ ] Improve import/export and backup workflows.
- [ ] Consider legacy INI import/export only after TOML MVP is stable.
- [ ] Evaluate optional themes only after the default WinUI 3 / Fluent style is stable.

## Current Phase Prohibited Items

Do not implement in Phase 1:

- FrpNexus Agent.
- Agent API.
- Cloud sync.
- Team permission management.
- Web Dashboard.
- Billing.
- Kubernetes integration.
- Multi-tenant backend.
- Complex traffic analytics.
- Advanced audit logs.
- Real remote process orchestration beyond placeholders.
- Production credential storage without an explicit security design.
