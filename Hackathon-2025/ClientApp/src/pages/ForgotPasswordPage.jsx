"use client"

import { useState } from "react"
import { useNavigate } from "react-router-dom"
import api from "../api"
import "./ForgotPasswordPage.css"

const ForgotPasswordPage = () => {
    const [email, setEmail] = useState("")
    const [isLoading, setIsLoading] = useState(false)
    const [isSubmitted, setIsSubmitted] = useState(false)
    const [error, setError] = useState("")
    const navigate = useNavigate()

    const handleSubmit = async (e) => {
        e.preventDefault()
        setIsLoading(true)
        setError("")

        try {
            await api.post("/auth/forgot-password", {
                email,
            })
            setIsSubmitted(true)
        } catch (err) {
            console.error("Forgot password error:", err)
            setError(err?.response?.data || "Something went wrong. Please try again.")
        } finally {
            setIsLoading(false)
        }
    }

    if (isSubmitted) {
        return (
            <div className="forgot-password-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="forgot-password-container">
                    <div className="success-content">
                        <div className="success-icon">📧</div>
                        <h1 className="success-title">Check Your Email</h1>
                        <p className="success-message">
                            If an account with <strong>{email}</strong> exists, we've sent you a password reset link.
                        </p>
                        <p className="success-submessage">
                            Please check your email and follow the instructions to reset your password. The link will expire in 1
                            hour.
                        </p>

                        <div className="success-actions">
                            <button onClick={() => navigate("/login")} className="back-to-login-btn">
                                <span className="button-icon">🔮</span>
                                <span>Back to Sign In</span>
                            </button>
                            <button onClick={() => setIsSubmitted(false)} className="try-again-btn">
                                <span className="button-icon">🔄</span>
                                <span>Try Different Email</span>
                            </button>
                        </div>

                        <div className="help-section">
                            <h3>Didn't receive the email?</h3>
                            <ul className="help-list">
                                <li>Check your spam or junk folder</li>
                                <li>Make sure you entered the correct email address</li>
                                <li>Wait a few minutes for the email to arrive</li>
                            </ul>
                            <p className="contact-support">
                                Still having trouble? <a href="mailto:support@cozypages.com">Contact our support team</a>
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        )
    }

    return (
        <div className="forgot-password-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="forgot-password-container">
                <div className="forgot-password-header">
                    <button onClick={() => navigate("/login")} className="back-button">
                        <span>←</span> Back to Sign In
                    </button>
                    <h1 className="forgot-password-title">Reset Your Password</h1>
                    <p className="forgot-password-subtitle">
                        Enter your email address and we'll send you a link to reset your password
                    </p>
                </div>

                <form onSubmit={handleSubmit} className="forgot-password-form">
                    <div className="form-group">
                        <label htmlFor="email">Email Address</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="Enter your email address"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            required
                            className="form-input"
                            disabled={isLoading}
                        />
                    </div>

                    {error && (
                        <div className="error-message">
                            <span className="error-icon">⚠️</span>
                            <span>{error}</span>
                        </div>
                    )}

                    <button type="submit" className="reset-button" disabled={isLoading}>
                        {isLoading ? (
                            <>
                                <div className="loading-spinner">
                                    <div className="spinner"></div>
                                </div>
                                <span>Sending Reset Link...</span>
                            </>
                        ) : (
                            <>
                                <span className="button-icon">📧</span>
                                <span>Send Reset Link</span>
                            </>
                        )}
                    </button>
                </form>

                <div className="forgot-password-footer">
                    <div className="security-note">
                        <div className="security-icon">🔒</div>
                        <div className="security-content">
                            <h4>Security Note</h4>
                            <p>
                                For your security, we'll only send reset instructions to the email address associated with your
                                CozyPages account.
                            </p>
                        </div>
                    </div>

                    <div className="alternative-options">
                        <p>
                            Remember your password? <a href="/login">Sign in here</a>
                        </p>
                        <p>
                            Don't have an account? <a href="/signup">Create one for free</a>
                        </p>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default ForgotPasswordPage