"use client"

import { useState } from "react"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"
import api from "../api"
import "./UpgradePage.css"

const UpgradePage = () => {
    const { user } = useAuth()
    const navigate = useNavigate()
    const [selectedPlan, setSelectedPlan] = useState("")
    const [isProcessing, setIsProcessing] = useState(false)

    const plans = [
        {
            id: "free",
            name: "Free",
            price: "$0/month",
            icon: "📖",
            description: "Perfect for trying out our service",
            features: ["1 personalized story", "Basic customization", "Digital format only"],
            disabled: user?.membership === "free",
        },
        {
            id: "pro",
            name: "Pro",
            price: "$5/month",
            icon: "✨",
            description: "Great for regular storytelling",
            features: ["10 stories per month", "Advanced customization", "High-quality illustrations", "Download & share"],
            disabled: user?.membership === "pro" || user?.membership === "premium",
        },
        {
            id: "premium",
            name: "Premium",
            price: "$15/month",
            icon: "🌟",
            description: "Perfect for families who love stories",
            features: [
                "50 stories per month",
                "Premium illustrations",
                "Multiple characters",
                "Print-ready format",
                "Priority support",
            ],
            badge: "Most Popular",
            disabled: user?.membership === "premium",
        },
    ]

    const handleUpgrade = async () => {
        if (!selectedPlan || selectedPlan === "free") {
            return
        }

        setIsProcessing(true)

        try {
            const { data } = await api.post("/payments/create-checkout-session", {
                membership: selectedPlan,
            })
            window.location.href = data.checkoutUrl
        } catch (err) {
            console.error(err)
            alert("Error starting payment session")
            setIsProcessing(false)
        }
    }

    const getCurrentPlanFeatures = () => {
        const currentPlan = plans.find((p) => p.id === user?.membership)
        return currentPlan?.features || []
    }

    if (!user) {
        return (
            <div className="upgrade-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>
                <div className="upgrade-container">
                    <h2>Please log in to view upgrade options.</h2>
                    <button onClick={() => navigate("/login")} className="login-redirect-btn">
                        <span className="button-icon">🔮</span>
                        <span>Go to Login</span>
                    </button>
                </div>
            </div>
        )
    }

    return (
        <div className="upgrade-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="upgrade-container">
                <div className="upgrade-header">
                    <button onClick={() => navigate("/profile")} className="back-button">
                        <span>←</span> Back to Profile
                    </button>
                    <h1 className="upgrade-title">Upgrade Your Plan</h1>
                    <p className="upgrade-subtitle">Unlock more magical stories and premium features</p>
                </div>

                <div className="current-plan-section">
                    <h2 className="section-title">
                        <span className="section-icon">⭐</span>
                        Your Current Plan
                    </h2>
                    <div className="current-plan-card">
                        <div className="plan-info">
                            <div className="plan-icon-large">{plans.find((p) => p.id === user.membership)?.icon || "📖"}</div>
                            <div className="plan-details">
                                <h3>{plans.find((p) => p.id === user.membership)?.name || "Free"} Plan</h3>
                                <p className="plan-price-current">{plans.find((p) => p.id === user.membership)?.price || "$0/month"}</p>
                            </div>
                        </div>
                        <div className="current-features">
                            <h4>What you have:</h4>
                            <ul>
                                {getCurrentPlanFeatures().map((feature, idx) => (
                                    <li key={idx}>✓ {feature}</li>
                                ))}
                            </ul>
                        </div>
                    </div>
                </div>

                <div className="upgrade-plans-section">
                    <h2 className="section-title">
                        <span className="section-icon">🚀</span>
                        Available Upgrades
                    </h2>

                    <div className="plans-grid">
                        {plans.map((plan) => (
                            <div
                                key={plan.id}
                                className={`plan-card ${selectedPlan === plan.id ? "selected" : ""} ${plan.disabled ? "disabled" : ""
                                    } ${plan.badge ? "premium" : ""}`}
                                onClick={() => !plan.disabled && setSelectedPlan(plan.id)}
                            >
                                {plan.badge && <div className="plan-badge">{plan.badge}</div>}
                                {plan.disabled && user.membership === plan.id && <div className="current-badge">Current Plan</div>}

                                <div className="plan-icon">{plan.icon}</div>
                                <h3>{plan.name}</h3>
                                <p className="plan-price">{plan.price}</p>
                                <p className="plan-description">{plan.description}</p>

                                <ul className="plan-features">
                                    {plan.features.map((feature, idx) => (
                                        <li key={idx}>✓ {feature}</li>
                                    ))}
                                </ul>

                                {plan.disabled && user.membership !== plan.id && (
                                    <div className="downgrade-note">Contact support to downgrade</div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>

                {selectedPlan && selectedPlan !== "free" && (
                    <div className="upgrade-actions">
                        <div className="upgrade-summary">
                            <h3>Ready to upgrade to {plans.find((p) => p.id === selectedPlan)?.name}?</h3>
                            <p>You'll be redirected to our secure payment processor to complete your upgrade.</p>
                        </div>

                        <button onClick={handleUpgrade} className="upgrade-button" disabled={isProcessing}>
                            <span className="button-icon">{isProcessing ? "⏳" : "🚀"}</span>
                            <span>
                                {isProcessing ? "Processing..." : `Upgrade to ${plans.find((p) => p.id === selectedPlan)?.name}`}
                            </span>
                        </button>
                    </div>
                )}

                <div className="upgrade-benefits">
                    <h2 className="section-title">
                        <span className="section-icon">💫</span>
                        Why Upgrade?
                    </h2>

                    <div className="benefits-grid">
                        <div className="benefit-card">
                            <div className="benefit-icon">📚</div>
                            <h4>More Stories</h4>
                            <p>Create more personalized adventures for your children with higher monthly limits.</p>
                        </div>

                        <div className="benefit-card">
                            <div className="benefit-icon">🎨</div>
                            <h4>Premium Quality</h4>
                            <p>Access to higher quality illustrations and advanced customization options.</p>
                        </div>

                        <div className="benefit-card">
                            <div className="benefit-icon">👨‍👩‍👧‍👦</div>
                            <h4>Family Features</h4>
                            <p>Support for multiple characters and family-oriented story themes.</p>
                        </div>

                        <div className="benefit-card">
                            <div className="benefit-icon">🖨️</div>
                            <h4>Print & Share</h4>
                            <p>Download your stories in print-ready format to create physical keepsakes.</p>
                        </div>
                    </div>
                </div>

                <div className="upgrade-footer">
                    <p>
                        Questions about upgrading? <a href="mailto:support@cozypages.com">Contact our support team</a>
                    </p>
                </div>
            </div>
        </div>
    )
}

export default UpgradePage