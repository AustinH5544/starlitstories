// @ts-check
// Centralized config for the frontend (Vite). JS version.

const required = (v, name) => {
    if (!v) throw new Error(`Missing ${name} (check your .env for ${name})`);
    return v;
};

const bool = (v, def = false) => (v === undefined ? def : v.toLowerCase() === "true");
const stripTrailingSlash = (s) => s.replace(/\/+$/, "");
const ensureLeadingSlash = (s) => (s.startsWith("/") ? s : `/${s}`);

// Mode flags
export const APP_ENV = import.meta.env.MODE; // 'development' | 'staging' | 'production'
export const IS_DEV = APP_ENV === "development";
export const IS_STAGING = APP_ENV === "staging";
export const IS_PROD = APP_ENV === "production";

// Required
export const API_URL = stripTrailingSlash(required(import.meta.env.VITE_API_URL, "VITE_API_URL"));
export const APP_BASE_URL = stripTrailingSlash(
    required(import.meta.env.VITE_APP_BASE_URL, "VITE_APP_BASE_URL")
);

// Optional
export const SENTRY_DSN = import.meta.env.VITE_SENTRY_DSN;
export const ANALYTICS_SITE_ID = import.meta.env.VITE_ANALYTICS_SITE_ID;
export const CDN_BASE = import.meta.env.VITE_CDN_BASE
    ? stripTrailingSlash(import.meta.env.VITE_CDN_BASE)
    : undefined;

export const FEATURES = {
    portal: bool(import.meta.env.VITE_FEATURE_PORTAL, true),
};

// Helpers
export const toAbs = (path) => APP_BASE_URL + ensureLeadingSlash(path);
export const toCdn = (path) => (CDN_BASE ?? APP_BASE_URL) + ensureLeadingSlash(path);

// Optional default export if you prefer object imports
const cfg = {
    APP_ENV,
    IS_DEV,
    IS_STAGING,
    IS_PROD,
    API_URL,
    APP_BASE_URL,
    SENTRY_DSN,
    ANALYTICS_SITE_ID,
    CDN_BASE,
    FEATURES,
    toAbs,
    toCdn,
};
export default cfg;