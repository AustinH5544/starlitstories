import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";

export default defineConfig({
    base: '/',
    plugins: [react()],
    ssr: {
        noExternal: ['react-helmet-async', 'react-router', 'react-router-dom'],
    },
})