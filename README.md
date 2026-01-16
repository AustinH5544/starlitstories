**Starlit Stories**

Starlit Stories is a full-stack SaaS platform that generates personalized, AI-powered children’s storybooks. The system supports authenticated users, subscription-based access, persistent story storage, and a modern web-based reading experience.
This repository contains the full application stack, including the frontend web app, backend API, and associated tests.
________________________________________
🚀 Features
•	Full-stack SaaS architecture (frontend + backend)
•	User authentication and authorization
•	Subscription-based access and billing logic
•	RESTful API for story generation and persistence
•	Cloud-backed database for user and story data
•	Secure secrets management via Azure Key Vault
•	Deployed and hosted on Azure infrastructure
________________________________________
🧱 Architecture Overview
The application follows a clean separation of concerns between frontend, backend, and data layers.
High-Level Components
•	Frontend: React-based web application for user interaction and story reading
•	Backend API: ASP.NET Core REST API handling authentication, business logic, and data access
•	Database: SQL database for users, stories, and application state
•	Cloud Infrastructure:
o	Azure App Service (hosting)
o	Azure API (backend exposure)
o	Azure Database
o	Azure Key Vault (secrets and configuration)
Solution Structure
•	Web application (React)
•	API project (ASP.NET Core)
•	Test projects for backend logic and services
________________________________________
🛠️ Tech Stack
Frontend
•	React
•	TypeScript / JavaScript
•	Modern component-based UI architecture
Backend
•	ASP.NET Core (.NET 8)
•	RESTful API design
•	Entity Framework Core
•	Authentication & authorization middleware
Data & Cloud
•	SQL Database
•	Azure App Service
•	Azure API
•	Azure Key Vault
Tooling
•	GitHub for version control
•	Automated testing for backend services
________________________________________
🧪 Testing
The repository includes test projects covering backend services and application logic. Tests are designed to validate core functionality and support safe iteration as the platform evolves.
________________________________________

🔐 Environment Configuration
This application relies on environment variables and secrets for secure operation.
You will need to configure values such as:
•	Database connection strings
•	Authentication secrets
•	API keys (including AI service integrations)
In production, secrets are managed via Azure Key Vault.
For local development, use environment variables or a secure local configuration method.
Note: No secrets are committed to this repository.
________________________________________
🌐 Deployment
A staging deployment is available at:
https://staging.starlitstories.app
Access is currently gated while the platform is under active development.
________________________________________
📌 Project Status
Starlit Stories is under active development, with ongoing work focused on:
•	Feature refinement
•	Performance and scalability improvements
•	UX enhancements
•	Production hardening
________________________________________
📫 Contact
Built and maintained by Austin Harrison and Tyler Woody.
For questions, feedback, or discussion, feel free to reach out via GitHub.

