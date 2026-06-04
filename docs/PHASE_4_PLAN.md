# Phase 4 Plan

Phase 4 refines FrpNexus into a preview-ready Windows desktop tool. It begins after Phase 3 remote operations are committed and documented.

## Goals

Phase 4 should polish the existing MVP workflow rather than expand the product into new platforms or enterprise features.

1. Produce a repeatable Windows x64 release package.
2. Improve reliability for local persistence and remote operation workflows.
3. Add safe import, export, and backup workflows.
4. Skip keyboard navigation and accessibility for the current MVP by user decision.
5. Make supported theme settings effective after the default WinUI 3 / Fluent style remains stable.

## Non-Negotiable Boundaries

- Do not store SSH passwords, tokens, private key contents, or private key passphrases.
- Do not log SSH passwords, tokens, private key contents, private key passphrases, or secret-bearing command lines.
- Do not package user SQLite databases, logs, SSH credentials, private keys, or FRP caches.
- Do not make INI the MVP default configuration format.
- Do not build Agent mode, cloud sync, web dashboard, billing, Kubernetes, or multi-tenant backend features.

## Recommended Sequence

### Phase 4 First Iteration: Packaging Foundation

- Update phase and Todo documents.
- Add Desktop release metadata.
- Add a Windows x64 folder-based publish script.
- Document release command, output layout, manual upgrade, and release checks.
- Keep installer, MSIX, automatic updates, and single-file publishing out of this iteration.

### Phase 4 Second Iteration: Reliability Audit

- Review existing local persistence and remote operation flows.
- Improve user-facing Chinese error states for recoverable failures.
- Review cancellation, timeout, double-click, retry, and empty-state behavior.
- Keep tests offline and deterministic.

### Phase 4 Third Iteration: Import, Export, And Backup

- Export safe local records such as settings, nodes, tunnels, configuration versions, runtime records, and deployment records.
- Import safe local records with validation and user confirmation.
- Provide a backup path for SQLite data without including secrets.
- Keep SSH passwords, tokens, private key contents, and passphrases out of imports and exports.

### Phase 4 Fourth Iteration: Skipped By User Decision

- Do not implement shortcuts, keyboard navigation polish, AutomationProperties, or screen reader-specific polish in the current MVP.
- Keep this as a documented skip so future work can revisit it intentionally.

### Phase 4 Fifth Iteration: Theme And UI Polish

- Make supported theme settings effective for `Light`, `Dark`, and `System`.
- Keep the default WinUI 3 / Fluent visual system as the baseline.
- Evaluate optional themes only after the default light style is stable.

## Testing Strategy

- Run `dotnet build`.
- Run full `dotnet test`.
- Run packaging scripts on Windows.
- Inspect release output for expected executable files.
- Confirm release output excludes tests, user databases, logs, credentials, private keys, and FRP caches.
- Add or update tests when reliability, import/export, or ViewModel behavior changes.

## Phase 4 Entry Gate

Before implementation:

- Phase 3 is committed and documented in `docs/PHASE_3_SUMMARY.md`.
- `docs/CURRENT_PHASE.md` is updated to Phase 4 after explicit user confirmation.
- `docs/PROJECT_TODO.md` has Phase 4 iteration Todo entries.
