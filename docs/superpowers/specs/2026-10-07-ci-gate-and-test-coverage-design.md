# CI Test Gate + Production Test Coverage — Design

**Date:** 2026-10-07
**Status:** Approved in conversation; awaiting written-spec review
**Branch:** `chore/tests-and-ci` (from `staging`)

## Goal

Broken code must never reach production. Every deploy (to `staging` and `main`) is blocked unless the API builds, all backend tests pass, and the frontend lints and builds. Backend tests cover the code where a bug costs money or trust, chosen by risk, not by a coverage percentage.

## Scope

**In:** CI gate (API + frontend workflows), backend test infrastructure, backend tests in three risk tiers, frontend lint cleanup.

**Out (follow-up rounds):** frontend unit/component tests (Vitest + React Testing Library), image optimization, PWA work.

## Constraints

- Production repo. A push to `main` deploys to prod; all work lands on `staging` first. Promoting to `main` is done by the user.
- Tests never call real Stripe, OpenAI, Azure (Blob, Key Vault), or SMTP/ACS.
- Production behavior must not change, apart from the one approved refactor (`IBlobUploadService`) and the approved lint fixes. If a test exposes a real bug, stop and report it with a proposed fix; the user decides. Until then the test documents current behavior.

## Current state (2026-10-07)

- 45 passing tests: Auth controller (13), story-result endpoint (2), ProgressBroker (5), model property tests (25).
- `PaymentsControllerTests` (12) and `ProfileControllerTests` (6) are entirely commented out (since Oct 2025), written against an older model (e.g. string membership). They will be rewritten, not uncommented.
- No tests for: Story generation, SavedCharacter, Share, Admin, Feedback, Users controllers; QuotaService, PeriodService, StripeGateway, MembershipEntitlements, PromptBuilder, UsernameRules, PngImageInspector, AdminAccessService.
- Frontend `npm run lint`: 41 problems (31 errors, 10 warnings).
- CI (`.github/workflows/api.yml`, `frontend.yml`) only deploys; no build/test/lint gate.

## 1. CI gate

### `api.yml`
- New `test` job (ubuntu-latest, .NET 8): `dotnet restore`, `dotnet build -c Release` (TreatWarningsAsErrors already on, so NuGet advisories fail here too), `dotnet test` including SQL Server tests (Docker is preinstalled on GitHub runners).
- `deploy-staging` and `deploy-prod` get `needs: test`.
- Add `pull_request` trigger. Deploy jobs keep their branch `if:` conditions, so PRs run only `test`.

### `frontend.yml`
- Add `npm run lint` before the build in both deploy jobs (or as a `lint` job they `need`).
- Add `pull_request` trigger running lint + build only, no deploy.

No bypass switch. A failing test blocks the deploy.

## 2. Test infrastructure

**Project:** keep the single `Hackathon-2025.Tests` (MSTest + Moq + `WebApplicationFactory`). Folders mirror the app: `Controllers/`, `Services/`, `Models/`, `Utils/`, plus new `SqlServer/`.

**`TestWebAppFactory` (shared, `Testing` environment, InMemory DB, no Key Vault)** replaces every external dependency:
- Existing: `IEmailService`, `StripeClient`, `OpenAIClient`, `ITurnstileService`.
- New: `IPaymentGateway` (Moq; lets tests return a parsed webhook tuple or checkout session), `IStoryGeneratorService` and `IImageGeneratorService` (fakes returning a canned story instantly, switchable to throw), `IBlobUploadService` (fake returning deterministic URLs).
- Each factory instance uses its own InMemory database name, so tests don't share data.

**Auth in tests:** `TestAuthHandler` already reads `X-Test-UserId`. Add an `X-Test-Email` header (emits `ClaimTypes.Email`) for admin tests, and an `X-Test-Anonymous: true` header that returns `NoResult` (unauthenticated) for 401 tests. With no headers the default stays authenticated user 1, so existing Auth tests are unaffected.

**Test data builders:** e.g. `TestUsers.Free(booksGenerated: 1)`, `TestUsers.Premium(addOnBalance: 3)`, `TestStories.For(user)`, so each test reads as its scenario.

