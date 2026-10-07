# Starlit Stories Client

This project is the frontend for Starlit Stories. It covers the public-facing product, account flows, story creation experience, profile area, billing UI, and story-reading interface.

## Frontend responsibilities

- Marketing and product-explanation pages
- Authentication and account recovery flows
- Story creation and customization workflows
- Story viewing for private and public-share routes
- Membership upgrade and billing entry points
- Profile and saved-story experience

## Engineering highlights

- Route-driven SPA structure with dedicated pages for product, auth, creation, support, and reading
- Centralized runtime configuration through Vite environment variables
- Shared Axios API client with JWT attachment and auth-expiry handling
- Stripe Elements integration for billing flows
- Production and staging build targets

## Main routes

- `/`
- `/create`
- `/customize`
- `/view`
- `/s/:token`
- `/login`
- `/signup`
- `/signup/complete`
- `/forgot-password`
- `/reset-password`
- `/verify-email`
- `/profile`
- `/upgrade`
- `/faq`
- `/support`
- `/about`

## Technology choices

- React 19
- React Router 7
- Vite 6
- Axios
- Stripe Elements

## Usage note

This client is included as part of a portfolio project and remains governed by the repository license rather than an open-source reuse model.
