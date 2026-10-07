# Starlit Stories API

This project is the backend service layer for Starlit Stories. It owns authentication, story generation orchestration, billing logic, persistence, sharing, quota enforcement, and operational endpoints.

## Backend responsibilities

- JWT auth with signup, login, email verification, resend verification, forgot password, and reset password
- Story generation with both synchronous completion and async job/progress flow
- Image generation integration and Blob-backed image persistence
- Stripe checkout, add-on purchases, billing portal, subscription status, cancellation, and webhook handling
- Profile, story history, saved-character management, and public story sharing
- Health, readiness, warm-up, and sitemap endpoints

## Engineering highlights

- Stateless auth design using JWTs instead of server session state
- EF Core persistence with migrations and test-time in-memory database usage
- Server-Sent Events for long-running story generation progress
- Persisted webhook idempotency fences to make Stripe event handling safe to retry
- Options-bound configuration with startup validation
- Membership-aware entitlement enforcement for quotas, lengths, and saved-character limits

## Main architectural pieces

- `Controllers/`: API surface and request orchestration
- `Services/`: generation, billing, quota, email, progress, and Blob integrations
- `Models/`: domain entities and request/response contracts
- `Options/`: typed configuration binding
- `Data/`: EF Core `AppDbContext`
- `Migrations/`: schema history

## Representative capabilities in code

- Plans: `Free`, `Pro`, `Premium`
- Default story quotas per period: `1`, `5`, `11`
- Saved character limits: `1`, `5`, `10`
- Story length access by plan
- Public share-token story access
- Stripe webhook reconciliation into persistent membership state

## Technology choices

- ASP.NET Core 8
- Entity Framework Core
- SQL Server
- Stripe
- OpenAI
- Azure Blob Storage
- Azure Key Vault

## Usage note

This code is published for portfolio review and technical discussion under the repository license, not as a reusable backend framework.
