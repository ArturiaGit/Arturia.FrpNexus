# FrpNexus Gitflow Constraints

FrpNexus uses documentation-level Gitflow constraints. These rules guide Codex and human contributors, but they do not add branch protection, hooks, or CI checks by themselves.

## Branch Model

Use the standard `main` / `develop` Gitflow model.

- `main`: Stable release branch. It should only receive merges from `release/<version>` or `hotfix/<scope>` branches.
- `develop`: Daily integration branch. All normal feature work starts from `develop` and merges back into `develop`.
- `feature/<scope>`: Feature branch. Create from `develop`, keep the scope short and descriptive, and merge back into `develop`.
- `release/<version>`: Release preparation branch. Create from `develop`, use it for release stabilization, and merge into both `main` and `develop`.
- `hotfix/<scope>`: Urgent production fix branch. Create from `main`, then merge into both `main` and `develop`.

## Codex Git Permissions

Codex may:

- Read the current branch.
- Read working tree status.
- Inspect diffs.
- Suggest branch names and Gitflow actions.

Codex must ask for explicit user approval before it:

- Creates or switches branches.
- Commits changes.
- Merges branches.
- Pushes to a remote.
- Creates tags.
- Initializes a Git repository.

## Commit Message Language

Git commit messages must be written in Chinese by default.

Allowed English terms include technical names, protocol names, file names, branch names, package names, command names, and version numbers, such as `FRP`, `SSH`, `SFTP`, `TOML`, `Avalonia`, `feature/ui-shell`, `dotnet build`, and `v1.0.0`.

Preferred commit message style:

- Use a short Chinese summary as the first line.
- Keep the first line focused on the actual change.
- If more detail is needed, add a blank line followed by concise Chinese body text.

Examples:

```text
添加 Gitflow 文档约束
```

```text
实现主窗口导航骨架

补充 Avalonia Shell 布局、中文导航项和页面占位 ViewModel。
```

## Current Repository State

If the working directory is not initialized as a Git repository, Codex must only maintain these documentation constraints. It must not run `git init`, create `main`, create `develop`, or configure remotes unless the user explicitly requests that action.

## Default Workflow For Future Work

For normal implementation work:

1. Confirm the repository is initialized.
2. Inspect the current branch and working tree state.
3. If branch changes are needed, ask the user before creating or switching branches.
4. Prefer `feature/<scope>` branches for ordinary work.
5. Keep commits focused on one feature, fix, or documentation change.
6. Run the relevant checks before suggesting a merge.

These rules do not override `docs/CURRENT_PHASE.md`. If Gitflow and phase constraints ever appear to conflict, phase constraints remain the product implementation authority, and Gitflow only controls repository workflow.
