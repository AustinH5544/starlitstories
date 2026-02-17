import axios from "axios";
import { API_URL } from "./config";

const api = axios.create({
    baseURL: `${API_URL}/api`,   // <- always hit the real API origin
    timeout: 500000,
    headers: { "Content-Type": "application/json" }
});

// Attach token before requests
api.interceptors.request.use((config) => {
    const token = localStorage.getItem("token");
    if (token) {
        // don't attach a stale token to auth endpoints that don't need it
        const url = (config.url || '').toLowerCase();
        const isAuthEndpoint =
            url.includes('/auth/login') ||
            url.includes('/auth/resend-verification') ||
            url.includes('/auth/signup') ||
            url.includes('/auth/verify-email') ||
            url.includes('/auth/forgot-password') ||
            url.includes('/auth/reset-password');

        if (!isAuthEndpoint) {
            config.headers.Authorization = `Bearer ${token}`;
        }
    }
    return config;
});

// Auto-logout on 401 response, but skip for auth endpoints or when explicitly asked
api.interceptors.response.use(
    (res) => res,
    (err) => {
        const status = err?.response?.status;
        const cfg = err?.config || {};
        const url = (cfg.url || '').toLowerCase();

        const isAuthEndpoint =
            url.includes('/auth/login') ||
            url.includes('/auth/resend-verification') ||
            url.includes('/auth/signup') ||
            url.includes('/auth/verify-email') ||
            url.includes('/auth/forgot-password') ||
            url.includes('/auth/reset-password');

        const skipRequested = Boolean(cfg.skipAuth401Handler);

        if (status === 401 && !isAuthEndpoint && !skipRequested) {
            // Clear token and any persisted user data
            localStorage.removeItem("token");
            localStorage.removeItem("user");
            sessionStorage.clear();

            // Redirect to login page
            window.location.href = "/login";
        }
        return Promise.reject(err);
    }
);

export default api;