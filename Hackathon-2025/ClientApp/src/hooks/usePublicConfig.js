import { useEffect, useState } from "react";
import { API_URL } from "../config";

const defaultConfig = {
    lengthHintEnabled: false,
    showProgressPill: false,
    pricing: {
        free: { price: "$0/month", originalPrice: null, isOnSale: false, badgeText: null, saleHint: null },
        pro: { price: "$4.99/month", originalPrice: null, isOnSale: false, badgeText: null, saleHint: null },
        premium: { price: "$9.99/month", originalPrice: null, isOnSale: false, badgeText: null, saleHint: null },
    },
};

const stripTrailingSlash = (value) => String(value || "").replace(/\/+$/, "");

export default function usePublicConfig() {
    const [config, setConfig] = useState(defaultConfig);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        let isMounted = true;
        const url = `${stripTrailingSlash(API_URL)}/api/config`;

        fetch(url, { credentials: "omit" })
            .then((res) => {
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                return res.json();
            })
            .then((data) => {
                if (!isMounted) return;
                setConfig({
                    ...defaultConfig,
                    ...data,
                    pricing: {
                        ...defaultConfig.pricing,
                        ...(data?.pricing ?? {}),
                    },
                });
            })
            .catch((err) => {
                console.error("Failed to load /api/config:", err);
                if (isMounted) {
                    setConfig(defaultConfig);
                }
            })
            .finally(() => {
                if (isMounted) {
                    setLoading(false);
                }
            });

        return () => {
            isMounted = false;
        };
    }, []);

    return { config, loading };
}
