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
            features: [
                "1 personalized story",
                "1 saved character",
                "Standard character creator",
                "Digital format only",
            ],
            disabled: user?.membership === "free",
        },
        {
            id: "pro",
            name: "Pro",
            price: "$4/month",
            icon: "✨",
            description: "Great for regular storytelling",
            features: [
                "5 stories per month",
                "5 saved characters",
                "Advanced character creation",
                "High-quality illustrations",
                "Download & share",
            ],
            disabled: ["pro", "premium", "storybook"].includes(user?.membership),
        },
        {
            id: "premium",
            name: "Premium",
            price: "$8/month",
            icon: "🌟",
            description: "Perfect for families who love stories",
            features: [
                "11 stories per month",
                "10 saved characters",
                "Advanced character creation",
                "Premium illustrations",
                "Print-ready format",
                "Priority support queue",
            ],
            badge: "Most Popular",
            disabled: ["premium", "storybook"].includes(user?.membership),
        },
        {
            id: "storybook",
            name: "Storybook",
            price: "$14/month",
            icon: "📚",
            description: "For families who want a full picture-book keepsake each month",
            features: [
                "11 regular stories per month",
                "1 Super Story per month",
                "32-page full children's storybook",
                "10 saved characters",
                "Advanced character creation",
                "Premium illustrations",
                "Print-ready format",
            ],
            disabled: user?.membership === "storybook",
        },
    ]

    const handleUpgrade = async () => {
        if (!selectedPlan || selectedPlan === "free") {
            return;
        }

        setIsProcessing(true);

        try {
            // Map UI ids to numeric enum values (Free=0, Pro=1, Premium=2, Storybook=3)
            let membershipValue;

            switch (selectedPlan) {
                case "pro":
                    membershipValue = 1;
                    break;
                case "premium":
                    membershipValue = 2;
                    break;
                case "storybook":
                    membershipValue = 3;
                    break;
                case "free":
                default:
                    membershipValue = 0;
                    break;
            }

            const { data } = await api.post("/payments/create-checkout-session", {
                membership: membershipValue,
            });

            window.location.href = data.checkoutUrl;
        } catch (err) {
            console.error(err);
            const resp = err.response;
            if (resp?.data) {
                console.log("Checkout error payload:", resp.data);
                if (resp.data.errors) {
                    console.log("Validation errors:", resp.data.errors);
                }
            }
            alert("Error starting payment session");
            setIsProcessing(false);
        }
    };

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
                    <p className="upgrade-subtitle">Unlock more stories, more saved characters, and the full character creator.</p>
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

                    <div className="upgrade-benefits-grid">
                        <div className="upgrade-benefit-card">
                            <div className="upgrade-benefit-icon">📚</div>
                            <h4>More Stories</h4>
                            <p>Create more personalized adventures for your children with higher monthly limits.</p>
                        </div>

                        <div className="upgrade-benefit-card">
                            <div className="upgrade-benefit-icon">🎨</div>
                            <h4>Character Creator</h4>
                            <p>Paid plans unlock advanced character creation, while Free keeps the standard creator.</p>
                        </div>

                        <div className="upgrade-benefit-card">
                            <div className="upgrade-benefit-icon">💾</div>
                            <h4>Saved Characters</h4>
                            <p>Keep 1 saved character on Free, 5 on Pro, and 10 on Premium or Storybook.</p>
                        </div>

                        <div className="upgrade-benefit-card">
                            <div className="upgrade-benefit-icon">🖨️</div>
                            <h4>Print & Share</h4>
                            <p>Download your stories in print-ready format to create physical keepsakes.</p>
                        </div>
                    </div>
                </div>

                <div className="upgrade-footer">
                    <p>
                        Multiple characters are currently disabled for all plans. If your membership changes, we keep saved characters in your account and only pause new saves until you are back under the active plan limit.
                    </p>
                    <p>
                        Priority support is planned, but not live yet. A practical rollout is a dedicated premium inbox or tagged support queue with faster first-response targets. Questions about upgrading? <a href="mailto:support@StarlitStories.app">Contact our support team</a>
                    </p>
                </div>
            </div>
        </div>
    )
}

export default UpgradePage
