"use client"

import { useState } from "react"
import api from "../api"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"
import "./SignupPage.css"
import EyeOpen from "../assets/eye-open.svg";
import EyeClosed from "../assets/eye-closed.svg";
import useWarmup from "../hooks/useWarmup";

import { checkPassword, requirementLabels, defaultRuleSet } from "../utils/passwordRules"
import PasswordChecklist from "../components/PasswordChecklist"
import PasswordMatch from "../components/PasswordMatch"

// NEW: JS username rules
import {
    checkUsername,
    usernameRequirementLabels,
    defaultUsernameRuleSet,
} from "../utils/usernameRules"

const SignupPage = () => {
    useWarmup();
    const [username, setUsername] = useState("")
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")
    const [confirm, setConfirm] = useState("")
    const [membership, setMembership] = useState("")
    const [showPwd, setShowPwd] = useState(false);
    const [showConfirm, setShowConfirm] = useState(false);
    const { login } = useAuth()
    const navigate = useNavigate()
    const [isLoading, setIsLoading] = useState(false);
    const [status, setStatus] = useState("");

    // Password rules
    const ruleSet = { ...defaultRuleSet };
    const { requirements, allMet: allRequirementsMet } = checkPassword(password, ruleSet);
    const labels = requirementLabels(ruleSet);
    const passwordsMatch = confirm.length > 0 && password === confirm;

    // Username rules (live checklist like password)
    const usernameRuleSet = { ...defaultUsernameRuleSet };
    const { requirements: usernameReqs, allMet: isUsernameValid } = checkUsername(username, usernameRuleSet);
    const usernameLabels = usernameRequirementLabels(usernameRuleSet);

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (isLoading) return;

        setIsLoading(true);
        setStatus("");

        try {
            // Accept mixed case input, but normalize to lowercase for storage/uniqueness
            const uname = username.trim().toLowerCase();

            if (!isUsernameValid) {
                alert("Please meet all username requirements before continuing.");
                return;
            }

            if (!allRequirementsMet) {
                alert("Please meet all password requirements before continuing.");
                return;
            }

            if (!passwordsMatch) {
                alert("Passwords don't match.");
                return;
            }

            if (!membership) {
                alert("Please select a membership plan.");
                return;
            }

            if (membership === "free") {
                const { data } = await api.post("/auth/signup", {
                    email,
                    username: uname,
                    password,
                    membership,
                });

                if (data?.requiresVerification) {
                    alert("Account created! Please verify your email to continue.");
                    navigate("/login");
                    return;
                }

                login(data);
                navigate("/profile");
            } else {
                const { data } = await api.post("/payments/create-checkout-session", {
                    email,
                    username: uname,
                    membership,
                });
                window.location.href = data.checkoutUrl;
            }
        } catch (err) {
            console.error(err);
            if (!err.response) {
                if (err.code === "ECONNABORTED") {
                    setStatus("Our servers are waking up. Please try again in a few seconds.");
                } else {
                    setStatus("Server is starting up or unavailable. Please try again shortly.");
                }
            } else if ([502, 503, 504].includes(err.response.status)) {
                setStatus("Server is waking up. Please wait a moment and retry.");
            } else {
                const msg = err.response?.data?.message || err.response?.data || "Signup failed";
                setStatus(msg);
            }
        } finally {
            setIsLoading(false);
        }
    };

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
                    {/* Username */}
                    <div className="form-group">
                        <label htmlFor="username">Username</label>
                        <input
                            id="username"
                            type="text"
                            placeholder="Choose a username"
                            value={username}
                            onChange={(e) => setUsername(e.target.value)}
                            required
                            pattern="^[A-Za-z0-9._-]{3,24}$"
                            title="3–24 chars: letters (A–Z or a–z), numbers (0–9), dot, underscore, hyphen"
                            aria-describedby="username-reqs"
                            autoComplete="username"
                        />
                        <PasswordChecklist
                            requirements={usernameReqs}
                            labels={usernameLabels}
                            id="username-reqs"
                        />
                    </div>

                    {/* Email */}
                    <div className="form-group">
                        <label htmlFor="email">Email Address</label>
                        <input
                            id="email"
                            type="email"
                            placeholder="Enter your email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            required
                            autoComplete="email"
                        />
                    </div>

                    {/* Password */}
                    <div className="form-group">
                        <label htmlFor="password">Password</label>
                        <div className="input-with-toggle">
                            <input
                                id="password"
                                className="input"
                                type={showPwd ? "text" : "password"}
                                placeholder="Create a password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                aria-describedby="password-reqs"
                                autoComplete="new-password"
                            />
                            <button
                                type="button"
                                className="toggle-visibility"
                                aria-label={showPwd ? "Hide password" : "Show password"}
                                aria-pressed={showPwd}
                                onClick={() => setShowPwd(s => !s)}
                                title={showPwd ? "Hide password" : "Show password"}
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

                        <PasswordChecklist requirements={requirements} labels={labels} id="password-reqs" />
                    </div>

                    {/* Confirm Password */}
                    <div className="form-group">
                        <label htmlFor="confirm">Confirm Password</label>
                        <div className="input-with-toggle">
                            <input
                                id="confirm"
                                className="input"
                                type={showConfirm ? "text" : "password"}
                                placeholder="Confirm your password"
                                value={confirm}
                                onChange={(e) => setConfirm(e.target.value)}
                                required
                                autoComplete="new-password"
                            />
                            <button
                                type="button"
                                className="toggle-visibility"
                                aria-label={showConfirm ? "Hide confirmation" : "Show confirmation"}
                                aria-pressed={showConfirm}
                                aria-controls="confirm"
                                onClick={() => setShowConfirm((s) => !s)}
                                title={showConfirm ? "Hide confirmation" : "Show confirmation"}
                            >
                                <img
                                    src={showConfirm ? EyeClosed : EyeOpen}
                                    alt=""
                                    className="icon-eye"
                                    width="22"
                                    height="22"
                                    draggable="false"
                                />
                            </button>
                        </div>

                        <PasswordMatch confirmValue={confirm} isMatch={passwordsMatch} />
                    </div>

                    <div className="form-group">
                        <label htmlFor="membership">Choose Your Plan</label>
                        <div className="membership-options">
                            <div
                                className={`membership-card ${membership === "free" ? "selected" : ""}`}
                                onClick={() => setMembership("free")}
                                role="radio"
                                aria-checked={membership === "free"}
                                tabIndex={0}
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
                                />
                            </div>

                            <div
                                className={`membership-card ${membership === "pro" ? "selected" : ""}`}
                                onClick={() => setMembership("pro")}
                                role="radio"
                                aria-checked={membership === "pro"}
                                tabIndex={0}
                            >
                                <div className="plan-icon">✨</div>
                                <h3>Pro</h3>
                                <p className="plan-price">$4/month</p>
                                <p className="plan-description">Great for regular storytelling</p>
                                <ul className="plan-features">
                                    <li>✓ 5 stories per month</li>
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
                                />
                            </div>

                            <div
                                className={`membership-card premium ${membership === "premium" ? "selected" : ""}`}
                                onClick={() => setMembership("premium")}
                                role="radio"
                                aria-checked={membership === "premium"}
                                tabIndex={0}
                            >
                                <div className="plan-badge">Most Popular</div>
                                <div className="plan-icon">🌟</div>
                                <h3>Premium</h3>
                                <p className="plan-price">$8/month</p>
                                <p className="plan-description">Perfect for families who love stories</p>
                                <ul className="plan-features">
                                    <li>✓ 11 stories per month</li>
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
                                />
                            </div>
                        </div>
                    </div>

                    {status && <div className="signup-status" aria-live="polite">{status}</div>}

                    <button
                        type="submit"
                        className="signup-button"
                        disabled={
                            isLoading ||
                            !isUsernameValid ||       // block until username passes
                            !allRequirementsMet ||
                            !passwordsMatch ||
                            !membership
                        }
                        title={
                            !membership
                                ? "Select a plan to continue"
                                : !isUsernameValid
                                    ? "Meet all username requirements to continue"
                                    : !allRequirementsMet
                                        ? "Meet all password requirements to continue"
                                        : !passwordsMatch
                                            ? "Passwords must match to continue"
                                            : undefined
                        }
                    >
                        <span className="button-icon">{isLoading ? "⏳" : "🚀"}</span>
                        <span>{isLoading ? "Warming Up..." : "Start My Journey"}</span>
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