**SQL Server tests (`SqlServer/`, `[TestCategory("SqlServer")]`):**
- `Testcontainers.MsSql` (test project only). One container per test run (assembly-level init); apply real EF migrations via `Database.Migrate()`. Each test cleans its rows.
- Because the `Testing` environment registers the InMemory provider, SQL Server tests use a factory variant (`SqlServerWebAppFactory`, derived from `TestWebAppFactory`) that replaces the `AppDbContext` registration with `UseSqlServer(<container connection string>)`. Pure data-level tests (e.g. "migrations apply") construct `AppDbContext` directly against the container.
- Docker unavailable: locally the tests are **skipped** (`Assert.Inconclusive`). In CI (`CI=true`) they **fail**, so payment tests can never be silently skipped.

**Stripe signature tests:** build a correctly signed `Stripe-Signature` header in-test (HMAC-SHA256 over `timestamp.payload` with a test secret) to exercise `StripeGateway.HandleWebhookAsync` for real.

**Approved production change, the only one in this round besides lint fixes:** add `IBlobUploadService` (`UploadImageAsync`, plus any other public methods `StoryController` uses). `BlobUploadService` implements it; DI registers the interface; `StoryController` depends on the interface. Behavior is identical. Reason: the concrete class connects to Azure in its constructor, which made `StoryController` untestable.

## 3. Coverage, by risk tier

### Tier 1: money and credits

**Stripe webhook handler (`PaymentsController.Webhook`), against SQL Server:**
- Free→Pro and Free→Premium: sets `PlanKey`, `Membership`, `PlanStatus`, period start/end, `BillingProvider`, customer/subscription refs.
- Free-credit carryover: unused free story (`BooksGenerated == 0`) adds +1 `AddOnBalance`; already used, no carryover. Counters reset on upgrade.
- Add-on packs: `addon_plus5` = +5 × qty, `addon_plus11` = +11 × qty; unknown SKU changes nothing.
- Unknown plan key: `PlanKey` stored, membership unchanged.
- User resolution order: uid, then customer ref, then subscription ref. User not found: 200, event consumed.
- Cancellation: status and `CancelAtUtc` recorded.
- Non-actionable event: 200, no changes.
- **Idempotency:** the same event ID twice (sequential) applies once; the same event ID concurrently (parallel requests) applies once.
- `StripeException` from the gateway returns 400; other exceptions return 500.

**`StripeGateway` webhook parsing:** valid signature accepted; forged or invalid rejected; a timestamp a few minutes old (normal Stripe retry) **accepted**, which is the regression test for the `tolerance: 0` bug; supported event types map to the expected tuple fields.

**Checkout and billing endpoints:** each requires auth; `create-checkout-session` passes the plan to the gateway; `buy-credits` enforces `CanBuyAddons` (Premium-only, only when base exhausted, per config); `billing/portal`, `subscription`, `cancel` behave for subscribed and unsubscribed users.

**`QuotaService` / `CreditsOptions`:** base quota per plan, case-insensitive ("Premium" == "premium"), unknown plan → 0, null/blank → free; `CanBuyAddons` matrix across config flags.

**`PeriodService`:** Stripe-window period vs calendar-month fallback; boundary when `now >= CurrentPeriodEndUtc`; calendar month change; rollover resets `BooksGenerated` / `AddOnSpentThisPeriod`, clears or keeps `AddOnBalance` per `CarryoverEnabled`, rolls the start forward and clears the stale end.

**Story generation (`StoryController`), sync `generate-full` and async `generate-full/start`:**
- Free user with quota used → 403; with add-on credit → succeeds and consumes 1 add-on.
- Success persists title, cover and pages; async publishes progress and result.
- **Generator failure → pending story deleted and reserved add-on refunded** (both paths).
- Month boundary triggers rollover before the quota check.
- Story length gated by plan (Free: short; Pro: short/medium; Premium: all), via `BuildEffectiveRequest`.
- Free users' character fields trimmed to the allowlist.

### Tier 2: access control and user data

