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
| 11 | Stories interrupted by a restart/deploy keep the credit spent and leave a stuck draft | credits | done (fix/recover-interrupted-stories; adds migration AddStoryReservedFromAddOn) |

## Resume here (state at 2026-10-08, before compacting)

- **PR #68** (staging -> main: items 1-8, 10, new avatars incl. the sleepy-moon default): **merged 2026-10-08 as merge commit ada0d05**.
  After the merge: watch both prod runs (checks before deploy), then check `https://api.starlitstories.app/healthz`, `/readyz`, `/api/healthz`, `/api/config`.
- **Item 11 is committed on `fix/recover-interrupted-stories` (commit e0d2ce3), NOT merged to staging on purpose**, so it gets its own PR.
  After #68 merges: merge the branch into `staging`, push (deploys staging), watch CI, then open a staging -> main PR for the user.
  The PR must call out: billing-adjacent change, **migration `20261008120000_AddStoryReservedFromAddOn`** (adds `Stories.ReservedFromAddOn bit NOT NULL DEFAULT 0`, hand-written with snapshot update), and the first-run effect below.
- **Open decision (ask the user if not answered):** on its first prod run, recovery refunds every already-stuck draft as a plan credit
  (old drafts don't record which credit they used; drafts from earlier months effectively give a bonus story this month).
  Options: refund all (current behavior) or only drafts younger than 30 days (needs a small change + test).
- Local tooling notes: Docker Desktop must be running for the SqlServer test category (skipped locally without it; CI runs them).
  The Azure CLI login has expired (`az login` needed to read Key Vault / App Service settings or prod logs). The Higgsfield MCP has ~1,119 credits.
- The repo lives in OneDrive; a sync lock once broke `git switch` mid-way (recovered cleanly). Recommend moving it to e.g. `C:\dev\`.

## Next up (user priorities, 2026-10-08)

Item 9 (image prompting / reference images) folds into these. Each is product work: brainstorm and agree on a design with the user before coding.

1. **Make the story's lesson make sense.** `StoryGenerator` (around lines 65-110) asks for the lesson to be "woven in" and to end with a line starting `Lesson:`.
   Review real outputs for whether the lesson actually follows from the plot; improve the prompt (and possibly the `Lesson:` line format).
2. **Evaluate the text model.** Story text and scene prompts use `gpt-4.1-mini` (4 call sites). Compare current options on quality, cost, latency for children's stories;
   keep image quality at `low` (cost policy).
3. **Make saved characters more intuitive.** UX review of the saved-character flow in `StoryForm.jsx` (`normalizeSavedCharacter`, the saved list, save/delete) and `SavedCharacterController`.
4. **One character sheet for all characters, used everywhere.** Today one base portrait is generated per story (`BuildBaseCharacterPrompt`) and passed as the single
   reference to every page/cover edit (`GenerateImagesWithCharacterBaseAsync`). Idea: render every character in the story on one sheet and use it as the reference for all images
   (gpt-image-2 edits accept multiple `image[]` references; see the earlier research in this conversation's notes).
   Note: extra characters are currently disabled (`MembershipEntitlements.MaxCharactersPerStory = 1`, `showCharacterTypeAndExtraButton = false` in `StoryForm.jsx`), so this pairs with deciding whether to re-enable multiple characters.
5. **Painting flight game is too zoomed in on small screens** (user's Galaxy Z Fold cover screen; the game shows on the create page while a story generates).
   `components/PaintingFlightGame.jsx` sizes the canvas in CSS pixels with minimums (`Math.max(360, width)`, `Math.max(240, height)`) and uses fixed pixel
   sizes for the brush, obstacle width (74) and gaps (112-170), so on a ~344-360px-wide screen it overflows and everything looks oversized.
   Likely fix: simulate in a fixed logical world (the existing 760x340 default) and scale the drawing uniformly to fit the container (letterbox if needed),
   so it looks and plays the same everywhere. Verify at Z Fold cover width (~344px) and a normal phone (~390px).
