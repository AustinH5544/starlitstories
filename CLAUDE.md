# CLAUDE.md

@AGENTS.md

## Claude-specific rules

- **Git: `staging` is yours, `main` is the user's.**
  - Read-only git commands (`status`, `log`, `diff`, `branch`, `show`) are fine anytime.
  - You may switch to `staging`, commit, pull, merge into it, and push it. Pushing `staging` deploys the staging environment, so say when you do.
  - Never commit, merge, rebase, reset, or push while `main` is checked out. Never push to, merge into, delete, or force-push `main`. Never force-push any branch. Promoting `staging` to `main` is done by the user.
  - These rules are enforced by a hook (`.claude/hooks/protect_main.py`). If it blocks something, stop and ask; don't work around it.
- This is a production repo: a push to `main` deploys straight to prod. Don't change code, config, or migrations unless asked, and call out anything that touches billing, auth, webhooks, or the database.
- Work logs for in-progress efforts live in `docs/` (e.g. [docs/stripe-test-mode-notes.md](docs/stripe-test-mode-notes.md)), not in this file.
