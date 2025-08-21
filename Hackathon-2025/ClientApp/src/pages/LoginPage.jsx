"use client"

import { useEffect, useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import api from "../api"
import { useAuth } from "../context/AuthContext"
import "./LoginPage.css"

const SESSION_KEY = "needsVerification"

const LoginPage = () => {
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")
    const [status, setStatus] = useState("")
    const [isLoading, setIsLoading] = useState(false)
    const [needsVerification, setNeedsVerification] = useState(false)

    const { login } = useAuth()
    const navigate = useNavigate()

    // Restore "needs verification" on mount; clear it when leaving the page
    useEffect(() => {
        const persisted = sessionStorage.getItem(SESSION_KEY)
        if (persisted === "true") setNeedsVerification(true)

        return () => {
            // Clear when navigating away so the banner doesn't stick across pages
            sessionStorage.removeItem(SESSION_KEY)
        }
    }, [])

    // Keep sessionStorage in sync while on this page
    useEffect(() => {
        sessionStorage.setItem(SESSION_KEY, needsVerification ? "true" : "false")
    }, [needsVerification])

    const handleSubmit = async (e) => {
        e.preventDefault()
        setStatus("")
        setIsLoading(true) // do NOT clear needsVerification here

        try {
            const response = await api.post(
                "/auth/login",
                { email, password },
                { skipAuth401Handler: true }
            )

            // successful login
            login(response.data)
            // clear persisted flag on success
            sessionStorage.removeItem(SESSION_KEY)
            navigate("/profile")
        } catch (err) {
            console.error(err)

            // --- Network / cold start / gateway errors ---
            // Axios "no response" (e.g., DNS fail, CORS block, server cold start)
            if (!err.response) {
                // Axios may set code to ECONNABORTED on timeout
                if (err.code === "ECONNABORTED") {
                    setStatus("Our servers are waking up. Please try again in a few seconds.")
                } else {
                    setStatus("We couldn’t reach the server. It may be starting up—please try again shortly.")
                }
                return
            }

            const { status, data } = err.response

            // Gateway / server warming / transient backend errors
            if ([502, 503, 504, 522, 523, 524].includes(status)) {
                setStatus("Server is starting up or temporarily unavailable. Please try again in a moment.")
                return
            }

            // Email verification flow
            if (data?.requiresVerification) {
                setNeedsVerification(true)
                setStatus("Please verify your email before logging in. Check your inbox for a verification link.")
                return
            }

            // Auth errors (bad credentials, 401/403) — fall back to API message if present
            if (status === 401 || status === 403) {
                setNeedsVerification(false)
                setStatus(data?.message || "Login failed. Please check your credentials.")
                return
            }

            // Generic fallback for other 4xx
            if (status >= 400 && status < 500) {
                setStatus(data?.message || "Login failed. Please check your input and try again.")
                return
            }

            // Any other unexpected status
            setStatus("Unexpected error while signing in. Please try again.")
        } finally {
            setIsLoading(false)
        }
    }

    const resendVerification = async () => {
        if (!email) {
            setStatus("Please enter your email address first.")
            return
        }

        setIsLoading(true)
        try {
            await api.post(
                "/auth/resend-verification",
                { email },
                { skipAuth401Handler: true }
            )
            setNeedsVerification(true) // keep the banner/CTA visible
            setStatus("Verification email sent! Please check your inbox.")
        } catch (err) {
            console.error(err)
            setStatus("Failed to resend verification email. Please try again.")
        } finally {
            setIsLoading(false)
        }
    }

    const getStatusClass = () => {
        const s = status.toLowerCase()
        if (s.includes("sent") || s.includes("check your inbox")) return "success"
        if (s.includes("failed") || s.includes("verify your email")) return "error"
        return "info"
    }

    return (
        <div className="login-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="login-container">
                <div className="login-header">
                    <h1 className="login-title">Welcome Back</h1>
                    <p className="login-subtitle">Sign in to continue your magical storytelling journey</p>
                </div>

                <form onSubmit={handleSubmit} className="login-form">
                    <div className="form-group">
                        <label htmlFor="email">Email Address</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="Enter your email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value.trim())}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Password</label>
                        <input
                            id="password"
                            type="password"
                            placeholder="Enter your password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    {status && <div className={`login-status ${getStatusClass()}`}>{status}</div>}

                    {needsVerification && (
                        <div className="verification-actions">
                            <button
                                type="button"
                                onClick={resendVerification}
                                disabled={isLoading || !email}
                                className="resend-button"
                            >
                                {isLoading ? "Sending..." : "📤 Resend Verification Email"}
                            </button>
                        </div>
                    )}

                    <button type="submit" className="login-button" disabled={isLoading}>
                        <span className="button-icon">{isLoading ? "⏳" : "🔮"}</span>
                        <span>{isLoading ? "Signing In..." : "Sign In"}</span>
                    </button>
                </form>

                <div className="login-footer">
                    <p>
                        Don't have an account? <Link to="/signup">Create one here</Link>
                    </p>
                    <p>
                        <Link to="/forgot-password" className="forgot-link">
                            Forgot your password?
                        </Link>
                    </p>
                </div>

                <div className="login-features">
                    <div className="feature-item">
                        <span className="feature-icon">📚</span>
                        <span>Access your saved stories</span>
                    </div>
                    <div className="feature-item">
                        <span className="feature-icon">✨</span>
                        <span>Continue creating magic</span>
                    </div>
                    <div className="feature-item">
                        <span className="feature-icon">👨‍👩‍👧‍👦</span>
                        <span>Share with your family</span>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default LoginPage