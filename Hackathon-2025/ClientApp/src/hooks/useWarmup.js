import { useEffect } from "react";
import api from "../api";

export default function useWarmup() {
    useEffect(() => {
        if (sessionStorage.getItem("ss_warmed")) return;
        (async () => {
            try {
                await api.get("/healthz", { timeout: 4000 });
                await api.post("/warmup", { shallow: true }, { timeout: 7000 });
                sessionStorage.setItem("ss_warmed", "1");
            } catch { }
        })();
    }, []);
}
