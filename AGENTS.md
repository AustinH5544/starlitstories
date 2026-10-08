# Starlit Stories — Agent Guide

AI-generated personalized children's storybooks. **This is a live production repo** (https://starlitstories.app) with real paying users. Prefer small, reviewable changes; flag anything touching auth, billing, webhooks, migrations, or config.

Folder-specific rules live next to the code — read them before working there:
- [Hackathon-2025/CLAUDE.md](Hackathon-2025/CLAUDE.md) — backend API
- [Hackathon-2025/ClientApp/CLAUDE.md](Hackathon-2025/ClientApp/CLAUDE.md) — React frontend
- [docs/README_INTERNAL.md](docs/README_INTERNAL.md) — generation flow, membership rules, billing notes

## Map

| Path | What |
|---|---|
| `StarlitStories.sln` | Solution entry point |
| `Hackathon-2025/` | ASP.NET Core 8 API (`Controllers/`, `Services/`, `Models/`, `Options/`, `Data/`, `Migrations/`, `Program.cs`, `PromptBuilder.cs`) |
| `Hackathon-2025/ClientApp/` | React 19 + Vite 6 SPA with build-time pre-rendering of public pages |
| `Hackathon-2025.Tests/` | MSTest + Moq + `WebApplicationFactory` (in-memory DB). `SqlServer/` tests run against a throwaway SQL Server in Docker (Testcontainers); without Docker they are skipped locally, but CI runs them. |
| `Jobs.WebhookPruner/` | Azure Function (timer) that prunes old `ProcessedWebhook` rows |
| `.github/workflows/` | Push to `main` → prod, `staging` → staging (API to App Service, client to Static Web Apps). Every deploy is gated: API build + all tests and frontend lint + build must pass, and each workflow waits on both sides. PRs into `main`/`staging` run the same checks without deploying. |
| `docs/` | Internal notes, architecture diagram, in-progress work logs |

## Commands

Run from the repo root unless noted.

- `dotnet build StarlitStories.sln` — warnings are errors (`TreatWarningsAsErrors`)
- `dotnet test Hackathon-2025.Tests/Hackathon-2025.Tests.csproj` (single class: `--filter "FullyQualifiedName~ClassName"`)
- `dotnet run --project Hackathon-2025/Hackathon-2025.csproj` — API on `https://localhost:5001`
- In `Hackathon-2025/ClientApp`: `npm install`, `npm run dev` (:5173), `npm run build`, `npm run build:staging`, `npm run lint`
- Stripe webhooks locally: `stripe listen --forward-to https://localhost:5001/api/payments/webhook`

No frontend test suite exists; run `npm run lint` and verify UI changes manually.

## Gotchas

- **Local startup needs Azure Key Vault access** (`kv-starlitstories-dev`) via `DefaultAzureCredential`. Only the `Testing` environment skips Key Vault.
- **Database is SQL Server in every environment except `Testing`** (EF InMemory). There is no SQLite.
- **Secrets** come from Key Vault (prod) or `dotnet user-secrets` (local). Never commit them. `ClientApp/.env.*` hold only public `VITE_*` values.
- **Docs drift**: verify claims in docs against code before relying on them.

## Conventions

- C#: nullable + implicit usings, 4-space indent, PascalCase types/members, camelCase locals, one class per file.
- React: `.jsx`, 4-space indent, PascalCase components in `src/components/` and `src/pages/`, each page/component paired with a same-named `.css` file; lower-case utility files (`api.js`, `config.js`).
- Tests: mirror production namespaces, files named `*Tests.cs`, descriptive method names like `Signup_Returns_RequiresVerification_And_Sends_Email`.
- Commits: short, specific subjects (`Updated character creation`). PRs: summary, testing performed, screenshots for UI, and explicit callouts for config/billing/auth/webhook impact.
