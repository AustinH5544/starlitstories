# Starlit Stories Backend — Internal Developer Documentation

## Overview

This backend powers **Starlit Stories**, an AI-powered children’s storybook platform. It handles user authentication, story generation, image creation (local or OpenAI), Stripe-based payment flows, Azure Blob image storage, and quota management.

### Core Technologies

- **.NET 8 / ASP.NET Core Web API**
- **Entity Framework Core** (SQL backend, typically Azure SQL)
- **Azure Blob Storage** for image persistence
- **Stripe** for subscriptions and one-time add-on credits
- **JWT Authentication** for secure user sessions
- **Background task orchestration** for story generation progress tracking
- **Email notifications** via SMTP

---

## Architecture

### 1. Controllers

| Controller | Responsibility |
|-------------|----------------|
| `AuthController` | Handles signup, login, verification, password reset, and JWT generation. |
| `StoryController` | Full story generation pipeline using the AI generator service. Supports async background job updates via SSE. |
| `ImageController` | Batch image generation using a local AI image generation server or OpenAI API. |
| `PaymentsController` | Stripe integration for subscriptions, add-on purchases, and webhooks. |
| `ProfileController` | Retrieves and updates user profiles, including avatars and owned stories. |
| `ShareController` | Generates secure public share links for stories. |
| `ConfigController` | Exposes runtime configuration (e.g., `LengthHintEnabled`) to frontend. |
| `BlobTestController` | Simple endpoint to test Azure Blob uploads. |
| `UsersController` | Tracks quota and period usage for logged-in users. |

---

### 2. Services

| Service | Description |
|----------|--------------|
| `BlobUploadService` | Uploads base64 or URL-based images to Azure Blob Storage. |
| `EmailService` | Sends verification and password reset emails via SMTP. |
| `IStoryGeneratorService` | Generates stories using LLM APIs or internal logic. |
| `OpenAIImageGeneratorService` / `LocalImageGeneratorService` | Pluggable backends for image generation. |
| `StripeGateway` | Encapsulates all Stripe-related calls and webhook handling. |
| `QuotaService` | Determines user quotas and addon purchase policies. |
| `PeriodService` | Tracks and resets user periods (monthly rollover). |
| `ProgressBroker` | Provides real-time progress updates to clients through Server-Sent Events. |

---

### 3. Models

Key domain models are stored under `/Models`:

- **User** — Represents registered users with authentication, billing, and quota tracking.
- **Story / StoryPage / StoryShare** — Persisted stories with shareable tokens.
- **StoryRequest / StoryResult** — Used during story generation.
- **ProcessedWebhook** — Prevents duplicate Stripe webhook processing.
- **Various DTOs** — Requests for signup, verification, password reset, checkout, etc.

---

### 4. Options & Configuration

Each options class maps to a section in configuration (`appsettings.json` or environment variables).

| Option Class | Description |
|---------------|--------------|
| `AppOptions` | Contains `BaseUrl` and allowed CORS origins. |
| `AzureBlobStorageOptions` | Connection string and container name. |
| `EmailOptions` | SMTP server credentials and sender information. |
| `JwtOptions` | Token signing key and metadata. |
| `StripeOptions` / `StripeSettings` | Stripe price IDs, webhook secrets, and API keys. |
| `OpenAISettings` | API key for OpenAI image/text models. |
| `CreditsOptions` | Policy rules and default quotas per plan. |
| `StoryOptions` | Feature toggles like `LengthHintEnabled`. |

---

## Configuration & Secrets

All secrets should be stored in **Azure Key Vault** or local user secrets (`dotnet user-secrets`).

### Example `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=StorybookDb;Trusted_Connection=True;"
  },
  "App": {
    "BaseUrl": "http://localhost:5173",
    "AllowedCorsOrigins": "http://localhost:5173;https://staging.starlitstories.app"
  },
  "AzureBlobStorage": {
    "ConnectionString": "<azure-blob-connection-string>",
    "ContainerName": "story-images"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "support@starlitstories.app",
    "SmtpPassword": "<password>",
    "FromEmail": "support@starlitstories.app",
    "FromName": "Starlit Stories"
  },
  "Jwt": {
    "Key": "<jwt-secret-key>",
    "Issuer": "StarlitStories",
    "Audience": "StarlitStoriesUsers",
    "ExpiresInMinutes": 1440
  },
  "Stripe": {
    "SecretKey": "<stripe-secret>",
    "PublishableKey": "<stripe-publishable>",
    "WebhookSecret": "<stripe-webhook>",
    "PriceIdPro": "price_xxx",
    "PriceIdPremium": "price_yyy",
    "PriceIdAddon5": "price_addon5",
    "PriceIdAddon11": "price_addon11"
  },
  "OpenAI": {
    "ApiKey": "<openai-key>"
  }
}
```

---

## Running Locally

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server / LocalDB
- Azure Storage account (for Blob)
- Stripe test account and webhook endpoint

### Commands

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Server will start on `https://localhost:5001` (HTTPS).

Frontend communicates with it via `/api` endpoints.

---

## Stripe Webhooks (Development)

To test locally:

```bash
stripe listen --forward-to https://localhost:5001/api/payments/webhook
```

Ensure you set the `WebhookSecret` value in configuration to match Stripe’s CLI output.

---

## Deployment Notes

- **Azure App Service** hosts the API (connected to Azure SQL + Blob Storage).  
- **Azure Static Web App** hosts the frontend (connected via environment variables).  
- **CI/CD via GitHub Actions** builds both backend and frontend.  
- Ensure environment variable naming matches Azure Key Vault secret keys.

---

## Switching Image Generation Providers

