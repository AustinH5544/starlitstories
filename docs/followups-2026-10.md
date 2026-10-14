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

## Resume here (state at 2026-10-09)

- **PR #68** (items 1-8, 10, new avatars) merged 2026-10-08 as ada0d05. **PR #69** (item 11, interrupted-story recovery + migration
  `20261008120000_AddStoryReservedFromAddOn`) merged 2026-10-08 as 027e509. Both prod runs passed; prod `/healthz` is 200 and the API started, so the migration applied.
- The user merged #69 with the default first-run behavior: recovery refunds **every** already-stuck draft as a plan credit (no 30-day cutoff).
- Items 1-8, 10 and 11 are all in prod. Next-up item 5 (flight game) is in prod (PR #70, merge commit 6fba913, 2026-10-10).
- Next-up item 3A (character cards) is on `staging` (ab834e9), user-tested, with a staging -> main PR opened 2026-10-10.
- **Item 1 (lessons) is in design**: the agreed direction is a Story Spine outline (8 beats: setup, routine, problem, three escalating tries,
  solution by the child hero, how things changed) used as a hidden plan, never as repeated phrases, plus lesson rules (lesson drives the problem,
  translated into the theme; safety lessons model the safe choice). Prompt work stays on its own branch, off `staging`, until the user approves
  old-vs-new samples. Samples are generated locally by calling OpenAI directly; this is blocked on a funded key (the local user-secrets key has no credits).
- **Prompt changes are reviewed with the user before any push, even to staging**: show the before/after prompt text and sample outputs first.
- Local tooling notes: Docker Desktop must be running for the SqlServer test category (skipped locally without it; CI runs them).
  The Azure CLI login has expired (`az login` needed to read Key Vault / App Service settings or prod logs). The Higgsfield MCP has ~1,119 credits.
- The repo lives in OneDrive; a sync lock once broke `git switch` mid-way (recovered cleanly). Recommend moving it to e.g. `C:\dev\`.

## Next up (user priorities, 2026-10-08)

Item 9 (image prompting / reference images) folds into these. Each is product work: brainstorm and agree on a design with the user before coding.

1. **Make the story's lesson make sense.** `StoryGenerator` (around lines 65-110) asks for the lesson to be "woven in" and to end with a line starting `Lesson:`.
   Review real outputs for whether the lesson actually follows from the plot; improve the prompt (and possibly the `Lesson:` line format).
2. **Evaluate the text model.** Story text and scene prompts use `gpt-4.1-mini` (4 call sites). Compare current options on quality, cost, latency for children's stories;
   keep image quality at `low` (cost policy).
3. **Make saved characters more intuitive.** Agreed plan (2026-10-09), each its own PR:
   - **A. Done** (feat/character-cards, on staging, user-tested): "Who's the story about?" cards at the top of the form, one-tap select, edit/delete on the
     card with delete confirmation, Save next to the editor; fixed saved advanced-outfit characters failing the basic form's shirt/pants validation.
   - **B. Next:** keep each story's base character portrait (today it is thrown away after use) and attach it to the saved character; show it on the card.
     Store it generically as the character's reference picture plus a thumbnail. Database change: own PR with a callout.
   - **D.** After a story finishes, offer "Save {name} for next time?" with the portrait.
   - **C. Later:** reuse the saved portrait as the reference for later books in the same art style (consistency across books, one image fewer);
     needs a "Repaint" option and invalidation when the look is edited.
   Original note: **Make saved characters more intuitive.** UX review of the saved-character flow in `StoryForm.jsx` (`normalizeSavedCharacter`, the saved list, save/delete) and `SavedCharacterController`.
4. **Agreed direction (2026-10-09):** the user picked the recommended path. (1) After the story text, one text call extracts a cast list:
   characters on 2+ pages, a fixed look for each, and their pages; inject each fixed look into every page prompt (text only, cheap).
   (2) One portrait per recurring character (main + up to 2 sidekicks); each page gets only the portraits of characters on it, as multiple edit references.
   Needs an image test first: multiple references on one page, no duplicate characters. A single multi-angle or whole-cast sheet was considered and parked.
   (3) Then re-enable user-added characters. Keep this separate from the lesson prompt branch so samples show which change did what.
   Original note: **One character sheet for all characters, used everywhere.** Today one base portrait is generated per story (`BuildBaseCharacterPrompt`) and passed as the single
   reference to every page/cover edit (`GenerateImagesWithCharacterBaseAsync`). Idea: render every character in the story on one sheet and use it as the reference for all images
   (gpt-image-2 edits accept multiple `image[]` references; see the earlier research in this conversation's notes).
   Note: extra characters are currently disabled (`MembershipEntitlements.MaxCharactersPerStory = 1`, `showCharacterTypeAndExtraButton = false` in `StoryForm.jsx`), so this pairs with deciding whether to re-enable multiple characters.
5. **Done (2026-10-09, fix/flight-game-small-screens; user playtested on staging):** the game simulates in world units, shows at least a 460x300 world
   scaled to fit (desktop unchanged at 1:1), uses a 4:3 play area up to 768px wide, and has tap-specific text. Original note:
   **Painting flight game is too zoomed in on small screens** (user's Galaxy Z Fold cover screen; the game shows on the create page while a story generates).
   `components/PaintingFlightGame.jsx` sizes the canvas in CSS pixels with minimums (`Math.max(360, width)`, `Math.max(240, height)`) and uses fixed pixel
   sizes for the brush, obstacle width (74) and gaps (112-170), so on a ~344-360px-wide screen it overflows and everything looks oversized.
   Likely fix: simulate in a fixed logical world (the existing 760x340 default) and scale the drawing uniformly to fit the container (letterbox if needed),
   so it looks and plays the same everywhere. Verify at Z Fold cover width (~344px) and a normal phone (~390px).
6. **Zero-downtime deploys.** After the PR #68 prod deploy (2026-10-08 ~20:08 UTC) every normal API request returned an empty 500 for about 2 minutes
   while the new version started (health endpoints stayed 200), then recovered on its own. Every deploy also restarts the in-process story jobs (item 11 refunds them).
   Options: an App Service deployment slot with warm-up + swap, or at least `WEBSITE_SWAP_WARMUP_PING_PATH` / health-check settings. Needs Azure access (`az login`).

7. **Skin tone options must be fully inclusive** (user, 2026-10-09). Today basic mode offers brown, dark, freckled, light, olive, pale, tan, and advanced
   mode filters tones by ethnicity (`skinTonesByEthnicity` in `StoryForm.jsx`). Review the full set so every child can be represented, without awkward
   or limiting choices; consider not tying tone options to ethnicity at all, and check how `PromptBuilder` phrases skin tone for the image model.
8. **Explore sign-in with Google, Apple, etc.** (user, 2026-10-09). One-tap social login linked to an existing account. Auth change: design first
   (account linking by verified email, existing email/password users, Apple's private relay emails, Stripe customer mapping), own PR with an auth callout.
9. **Free stories for beta testers** (user, 2026-10-09). Beta users should get about 5-10 free stories to try everything without paying. Specs to explore:
   how testers are identified (invite code or link, admin grant, or everyone during the beta), whether credits expire, whether they use the existing
   add-on balance (`AddOnBalance`, already spendable by Free users), abuse limits (one grant per account/email), and what happens when the beta ends.
   Billing-adjacent: own PR with a callout.

Order for 2026-10-10 (user): lesson prompt first (needs a funded OpenAI key for samples), then items 7-9 are logged so they aren't forgotten.
