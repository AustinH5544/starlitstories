# CLAUDE.md

@AGENTS.md

## Claude-specific rules

- **Git: `staging` is yours, `main` is the user's.**
  - Read-only git commands (`status`, `log`, `diff`, `branch`, `show`) are fine anytime.
  - You may switch to `staging`, commit, pull, merge into it, and push it. Pushing `staging` deploys the staging environment, so say when you do.
  - Never commit, merge, rebase, reset, or push while `main` is checked out. Never push to, merge into, delete, or force-push `main`. Never force-push any branch.
  - **Promoting to prod goes through a pull request.** When `staging` is verified, you may open a PR from `staging` into `main` with `gh pr create --base main --head staging`, and update its description or comment on it. The user reviews and merges it; merging deploys prod. Tell the user to merge with **"Create a merge commit"** (not squash or rebase, which make `main` and `staging` diverge) and not to delete the `staging` branch afterwards. Never merge a PR yourself (no `gh pr merge`, no `--auto`, no merge calls through `gh api`).
  - PR description: what changed and why, tests and checks run (with CI links), what was verified on staging, and explicit callouts for billing, auth, webhooks, database, or config impact. End with anything the user should check before merging.
  - These rules are enforced by a hook (`.claude/hooks/protect_main.py`). If it blocks something, stop and ask; don't work around it.
- This is a production repo: a push to `main` deploys straight to prod. Don't change code, config, or migrations unless asked, and call out anything that touches billing, auth, webhooks, or the database.
- Work logs for in-progress efforts live in `docs/` (e.g. [docs/stripe-test-mode-notes.md](docs/stripe-test-mode-notes.md)), not in this file.
