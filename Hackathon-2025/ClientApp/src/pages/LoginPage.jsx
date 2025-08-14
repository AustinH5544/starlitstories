"use client"

import { useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import api from "../api"
import { useAuth } from "../context/AuthContext"
import "./LoginPage.css"

const LoginPage = () => {
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")
    const [status, setStatus] = useState("")
    const [isLoading, setIsLoading] = useState(false)
    const [needsVerification, setNeedsVerification] = useState(false)

    const { login } = useAuth()
    const navigate = useNavigate()

    const handleSubmit = async (e) => {
        e.preventDefault()
        setStatus("")
        setNeedsVerification(false)
        setIsLoading(true)

        try {
            const response = await api.post("/auth/login", {
                email,
                password,
            })

            login(response.data)
            navigate("/profile")
        } catch (err) {
            console.error(err)

            if (!err.response) {
                // Covers cold starts, server down, network errors, etc.
                setStatus("Unable to reach the server. Please try again in a moment.")
                return
            }

            const errorData = err.response.data

            if (errorData?.requiresVerification) {
                setNeedsVerification(true)
                setStatus("Please verify your email before logging in. Check your inbox for a verification link.")
            } else {
                setStatus(errorData?.message || "Login failed. Please check your credentials.")
            }
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
            await api.post("/auth/resend-verification", {
                email,
            })
            setStatus("Verification email sent! Please check your inbox.")
            setNeedsVerification(false)
        } catch (err) {
            console.error(err)
            setStatus("Failed to resend verification email. Please try again.")
        } finally {
            setIsLoading(false)
        }
    }

    const getStatusClass = () => {
        if (status.toLowerCase().includes("sent") || status.toLowerCase().includes("check your inbox")) return "success"
        if (status.includes("failed") || status.includes("Failed") || status.includes("verify your email")) return "error"
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