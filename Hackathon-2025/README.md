# Starlit Stories Backend 🌙✨

This is the **backend API** for **Starlit Stories**, an AI-powered children's storybook generator.  
It provides secure user authentication, story generation, image generation, and payment integration using **ASP.NET Core 8**, **Azure**, and **Stripe**.

---

## 🚀 Features

- **AI Story Generation** – Uses OpenAI API (or local model) to generate creative stories based on user prompts and characters.  
- **Dynamic Image Generation** – Supports OpenAI DALL·E or local server-based image generation.  
- **Stripe Payments** – Handles membership plans and add-on credit purchases.  
- **Azure Blob Storage** – Stores story and cover images securely in Azure.  
- **Email Verification & Password Reset** – Built-in SMTP email service for account verification and password recovery.  
- **Role-Based Quotas & Add-ons** – Enforces generation limits based on user membership level.  
- **Public Story Sharing** – Users can share stories through time-limited public URLs.

---

## 🧱 Project Structure

```
Hackathon-2025/
│
├── Controllers/         # API endpoints (Auth, Story, Payments, Profile, etc.)
├── Data/                # EF Core DbContext and migrations
├── Models/              # Database entities and DTOs
├── Options/             # Strongly-typed configuration settings
├── Services/            # Business logic and integrations
└── Program.cs           # Main application entrypoint
```

---

## ⚙️ Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/yourusername/starlit-stories-backend.git
cd starlit-stories-backend
```

### 2. Configure Environment Variables

You can use either **User Secrets** or an `appsettings.Development.json` file.  
Example configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=StarlitStories;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY",
    "Issuer": "starlitstories.app",
    "Audience": "starlitstories.app",
    "ExpiresInMinutes": 60
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "app-password",
    "FromEmail": "support@starlitstories.app",
    "FromName": "Starlit Stories"
  },
  "AzureBlobStorage": {
    "ConnectionString": "YOUR_AZURE_STORAGE_CONNECTION_STRING",
    "ContainerName": "story-images"
  },
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  },
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSecret": "whsec_...",
    "PriceIdPro": "price_...",
    "PriceIdPremium": "price_...",
    "PriceIdAddon5": "price_...",
    "PriceIdAddon11": "price_..."
  },
  "App": {
    "BaseUrl": "https://starlitstories.app",
    "AllowedCorsOrigins": "https://starlitstories.app;https://staging.starlitstories.app"
  }
}
```

---

### 3. Apply Database Migrations

```bash
dotnet ef database update
```

---

### 4. Run the Application

```bash
dotnet run
```

The API will be available at:  
👉 `https://localhost:5001` (HTTPS) or `http://localhost:5000`

---

## 🧠 Core Endpoints Overview

| Area | Endpoint | Description |
|------|-----------|-------------|
| **Auth** | `/api/auth/signup` | Register new users |
|  | `/api/auth/login` | Log in and receive JWT |
|  | `/api/auth/verify-email` | Verify email using token |
|  | `/api/auth/forgot-password` | Request password reset |
| **Stories** | `/api/story/generate-full` | Generate full story with text and images |
|  | `/api/story/progress/{jobId}` | Stream generation progress |
| **Profile** | `/api/profile/me` | Get user profile |
|  | `/api/profile/me/stories` | Get user stories |
| **Payments** | `/api/payments/create-checkout-session` | Start Stripe checkout for plans |
|  | `/api/payments/buy-credits` | Purchase add-on credits |
| **Share** | `/api/stories/{id}/share` | Generate public share link |
| **Config** | `/api/config` | Get public configuration flags |

---

## 🧩 Key Services

### 🪄 `StoryGenerator` & `PromptBuilder`
- Converts user character and theme selections into AI-ready prompts.
- Uses either OpenAI GPT models or a local generation service.

### 🖼️ `LocalImageGeneratorService` / `OpenAIImageGeneratorService`
- Flexible image generation pipeline that supports local diffusion servers or OpenAI DALL·E.

### 💌 `EmailService`
- Sends verification and password reset emails using SMTP with templated content.

### 💰 `StripeGateway`
- Handles subscription and add-on purchases.
- Processes webhooks idempotently to update user membership and balances.

### 🕓 `PeriodService` & `QuotaService`
- Enforces time-based generation quotas and handles resets per billing cycle.

---

## ☁️ Deployment Notes

- Designed for **Azure App Service** with **Azure SQL Database** and **Blob Storage**.  
- Set environment variables in Azure’s **Configuration** blade under “Application settings”.  
- Use `ASPNETCORE_ENVIRONMENT=Production` and secure secrets through **Azure Key Vault**.

---

## 🧪 Testing Tips

- Use `Stripe CLI` for webhook testing:  
  ```bash
  stripe listen --forward-to localhost:5001/api/payments/webhook
  ```
- For local image generation, set `ImageController` endpoint to your diffusion server URL.

---

## 📜 License

MIT License © 2025 [@AustinH5544](https://github.com/AustinH5544) and [@twoody0](https://github.com/twoody0)  
Backend component of the Starlit Stories application.
