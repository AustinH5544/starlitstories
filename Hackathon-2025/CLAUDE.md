# Backend API (ASP.NET Core 8)

Loaded when working in `Hackathon-2025/`. Generation, membership, and billing rules are in [../docs/README_INTERNAL.md](../docs/README_INTERNAL.md).

## Where things are wired

- `Program.cs` holds all composition: Key Vault, options binding + `ValidateOnStart`, EF Core, JWT, DI registrations, CORS, rate limiting (`login-ip`, `signup-ip`, `feedback-ip`, `support-ip`), security headers, and minimal-API endpoints (`/__ping`, `/healthz`, `/readyz`, `/api/healthz`, `POST /api/warmup`, `/sitemap.xml`, `/sitemaps/sitemap-{index}.xml`).
- Controllers use `[Route("api/[controller]")]`, so the **class** name sets the URL. `Controllers/PaymentController.cs` contains `PaymentsController`, which gives `/api/payments/...`.
- Typed options live in `Options/`, plus a few in `Models/` (`CreditsOptions`, `StoryOptions`, `StripeSettings`, `OpenAISettings`). Config sections: `ConnectionStrings`, `Jwt`, `Email`, `Stripe`, `AzureBlobStorage`, `OpenAI`, `App`, `Billing`, `Admin`, `Turnstile`, `Credits`, `Story`, `Sharing`, `ImageGeneration`, `Feedback`, `Support`.

## Story generation

- `StoryController` → `IStoryGeneratorService` (`Services/StoryGenerator.cs`) → `PromptBuilder.cs` for prompts → `IImageGeneratorService` → `IBlobUploadService` (`BlobUploadService`). Tests swap in `FakeBlobUploadService`.
- Two paths: sync `POST /api/story/generate-full`, and async `POST /api/story/generate-full/start`. The async path streams progress over SSE at `GET /api/story/progress/{jobId}` via the singleton `ProgressBroker`; fetch the result with `GET /api/story/result/{jobId}`.
- Jobs run in-process, so a restart (every deploy) kills stories mid-generation. Each draft records `ReservedFromAddOn`; `StaleDraftRecovery` (run every 10 minutes by `StaleDraftRecoveryService`) removes page-less drafts older than `StoryCredits.GenerationWindow` (30 min) and refunds the right credit. Owners deleting such a draft get the refund too.
- Text model: `gpt-4.1-mini`. Images: `gpt-image-2` at `quality = "low"`. Low quality is a **deliberate, validated cost decision**; do not raise it.
- `IImageGeneratorService` has a single implementation, `OpenAIImageGeneratorService`, registered in `Program.cs`.
- On failure the controller deletes the pending `Story` and refunds any reserved add-on credit. Keep that invariant.

## Membership, quota, billing

- Plans are the `MembershipPlan` enum (`Free`, `Pro`, `Premium`). Plan limits are split across two places:
  - `Credits:BaseQuotas` in `appsettings.json` (stories per period, 1/5/11), enforced by `QuotaService`. `PeriodService` handles the monthly rollover.
  - `Services/MembershipEntitlements.cs` (saved-character limits 1/5/10, and which character fields Free users may set).
- **Credit counters (`BooksGenerated`, `AddOnBalance`, `AddOnSpentThisPeriod`) are changed only with single conditional `ExecuteUpdateAsync` statements** (reserve, refund, rollover in `StoryController`). Never load a user, change a counter, and save the whole row (`_db.Users.Update(user)`): simultaneous requests double-spend, and a late refund overwrites plan changes or purchases made meanwhile.
- The webhook runs inside EF's retrying execution strategy and clears the change tracker at the start of each attempt, so a retried event is applied once. `CancelAtUtc` is only set by events that carry a subscription ID; add-on checkouts don't change `PlanStatus`.
- `StripeGateway` implements `IPaymentGateway`: it verifies and parses webhook events. `PaymentsController.Webhook` then writes the idempotency fence with a raw SQL `MERGE` into `ProcessedWebhooks` keyed on the event ID, and skips events it has already processed. `Jobs.WebhookPruner` deletes old fence rows.
- Billing, auth, and webhook changes are high-risk: production takes real payments. Test against Stripe test mode first (see [../docs/stripe-test-mode-notes.md](../docs/stripe-test-mode-notes.md)).

## Database / migrations

- `Data/AppDbContext.cs`. SQL Server outside `Testing`.
- Add migrations from this folder: `dotnet ef migrations add <Name>`. Never edit an applied migration; production applies them in order.
- Some migrations don't follow the usual pattern: `Migrations/MakeStoryPageRequiredWithCascade.cs` has no timestamp or namespace, and `20260317120000_AddStoryRequestMetadata.cs` and `20261008120000_AddStoryReservedFromAddOn.cs` were hand-written without a `.Designer.cs` file (`dotnet ef` needs Key Vault access to boot the app). Hand-written migrations must update `AppDbContextModelSnapshot.cs` too. Leave the existing ones alone unless asked.

## Tests

- `Hackathon-2025.Tests/Utils/TestWebAppFactory.cs` boots the app in the `Testing` environment (InMemory DB, no Key Vault) and swaps out `IEmailService`, `StripeClient`, `OpenAIClient`, and `ITurnstileService`. Any new external dependency needs a test double registered there.
- Anything that reserves or refunds credits (story generation, webhooks) must be tested with `SqlServerWebAppFactory` (`[TestCategory("SqlServer")]`, needs Docker locally): InMemory can't run `ExecuteUpdate`, raw SQL, or real transactions. `FailNextSave` simulates a deadlock retry; `FakeStoryGenerator.Hold` pauses generation mid-story.

## Endpoint security

New controllers and actions must carry `[Authorize]` unless they are deliberately public. The public ones today are auth, feedback (rate-limited), the support contact form `POST /api/support` (rate-limited + Turnstile), config, the Stripe webhook, public share fetch, story `ping`, and the SSE `progress/{jobId}` stream (`EventSource` can't send auth headers). A public endpoint that calls OpenAI or writes to Blob storage costs real money.

`EndpointAuthorizationTests` enforces this: any endpoint reachable without login must be on its `AllowedPublic` list, so adding a public endpoint means adding it there in the same commit.
