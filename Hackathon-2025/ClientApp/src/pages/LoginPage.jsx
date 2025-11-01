"use client"

import { useEffect, useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import api from "../api"
import { useAuth } from "../context/AuthContext"
import "./LoginPage.css"
import EyeOpen from "../assets/eye-open.svg";
import EyeClosed from "../assets/eye-closed.svg";
import useWarmup from "../hooks/useWarmup";

const SESSION_KEY = "needsVerification"

const LoginPage = () => {
    useWarmup();
    const [identifier, setIdentifier] = useState("")
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")
    const [status, setStatus] = useState("")
    const [isLoading, setIsLoading] = useState(false)
    const [needsVerification, setNeedsVerification] = useState(false)
    const [showPwd, setShowPwd] = useState(false);
    const [dots, setDots] = useState("");

    const { login } = useAuth()
    const navigate = useNavigate()

    useEffect(() => {
        if (!isLoading) {
            setDots("");
            return;
        }

        const interval = setInterval(() => {
            setDots((prev) => (prev.length < 3 ? prev + "." : ""));
        }, 500);

        return () => clearInterval(interval);
    }, [isLoading]);

    useEffect(() => {
        const persisted = sessionStorage.getItem(SESSION_KEY)
        if (persisted === "true") setNeedsVerification(true)
        return () => sessionStorage.removeItem(SESSION_KEY)
    }, [])

    useEffect(() => {
        sessionStorage.setItem(SESSION_KEY, needsVerification ? "true" : "false")
    }, [needsVerification])

    useEffect(() => {
        if (identifier.includes("@")) setEmail(identifier.trim())
    }, [identifier])

    const handleSubmit = async (e) => {
        e.preventDefault()
        setStatus("")
        setIsLoading(true)

        try {
            const response = await api.post(
                "/auth/login",
                { identifier, password },
                { skipAuth401Handler: true }
            )
            login(response.data)
            sessionStorage.removeItem(SESSION_KEY)
            navigate("/profile")
        } catch (err) {
            console.error(err)
            if (!err.response) {
                if (err.code === "ECONNABORTED")
                    setStatus("Our servers are waking up. Please try again in a few seconds.")
                else
                    setStatus("We couldn’t reach the server. It may be starting up—please try again shortly.")
                return
            }

            const { status: httpStatus, data } = err.response
            if ([502, 503, 504, 522, 523, 524].includes(httpStatus)) {
                setStatus("Server is starting up or temporarily unavailable. Please try again in a moment.")
                return
            }

            if (data?.requiresVerification) {
                setNeedsVerification(true)
                if (data?.email) setEmail(data.email)
                else if (identifier.includes("@")) setEmail(identifier)
                setStatus("Please verify your email before logging in. Check your inbox for a verification link.")
                return
            }

            if (httpStatus === 401 || httpStatus === 403) {
                setNeedsVerification(false)
                setStatus(data?.message || "Login failed. Please check your credentials.")
                return
            }

            if (httpStatus >= 400 && httpStatus < 500) {
                setStatus(data?.message || "Login failed. Please check your input and try again.")
                return
            }

            setStatus("Unexpected error while signing in. Please try again.")
        } finally {
            setIsLoading(false)
        }
    }

    const resendVerification = async () => {
        const targetEmail = (email || (identifier.includes("@") ? identifier : "")).trim()
        if (!targetEmail) {
            setStatus("Please enter your email address first.")
            return
        }

        setIsLoading(true)
        try {
            await api.post(
                "/auth/resend-verification",
                { email: targetEmail },
                { skipAuth401Handler: true }
            )
            setNeedsVerification(true)
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
                        <label htmlFor="identifier">Email or Username</label>
                        <input
                            id="identifier"
                            type="text"
                            placeholder="Enter your email or username"
                            value={identifier}
                            onChange={(e) => setIdentifier(e.target.value.trim())}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Password</label>
                        <div className="input-with-toggle">
                            <input
                                id="password"
                                type={showPwd ? "text" : "password"}
                                placeholder="Enter your password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                disabled={isLoading}
                            />
                            <button
                                type="button"
                                className="toggle-visibility"
                                aria-label={showPwd ? "Hide password" : "Show password"}
                                aria-pressed={showPwd}
                                onClick={() => setShowPwd((s) => !s)}
                            >
                                <img
                                    src={showPwd ? EyeClosed : EyeOpen}
                                    alt=""
                                    className="icon-eye"
                                    width="22"
                                    height="22"
                                />
                            </button>
                        </div>
                    </div>

                    {status && <div className={`login-status ${getStatusClass()}`}>{status}</div>}

                    {needsVerification && (
                        <div className="verification-actions">
                            <button
                                type="button"
                                onClick={resendVerification}
                                disabled={isLoading}
                                className="resend-button"
                            >
                                {isLoading ? "Sending..." : "📤 Resend Verification Email"}
                            </button>
                        </div>
                    )}

                    <button type="submit" className="login-button" disabled={isLoading}>
                        <span className="button-icon">{isLoading ? "⏳" : "🔮"}</span>
                        <span>{isLoading ? `Signing in${dots}` : "Sign In"}</span>
                    </button>
                </form>

                <div className="login-footer">
                    <p>Don't have an account? <Link to="/signup">Create one here</Link></p>
                    <p><Link to="/forgot-password" className="forgot-link">Forgot your password?</Link></p>
                </div>

                <div className="login-features">
                    <div className="feature-item"><span className="feature-icon">📚</span><span>Access your saved stories</span></div>
                    <div className="feature-item"><span className="feature-icon">✨</span><span>Continue creating magic</span></div>
                    <div className="feature-item"><span className="feature-icon">👨‍👩‍👧‍👦</span><span>Share with your family</span></div>
                </div>
            </div>
        </div>
    )
}

export default LoginPage