- **Endpoint auth guard:** enumerate every endpoint (`EndpointDataSource`); each must require authorization or be on an explicit public allowlist (auth endpoints, feedback (rate-limited), Stripe webhook, config, share fetch, story ping, SSE progress, `/__ping`, `/healthz`, `/readyz`, `/api/healthz`, `/api/warmup`, sitemaps). Fails if a new unprotected endpoint appears.
- **Ownership:** users can't read or modify another user's story (profile), saved characters (update/delete), shares (create on another's story, revoke another's link), or job results (already covered).
- **Admin:** every admin endpoint is 403 for non-admins and works for admins; `AdminAccessService` email matching is case- and whitespace-insensitive and handles a missing email claim.
- **Saved characters:** limits 1/5/10 by plan; Free field sanitization; validation errors.
- **Sharing:** create, fetch and revoke; expired share returns not found; default expiry from `Sharing:DefaultExpirationDays`; requested-days behavior documented (see open questions).
- **Profile:** `me`, avatar update, username rules (valid, invalid, taken, normalization), story list paging, single story.
- **Usage (`/api/users/me/usage`):** remaining base and add-on counts and `canBuyAddons` per plan.
- **Feedback:** validation of required fields and limits.
- **Auth gaps:** wrong password and unknown email return the same error (no account enumeration); issued JWT carries user ID and email claims.

### Tier 3: helpers and platform

- `UsernameRules`, `MembershipEntitlements` (limits, Free allowlist, role default), `PngImageInspector` (valid PNG stats, non-PNG / truncated rejected), `PromptBuilder` (default art style, unknown style fallback, length caps via `CleanForModel`, lesson guards, character anchor content).
- **Platform:** all migrations apply to an empty SQL Server (SqlServer category); `/healthz`, `/api/healthz`, `/readyz` respond; `/sitemap.xml` is valid XML listing the static public URLs.

### Not tested (deliberately)
Real email delivery, OpenAI HTTP calls, Azure Blob I/O, Key Vault (thin wrappers over external services); no new trivial model-property tests.

**Expected size:** about 120–150 new tests (suite ~170–200), whole suite under ~2 minutes including the SQL Server container.

## 4. Frontend lint cleanup

41 problems in 12 files. Mechanical, behavior-preserving fixes only:
- `no-unused-vars` (16): remove.
- `no-empty` (11): add an explanatory comment inside intentionally empty `catch {}` blocks.
- `react-hooks/exhaustive-deps` (8, warnings): fix only where the change cannot alter behavior; otherwise add a justified `eslint-disable-next-line` comment.
- `no-useless-escape` (2): remove the escapes.
- `react-refresh/only-export-components` (2, warnings): leave or move the helper; no behavior change.
- `no-unsafe-finally` (1, `ProfilePage.jsx`): restructure as `if (alive) { ... }`, same behavior.
- `no-undef` (1, `StoryForm.jsx`): **latent crash.** `animalIcon` import is commented out and `assets/ui-icons/animal.png` does not exist; any character with `isAnimal: true` would crash the create page. Fix: use `personIcon` as the fallback. No visible change today (the animal toggle is hidden).

**Verification:** lint passes with 0 errors; `npm run build:staging` succeeds; pre-rendered HTML for all public routes is byte-identical before and after (diff of `dist/**/index.html`); user spot-checks the create and profile pages on staging.

## 5. Process

Work on `chore/tests-and-ci`, one commit per step, merged into `staging` when green:
1. Test infrastructure + `IBlobUploadService`.
2. Tier 1 tests.
3. Tier 2 tests.
4. Tier 3 tests.
5. Frontend lint cleanup.
6. CI gate (last, so it switches on only when everything already passes).

Bugs found by tests are reported to the user with a proposed fix; nothing changes behavior without approval.

## Done when

- CI blocks deploys on failing build, tests or lint, for both `staging` and `main`, and runs checks on PRs.
- ~170–200 tests pass locally and in CI, with the SQL Server tests running (not skipped) in CI.
- `staging` is deployed and healthy.
- The user has a list of any bugs found, with proposed fixes.

## Open questions (decide when reached; don't block the work)

- **Share link expiry cap:** `ShareController` accepts any positive `days` from the client with no maximum. Keep unlimited, or cap (e.g. 365)? The test documents current behavior until decided.
