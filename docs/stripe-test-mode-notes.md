# Stripe Webhook Testing — Test Mode (work log)

> Moved from the root `CLAUDE.md` "In-Progress Work" section (last updated around Feb 2026). Status may be stale — confirm before acting on it.

We switched to **Stripe test mode** (`pk_test_` / `sk_test_` keys) to avoid real charges. Use card `4242 4242 4242 4242` for all test purchases.

## Completed fixes (committed, needs deploy)

- `Services/StripeGateway.cs`: Removed `tolerance: 0` from `EventUtility.ConstructEvent`. This was rejecting Stripe retries because the old timestamp fell outside the 0-second window. Now uses the SDK default of 300 seconds.
- `ClientApp/src/pages/ProfilePage.jsx`: Fixed subscription display — canceled state now shows `"Next renewal / Canceled (Pro until [date])"` instead of `"Ends on / [date]"`. Removed the "Cancel membership" button entirely; the Stripe customer portal handles cancel and reactivate.
- `ClientApp/src/pages/ProfilePage.css`: Fixed text overflow in the renewal detail card — `word-break: break-word` + `min-width: 0` on `.detail-content`.

## Pending actions before testing

1. **Delete** the "StarlitStories API - Dev" webhook from the Stripe Dashboard (live mode). It points to the same production URL as "Production Webhook" but uses a different signing secret, causing ~50% of live webhook deliveries to fail with `400 The expected signature was not found`.
2. **Deploy** the `StripeGateway.cs` change (push to main → GitHub Actions → Azure App Service).
3. In the **Stripe test mode Dashboard → Settings → Billing → Customer portal**, set downgrade timing to `"Schedule change for next renewal"` (to match intended production behavior).
4. **Re-test** a full checkout in test mode: confirm `checkout.session.completed` webhook processes without 400 errors, confirm profile page updates correctly after redirect.

## How to test

Trigger a Stripe test mode checkout → complete with `4242 4242 4242 4242` → Stripe redirects back to `/profile` → page reloads and re-fetches `GET /api/payments/subscription` → verify the plan/renewal display is correct.
