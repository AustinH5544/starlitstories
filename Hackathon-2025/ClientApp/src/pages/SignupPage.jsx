"use client"

import { useState } from "react"
import axios from "axios"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"
//import { loadStripe } from '@stripe/stripe-js';
import "./SignupPage.css"

const SignupPage = () => {
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")
    const [confirm, setConfirm] = useState("")
    const [membership, setMembership] = useState("")
    const [status, setStatus] = useState("")
    const [isLoading, setIsLoading] = useState(false)

    const { login } = useAuth()
    const navigate = useNavigate()

    const handleSubmit = async (e) => {
        e.preventDefault()
        setStatus("")

        if (password !== confirm) {
            setStatus("Passwords don't match")
            return
        }

        if (!membership) {
            setStatus("Please select a membership plan.")
            return
        }

        setIsLoading(true)

        if (membership === "free") {
            // Free flow — signup and require email verification
            try {
                const response = await axios.post("http://localhost:5275/api/auth/signup", {
                    email,
                    password,
                    membership,
                })

                setStatus("Account created successfully! Please check your email to verify your account.")

                // Redirect to verification page after a short delay
                setTimeout(() => {
                    navigate(`/verify-email?email=${encodeURIComponent(email)}`)
                }, 2000)
            } catch (err) {
                console.error(err)
                setStatus(err.response?.data || "Signup failed")
            }
        } else {
            // Paid flow — redirect to Stripe
            try {
                const { data } = await axios.post("http://localhost:5275/api/payments/create-checkout-session", {
                    email,
                    membership,
                })
                window.location.href = data.checkoutUrl
            } catch (err) {
                console.error(err)
                setStatus("Error starting payment session")
            }
        }

        setIsLoading(false)
    }

    const getStatusClass = () => {
        if (status.includes("successfully")) return "success"
        if (status.includes("failed") || status.includes("Error") || status.includes("don't match")) return "error"
        return "info"
    }

    return (
        <div className="signup-page">
            <div className="stars"></div>
            <div className="small-clouds"></div>
            <div className="clouds"></div>

            <div className="signup-container">
                <div className="signup-header">
                    <h1 className="signup-title">Join the Magic</h1>
                    <p className="signup-subtitle">Start creating personalized bedtime stories for your little ones</p>
                </div>

                <form onSubmit={handleSubmit} className="signup-form">
                    <div className="form-group">
                        <label htmlFor="email">Email Address</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="Enter your email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Password</label>
                        <input
                            id="password"
                            type="password"
                            placeholder="Create a password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="confirm">Confirm Password</label>
                        <input
                            id="confirm"
                            type="password"
                            placeholder="Confirm your password"
                            value={confirm}
                            onChange={(e) => setConfirm(e.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="form-group">
                        <label htmlFor="membership">Choose Your Plan</label>
                        <div className="membership-options">
                            <div
                                className={`membership-card ${membership === "free" ? "selected" : ""} ${isLoading ? "disabled" : ""}`}
                                onClick={() => !isLoading && setMembership("free")}
                            >
                                <div className="plan-icon">📖</div>
                                <h3>Free</h3>
                                <p className="plan-price">$0/month</p>
                                <p className="plan-description">Perfect for trying out our service</p>
                                <ul className="plan-features">
                                    <li>✓ 1 personalized story</li>
                                    <li>✓ Basic customization</li>
                                    <li>✓ Digital format only</li>
                                </ul>
                                <input
                                    type="radio"
                                    name="membership"
                                    value="free"
                                    checked={membership === "free"}
                                    onChange={(e) => setMembership(e.target.value)}
                                    style={{ display: "none" }}
                                    disabled={isLoading}
                                />
                            </div>

                            <div
                                className={`membership-card ${membership === "pro" ? "selected" : ""} ${isLoading ? "disabled" : ""}`}
                                onClick={() => !isLoading && setMembership("pro")}
                            >
                                <div className="plan-icon">✨</div>
                                <h3>Pro</h3>
                                <p className="plan-price">$5/month</p>
                                <p className="plan-description">Great for regular storytelling</p>
                                <ul className="plan-features">
                                    <li>✓ 10 stories per month</li>
                                    <li>✓ Advanced customization</li>
                                    <li>✓ High-quality illustrations</li>
                                    <li>✓ Download & share</li>
                                </ul>
                                <input
                                    type="radio"
                                    name="membership"
                                    value="pro"
                                    checked={membership === "pro"}
                                    onChange={(e) => setMembership(e.target.value)}
                                    style={{ display: "none" }}
                                    disabled={isLoading}
                                />
                            </div>

                            <div
                                className={`membership-card premium ${membership === "premium" ? "selected" : ""} ${isLoading ? "disabled" : ""}`}
                                onClick={() => !isLoading && setMembership("premium")}
                            >
                                <div className="plan-badge">Most Popular</div>
                                <div className="plan-icon">🌟</div>
                                <h3>Premium</h3>
                                <p className="plan-price">$15/month</p>
                                <p className="plan-description">Perfect for families who love stories</p>
                                <ul className="plan-features">
                                    <li>✓ 50 stories per month</li>
                                    <li>✓ Premium illustrations</li>
                                    <li>✓ Multiple characters</li>
                                    <li>✓ Print-ready format</li>
                                    <li>✓ Priority support</li>
                                </ul>
                                <input
                                    type="radio"
                                    name="membership"
                                    value="premium"
                                    checked={membership === "premium"}
                                    onChange={(e) => setMembership(e.target.value)}
                                    style={{ display: "none" }}
                                    disabled={isLoading}
                                />
                            </div>
                        </div>
                    </div>

                    {status && <div className={`signup-status ${getStatusClass()}`}>{status}</div>}

                    <button type="submit" className="signup-button" disabled={isLoading}>
                        <span className="button-icon">{isLoading ? "⏳" : "🚀"}</span>
                        <span>{isLoading ? "Creating Account..." : "Start My Journey"}</span>
                    </button>
                </form>

                <div className="signup-footer">
                    <p>
                        Already have an account? <a href="/login">Sign in here</a>
                    </p>
                </div>
            </div>
        </div>
    )
}

export default SignupPage
