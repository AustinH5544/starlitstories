# Follow-ups after the CI gate (Oct 2026)

Work log for the items found during the CI-gate / test-coverage work (details in
[superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md](superpowers/specs/2026-10-07-ci-gate-and-test-coverage-design.md), "Open questions").
Each item: own branch → tests that fail first → merge to `staging` → user checks → PR to `main` (user merges).

## Decisions (user, 2026-10-08)

- Share links: cap expiry at **365 days** (default stays 30).
- Free users holding add-on credits: **may spend them** (Free-plan limits like story length still apply).
- Avatars: **built-in presets only**; also **add more preset avatars**.
- Prompt fallback: **undecided**. User wants to see what the fallback prompts actually produce first; cancelling may be better than an unrelated image.

## Order and status

| # | Item | Kind | Status |
|---|------|------|--------|
| 1 | Webhook retry can credit add-ons twice (EF execution strategy retries without clearing tracked changes) | billing | done (fix/webhook-retry-double-credit) |
| 2 | Add-on / invoice webhooks wipe a scheduled cancellation (`CancelAtUtc` set unconditionally) | billing | done (fix/webhook-keep-cancel-date) |
| 3 | Client disconnect can strand a reserved credit (`Start` passes request token to `Task.Run`) | credits | done (fix/start-job-ignores-disconnect) |
| 4 | Two tabs / double click can spend quota twice (no concurrency guard on reserve) | credits | done (fix/atomic-credit-reservation); also fixed: a refund after a failed story overwrote plan upgrades and credit purchases made during generation |
| 5 | Let Free users spend add-on credits | product | todo |
| 6 | Cap share-link expiry at 365 days | product | todo |
| 7 | Avatars: presets only; add more presets | product | todo |
| 8 | Prompt fallback: review fallback prompts with user, then fix or remove | product | todo (needs user) |
| 9 | Image prompting / reference-image experiments | exploration | todo |
| 10 | Deferred CI/test minors (workflow permissions, pin mssql image, job timeouts, refund-on-upload-failure test, etc.) | hygiene | todo |
