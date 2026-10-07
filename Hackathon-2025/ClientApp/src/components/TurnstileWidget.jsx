import { useEffect, useRef } from "react"
import { TURNSTILE_SITE_KEY } from "../config"

const SCRIPT_SRC = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit"

/**
 * Cloudflare Turnstile human check. Renders nothing when no site key is configured.
 * Tokens are single-use: bump `resetSignal` after each submit to get a fresh one.
 * SSR-safe: the widget is only created in an effect.
 */
const TurnstileWidget = ({ onToken, onError, resetSignal = 0, theme = "auto", className = "" }) => {
    const containerRef = useRef(null)
    const widgetIdRef = useRef(null)
    // Keep the latest callbacks without re-rendering the widget when they change.
    const onTokenRef = useRef(onToken)
    const onErrorRef = useRef(onError)
    onTokenRef.current = onToken
    onErrorRef.current = onError

    useEffect(() => {
        if (!TURNSTILE_SITE_KEY) return undefined

        let cancelled = false

        const renderWidget = () => {
            if (cancelled || !containerRef.current || !window.turnstile || widgetIdRef.current !== null) return

            widgetIdRef.current = window.turnstile.render(containerRef.current, {
                sitekey: TURNSTILE_SITE_KEY,
                theme,
                callback: (token) => onTokenRef.current?.(token),
                "expired-callback": () => onTokenRef.current?.(""),
                "error-callback": () => {
                    onTokenRef.current?.("")
                    onErrorRef.current?.("Human verification could not load. Please refresh and try again.")
                },
            })
        }

        const existingScript = document.querySelector(`script[src="${SCRIPT_SRC}"]`)
        if (window.turnstile) {
            renderWidget()
        } else if (existingScript) {
            existingScript.addEventListener("load", renderWidget)
        } else {
            const script = document.createElement("script")
            script.src = SCRIPT_SRC
            script.async = true
            script.defer = true
            script.addEventListener("load", renderWidget)
            document.head.appendChild(script)
        }

        return () => {
            cancelled = true
            document.querySelector(`script[src="${SCRIPT_SRC}"]`)?.removeEventListener("load", renderWidget)
            if (widgetIdRef.current !== null && window.turnstile?.remove) {
                window.turnstile.remove(widgetIdRef.current)
                widgetIdRef.current = null
            }
        }
    }, [theme])

    useEffect(() => {
        if (resetSignal === 0 || widgetIdRef.current === null || !window.turnstile?.reset) return
        onTokenRef.current?.("")
        window.turnstile.reset(widgetIdRef.current)
    }, [resetSignal])

    if (!TURNSTILE_SITE_KEY) return null
    return <div ref={containerRef} className={className} />
}

export default TurnstileWidget
