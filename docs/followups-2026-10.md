# Follow-ups after the CI gate (Oct 2026)

Work log for the items found during the CI-gate / test-coverage work (details in
[superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md](superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md), "Open questions").
Each item: own branch → tests that fail first → merge to `staging` → user checks → PR to `main` (user merges).

## Decisions (user, 2026-10-08)

- Share links: cap expiry at **365 days** (default stays 30).
- Free users holding add-on credits: **may spend them** (Free-plan limits like story length still apply).
- Avatars: **built-in presets only**; also **add more preset avatars**.
- Prompt fallback (decided after review): **no fallback prompts**. Retry the scene-writing call patiently (4 attempts, 1s/2s/4s); if it still fails, the story fails and is refunded. The forest keyword fallback drew unrelated pictures.
- Avatars: user chose all four new categories (animals, fantasy, everyday heroes, diverse kids), ~15 total, redo existing in one style; generate with the user's Higgsfield account after comparing model pricing.

## Order and status

| # | Item | Kind | Status |
|---|------|------|--------|
| 1 | Webhook retry can credit add-ons twice (EF execution strategy retries without clearing tracked changes) | billing | done (fix/webhook-retry-double-credit) |
| 2 | Add-on / invoice webhooks wipe a scheduled cancellation (`CancelAtUtc` set unconditionally) | billing | done (fix/webhook-keep-cancel-date) |
| 3 | Client disconnect can strand a reserved credit (`Start` passes request token to `Task.Run`) | credits | done (fix/start-job-ignores-disconnect) |
| 4 | Two tabs / double click can spend quota twice (no concurrency guard on reserve) | credits | done (fix/atomic-credit-reservation); also fixed: a refund after a failed story overwrote plan upgrades and credit purchases made during generation |
| 5 | Let Free users spend add-on credits | product | done (feat/free-users-spend-addons) |
| 6 | Cap share-link expiry at 365 days | product | done (fix/share-expiry-cap) |
| 7 | Avatars: presets only; 15 new storybook animal/fantasy avatars (GPT Image 2.5 via Higgsfield, ~33 credits), ~80 KB each | product | done (feat/avatar-presets-only, feat/new-avatars) |
| 8 | Prompt fallback: patient retries, no off-topic fallback | product | done (fix/prompt-retries-no-fallback) |
| 9 | Image prompting / reference-image experiments | exploration | todo |
| 10 | Deferred CI/test minors (workflow permissions, pin mssql image, job timeouts, refund-on-upload-failure test, etc.) | hygiene | done (chore/ci-test-hygiene) |
