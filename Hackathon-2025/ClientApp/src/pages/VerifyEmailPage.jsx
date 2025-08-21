"use client"

import { useState, useEffect } from "react"
import { useSearchParams, useNavigate, Link } from "react-router-dom"
import api from "../api"
import { useAuth } from "../context/AuthContext"
import "./VerifyEmailPage.css"

const VerifyEmailPage = () => {
    const [searchParams] = useSearchParams()
    const navigate = useNavigate()
    const { login } = useAuth()

    const email = searchParams.get("email")
    const token = searchParams.get("token")

    const [status, setStatus] = useState("")
    const [isVerifying, setIsVerifying] = useState(false)
    const [isResending, setIsResending] = useState(false)
    const [verificationComplete, setVerificationComplete] = useState(false)

    useEffect(() => {
        if (email && token) {
            verifyEmail()
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [email, token])

    const verifyEmail = async () => {
        setIsVerifying(true)
        try {
            // Backend currently validates by token; sending email is harmless and future-proof
            const response = await api.post(
                "/auth/verify-email",
                { email, token },
                { skipAuth401Handler: true }
            )

            setStatus("Email verified successfully! Redirecting to your dashboard...")
            setVerificationComplete(true)

            // Log the user in automatically (your VerifyEmail returns token/email/membership)
            login(response.data)

            setTimeout(() => {
                navigate("/profile", { replace: true })
            }, 1500)
        } catch (err) {
            console.error(err)
            const msg = err?.response?.data?.message || "Verification failed. The link may be invalid or expired."
            setStatus(msg)
        } finally {
            setIsVerifying(false)
        }
    }

    const resendVerification = async () => {
        if (!email) {
            setStatus("Email address is required to resend verification.")
            return
        }

        setIsResending(true)
        try {
            await api.post(
                "/auth/resend-verification",
                { email },
                { skipAuth401Handler: true }
            )
            setStatus("Verification email sent! Please check your inbox.")
        } catch (err) {
            console.error(err)
            const msg = err?.response?.data?.message || "Failed to resend verification email."
            setStatus(msg)
        } finally {
            setIsResending(false)
        }
    }

    const getStatusClass = () => {
        const s = status.toLowerCase()
        if (verificationComplete) return "success"
        if (s.includes("failed") || s.includes("invalid") || s.includes("expired")) return "error"
        return "info"
    }

    return (
        <div className="verify-email-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="verify-email-container">
                <div className="verify-email-icon">{isVerifying ? "⏳" : verificationComplete ? "✅" : "📧"}</div>

                <h1 className="verify-email-title">
                    {isVerifying ? "Verifying Email..." : verificationComplete ? "Email Verified!" : "Verify Your Email"}
                </h1>

                {email && !verificationComplete && (
                    <>
                        <p className="verify-email-message">
                            {token
                                ? "We're verifying your email address. Please wait..."
                                : "Please check your email and click the verification link we sent to:"}
                        </p>
                        <div className="verify-email-email">{email}</div>
                    </>
                )}

                {verificationComplete && (
                    <p className="verify-email-message">
                        Welcome to Bedtime Stories! You can now start creating magical stories for your little ones.
                    </p>
                )}

                {!email && !token && (
                    <p className="verify-email-message">
                        This page is used to verify email addresses. If you need to verify your email, please check your inbox for a
                        verification link.
                    </p>
                )}

                {status && <div className={`verify-email-status ${getStatusClass()}`}>{status}</div>}

                {!verificationComplete && (
                    <div className="verify-email-actions">
                        {email && !token && (
                            <button onClick={resendVerification} disabled={isResending} className="verify-email-button">
                                {isResending ? (
                                    <>
                                        <div className="loading-spinner"></div>
                                        Sending...
                                    </>
                                ) : (
                                    <>📤 Resend Verification Email</>
                                )}
                            </button>
                        )}

                        <Link to="/login" className="verify-email-button secondary">
                            🔑 Back to Login
                        </Link>
                    </div>
                )}

                {verificationComplete && (
                    <div className="verify-email-actions">
                        <Link to="/profile" className="verify-email-button">
                            🚀 Go to Dashboard
                        </Link>
                    </div>
                )}
            </div>
        </div>
    )
}

export default VerifyEmailPage