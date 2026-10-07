# Starlit Stories Internal Notes

This document is a concise engineering reference for the current backend implementation.

It complements the public-facing README files by capturing implementation details that are useful during code review or technical discussion.

## Backend shape

- ASP.NET Core 8 Web API
- EF Core with SQL Server in normal environments and InMemory only for testing
- JWT auth without ASP.NET Identity
- OpenAI for generation
- Stripe for subscriptions and add-on credits
- Azure Blob Storage for generated image persistence
- Azure Key Vault for runtime secrets outside `Testing`

## Runtime behavior

- `Program.cs` loads Azure Key Vault in `Development`, `Staging`, and `Production`
- CORS is sourced from `App:AllowedCorsOrigins`, with localhost and production fallbacks
- Health endpoints: `/__ping`, `/healthz`, `/readyz`, `/api/healthz`
- Warm-up endpoint: `POST /api/warmup`
- Sitemap endpoints: `/sitemap.xml` and `/sitemaps/sitemap-{index}.xml`
- Login rate limiting is configured with policy `login-ip`

## Major controllers

- `AuthController`: signup, login, verify email, resend verification, forgot/reset password
- `StoryController`: synchronous generation, async job start, SSE progress, result fetch
- `PaymentsController`: checkout, one-time credits, billing portal, subscription status, cancel, webhook
- `ProfileController`: user profile and story retrieval
- `SavedCharacterController`: saved character CRUD and downgrade policy messaging
- `ShareController`: create, delete, and fetch public story shares
- `UsersController`: quota and usage

## Membership rules in code

- Plans: `Free`, `Pro`, `Premium`
- Base quotas from config default to `free=1`, `pro=5`, `premium=11`
- Saved character limits: `1`, `5`, `10`
- Free users get only the limited character field allowlist
- Story length gating:
  - Free: short only
  - Pro: short and medium
  - Premium: short, medium, and long

## Story generation flow

1. Authenticate the user and roll over the billing period if needed.
2. Reserve base quota or add-on credit.
3. Create a pending `Story` record.
4. Generate text and image prompts through `IStoryGeneratorService`.
5. Generate images through `IImageGeneratorService`.
6. Upload final images to Blob storage.
7. Persist pages and publish SSE progress updates when using the async path.

On failure, the controller removes the pending story and refunds any reserved add-on credit.

## Billing notes

- Billing provider defaults to Stripe
- Webhook idempotency is enforced with `ProcessedWebhook`
- Free-to-paid upgrade logic can carry over one unused free credit into `AddOnBalance`
- Add-on purchases are gated by config and membership rules in `QuotaService`

## Local-development constraint

Unless you run under `Testing`, this app expects Azure Key Vault access at startup. If a developer does not have access to `kv-starlitstories-dev`, local startup will fail until that behavior is changed or mocked.