To use **local** generation:
```csharp
services.AddScoped<IImageGeneratorService, LocalImageGeneratorService>();
```

To use **OpenAI DALL·E**:
```csharp
services.AddScoped<IImageGeneratorService, OpenAIImageGeneratorService>();
```

This can be toggled via environment variable in `Program.cs`.

---

## Email Templates

- `SendVerificationEmailAsync(string email, string token)` → Sends verification link.  
- `SendPasswordResetEmailAsync(string email, string token)` → Sends password reset link.  

Emails use `AppOptions.BaseUrl` to build frontend links (e.g., `/verify?token=`).

---

## Periodic Quota Management

`PeriodService` and `QuotaService` handle user quotas:

- **Base quota:** `free=1`, `pro=5`, `premium=11`
- **Add-ons:** tracked via `AddOnBalance`
- Monthly rollover handled in `PeriodService.OnPeriodRollover()`

---

## API Summary (Quick Reference)

| Endpoint | Auth | Description |
|-----------|------|--------------|
| `POST /api/auth/signup` | ❌ | Create user account. |
| `POST /api/auth/login` | ❌ | Authenticate and return JWT. |
| `POST /api/auth/verify-email` | ❌ | Confirms verification token. |
| `POST /api/stories/generate-full` | ✅ | Generate complete story with images. |
| `GET /api/stories/progress/{jobId}` | ❌ | Stream background job progress (SSE). |
| `POST /api/payments/create-checkout-session` | ✅ | Stripe subscription checkout. |
| `POST /api/payments/buy-credits` | ✅ | Purchase add-on credits. |
| `GET /api/profile/me` | ✅ | Retrieve user profile. |
| `PUT /api/profile/avatar` | ✅ | Update avatar. |
| `GET /api/share/{token}` | ❌ | Publicly shared story. |

---

## Logging & Diagnostics

- Uses default **ASP.NET Core Logging**.
- Stripe events logged via `ILogger<PaymentsController>`.
- 400s and 500s automatically formatted by middleware.

---

## Security and API Fundamentals

### What is an API?

- Definition: An Application Programming Interface (API) is a contract that lets clients (web/mobile frontends, services) interact with this backend over HTTPS.
- Style: This project exposes a REST-style JSON API under `/api/...` using standard HTTP verbs:
  - GET (read), POST (create/action), PUT/PATCH (update), DELETE (remove).
- Conventions:
  - Stateless requests: each request carries all info needed to process it (not tied to server session).
  - Idempotency: GET/PUT/DELETE are expected to be safe/idempotent where feasible; POST drives server-side actions.
  - Content types: `application/json` for request/response bodies; `text/event-stream` for Server-Sent Events (SSE).
  - Status codes: 2xx success, 4xx client errors (validation, auth), 5xx server errors.
- CORS: Cross-Origin Resource Sharing limits which frontend origins can call the API. Allowed origins come from `AppOptions.AllowedCorsOrigins`.

### What is JWT?

- Purpose: JSON Web Tokens (JWT) are compact, URL-safe bearer tokens used for stateless authentication and authorization between the frontend and this API.
- Structure: `header.payload.signature` (all Base64URL-encoded).
  - Header: algorithm and token type (e.g., HS256, JWT).
  - Payload (claims): statements about the user (e.g., `sub` user ID, `email`, roles) plus standard claims like `exp` (expiry), `iss` (issuer), `aud` (audience).
  - Signature: cryptographic signature over header+payload using the server’s signing key.
- Signing vs. encryption:
  - This project signs tokens using HMAC SHA-256 with a symmetric secret (`JwtOptions.Key`). Tokens are not encrypted; treat contents as readable by the client. Never place secrets in JWT payloads.
- Validation basics:
  - Verify signature using the same secret.
  - Enforce expiration (`exp`) and reject expired tokens.
  - Match `iss` (Issuer) and `aud` (Audience) from configuration.
  - Optionally allow small clock skew to account for client/server time drift.

### How this backend uses JWT

- Issuance: `AuthController` creates a JWT after successful signup/login/verification.
  - Configuration: `JwtOptions.Key`, `Issuer`, `Audience`, `ExpiresInMinutes`.
  - Claims: Includes user identity (e.g., subject/user ID, email). Roles/permissions can be added if needed.
- Transport: Clients send the token in the HTTP header:
  - `Authorization: Bearer <jwt>`
- Protection:
  - Endpoints that require auth are decorated with `[Authorize]`. Public endpoints use `[AllowAnonymous]`.
  - Tokens are short-lived per `ExpiresInMinutes`. There is no refresh-token flow in this codebase by default; clients should re-authenticate after expiry.
- Storage guidance:
  - Prefer secure, HTTP-only cookies to reduce XSS exposure, or if using storage APIs, implement strong XSS mitigation.
  - Always use HTTPS in production to prevent token interception.

### Related backend concepts

- Server-Sent Events (SSE):
  - One-way real-time stream from server to client using `text/event-stream`.
  - Used by `GET /api/stories/progress/{jobId}` to push background job updates without polling.
- Webhooks:
  - Incoming HTTP callbacks from third-party services (Stripe) to our `PaymentsController`.
  - Authenticity: Stripe signatures are validated using `Stripe.WebhookSecret`.
  - Idempotency: Processed events are tracked via `ProcessedWebhook` to avoid duplicates.
- CORS and origins:
  - The API only accepts cross-origin browser requests from whitelisted origins set in `AppOptions.AllowedCorsOrigins`.

---

## Future Improvements

- Support for multiple image generation backends
- Story edit and regeneration flows
- More granular quota management (per character, per story)
- Centralized health monitoring endpoint

---

© 2025 Starlit Stories — Internal Developer Documentation
