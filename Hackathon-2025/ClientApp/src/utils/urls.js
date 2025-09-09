export function publicBase() {
    // Always prefer the configured prod URL
    const env = import.meta.env.VITE_PUBLIC_SITE_URL;
    return (env && env.trim()) || window.location.origin;
}