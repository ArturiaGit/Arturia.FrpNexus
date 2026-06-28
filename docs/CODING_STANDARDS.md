# FrpNexus Coding Standards

These standards define the default engineering rules for FrpNexus implementation work. They do not replace the product phase constraints in `docs/CURRENT_PHASE.md`; if a requested implementation conflicts with the active phase, the phase constraint wins.

## Architecture Boundaries

FrpNexus uses a layered .NET desktop architecture.

- `Core` contains domain models, enums, value objects, and pure business types. It must not depend on UI, infrastructure, dependency injection, or logging frameworks.
- `Application` contains service interfaces, application-level contracts, use-case orchestration, and DTOs. It may depend on `Core`, but must not depend on `Desktop`.
- `Infrastructure` contains concrete implementations for SSH, SFTP, file system access, process execution, persistence, release downloads, and other external systems.
- `Desktop` contains Avalonia views, view models, converters, controls, styles, desktop composition, and UI-specific adapters.

Dependency direction must stay inward:

- `Desktop` may depend on `Application` and `Core`.
- `Infrastructure` may depend on `Application` and `Core`.
- `Application` may depend on `Core`.
- `Core` must stay independent.

## Services And Dependency Injection

All application services must be defined behind interfaces and resolved through dependency injection.

- Use `Microsoft.Extensions.DependencyInjection` as the DI container.
- Keep service abstractions in `Application` unless they are truly UI-only.
- Keep concrete external-system implementations in `Infrastructure`.
- Keep desktop-only adapters and registrations in `Desktop`.
- Establish a clear composition root in the desktop project, such as `Composition/DesktopCompositionRoot.cs`.
- View models must depend on service interfaces, not concrete implementations.
- Do not manually `new` concrete services inside view models, views, or other services when DI can provide them.
- Prefer focused services with a clear responsibility over broad manager classes.

Suggested lifetimes:

- Stateless services: `Singleton`.
- Page view models: usually `Transient`.
- Runtime session objects: explicit lifetime management, or `Scoped` only when a real scope exists.

## Avalonia And MVVM

Avalonia UI code must remain MVVM-friendly.

- Use `CommunityToolkit.Mvvm` for observable state and commands.
- View models should inherit from a shared base view model.
- Prefer `[ObservableProperty]`, `RelayCommand`, and `AsyncRelayCommand` over hand-written boilerplate.
- Code-behind should only contain Avalonia initialization and unavoidable view-specific glue.
- Business logic, command logic, service calls, and UI state transitions belong in view models or services.
- SSH, SFTP, remote process, download, and file operations must not be implemented directly in views.

## Reusable UI, Styles, And Resources

Reusable UI must be extracted deliberately instead of duplicated across pages.

- Common controls belong under `Controls`, or a feature folder when they are feature-specific.
- Common styles and resource dictionaries belong under `Styles`, `Themes`, or `Resources`.
- Group resources by responsibility, such as colors, typography, controls, icons, and page-specific styles.
- Value converters belong under `Converters`, or a feature folder when they are feature-specific.
- Behaviors, attached properties, and interaction helpers belong under dedicated folders such as `Behaviors`, `AttachedProperties`, or a clearly named UI infrastructure folder.
- Repeated UI patterns such as status badges, empty states, command bars, log rows, metric tiles, and page sections should become reusable components or styles once they appear in more than one page.

Avoid:

- Adding converters, controls, behaviors, or reusable resources directly to the project root.
- Duplicating the same visual structure across pages.
- Creating one large catch-all converter, style, or control file for unrelated behavior.
- Mixing page-specific helpers with global UI infrastructure.

## Logging

Use `Serilog` as the logging framework.

- Console logging must output `Information` and above.
- Local file logging must record `Warning` and above.
- Prefer structured logging with named properties instead of string concatenation.
- Write local logs under the user-local application data area, for example `%LocalAppData%/Arturia/FrpNexus/logs/`.
- Services should log technical details that help diagnose failures.
- View models should convert failures into Chinese user-facing UI states or messages.

Never log:

- SSH passwords.
- Tokens.
- Private key contents.
- Full sensitive connection strings.
- Secret environment variables.

## Async, Cancellation, And Long-Running Work

Operations that can block must be asynchronous.

- SSH connection tests, SFTP transfers, release downloads, remote commands, log streaming, TOML generation involving I/O, and process control should use async APIs.
- Long-running operations must accept and honor `CancellationToken`.
- View model commands for async work should use `AsyncRelayCommand`.
- Do not block the UI thread with `.Result`, `.Wait()`, or synchronous waits around async work.
- Report progress through view model state, not direct UI manipulation from services.

## Error Handling

Keep error responsibilities separated.

- Services should return clear result types or throw application-recognizable exceptions.
- Services must not directly show dialogs, info bars, or other UI.
- View models translate service failures into Chinese UI messages, status badges, and recoverable states.
- User-facing errors should be concise and actionable.
- Technical details should go to logs, not raw UI messages.

## Local Persistence

Local structured data must use SQLite by default.

- Do not use LiteDB as the default local database.
- LiteDB may only be considered later for clearly document-shaped, relationship-free, migration-light data that is isolated from the main application database.
- Nodes, tunnels, configuration versions, runtime records, deployment records, settings, and similar structured data must be accessed through `Application` layer interfaces.
- SQLite concrete implementations belong in the `Infrastructure` layer.
- `Desktop`, views, and view models must not directly access SQLite connections, connection strings, database file paths, or SQL statements.
- Logs remain the responsibility of Serilog file logging and must not be written to SQLite by default.
- Sensitive data such as tokens, passwords, private key contents, and secret connection strings must not be stored in SQLite as plain text.
- Prefer `Microsoft.Data.Sqlite` or Dapper for initial SQLite implementation. Consider EF Core SQLite only when schema migration and relationship complexity justify it.

## Configuration And Secrets

Configuration and secrets need explicit handling.

- Ordinary settings should go through configuration service abstractions.
- Sensitive values must not be written to ordinary plain-text configuration files without an explicit product decision.
- Tokens, passwords, and private keys must not appear in logs, test snapshots, or sample data.
- Private key paths may be stored when needed, but private key contents require a dedicated security strategy.

## Testing Standards

Tests should focus on behavior and boundaries.

- Prioritize unit tests for `Core` models and `Application` service orchestration.
- Test view model navigation, command availability, sample states, validation, and error-state transitions.
- Mock service interfaces instead of calling real SSH/SFTP servers in unit tests.
- Add integration tests only when the external dependency can be controlled or safely simulated.
- UI visual details do not require unit tests, but the view model behavior that drives them should be testable.

## Repository Hygiene

Keep the repository clean and intentional.

- `bin/`, `obj/`, test output, IDE caches, and generated build artifacts should not be committed.
- Follow the existing folder structure before adding a new folder or abstraction.
- Prefer small, focused files over broad utility buckets.
- Do not introduce a new package unless it supports an established architectural need.
- When adding a package, keep package references in the project that actually owns the dependency.
- Avoid unrelated refactors while implementing a focused feature.
