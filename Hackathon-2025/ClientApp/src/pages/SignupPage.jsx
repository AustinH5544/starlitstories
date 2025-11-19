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
    const [showPwd, setShowPwd] = useState(false);
    const [showConfirm, setShowConfirm] = useState(false);
    const [isUsernameFocused, setIsUsernameFocused] = useState(false);
    const [isPasswordFocused, setIsPasswordFocused] = useState(false);

    const { login } = useAuth()
    const navigate = useNavigate()
    const [isLoading, setIsLoading] = useState(false);
    const [status, setStatus] = useState("");

    // Password rules
    const ruleSet = { ...defaultRuleSet };
    const { requirements, allMet: allRequirementsMet } = checkPassword(password, ruleSet);
    const labels = requirementLabels(ruleSet);
    const passwordsMatch = confirm.length > 0 && password === confirm;

    // Username rules
    const usernameRuleSet = { ...defaultUsernameRuleSet };
    const { requirements: usernameReqs, allMet: isUsernameValid } = checkUsername(username, usernameRuleSet);
    const usernameLabels = usernameRequirementLabels(usernameRuleSet);

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (isLoading) return;

        setStatus("");

        // Normalize to lowercase for storage/uniqueness, but allow mixed-case input
        const uname = username.trim().toLowerCase();

        // Validate BEFORE setting isLoading to avoid sticky disabled button
        if (!isUsernameValid) {
            setStatus("Please meet all username requirements before continuing.");
            return;
        }
        if (!allRequirementsMet) {
            setStatus("Please meet all password requirements before continuing.");
            return;
        }
        if (!passwordsMatch) {
            setStatus("Passwords don't match.");
            return;
        }

        setIsLoading(true);

        try {
            // Always create as FREE by default
            const { data } = await api.post("/auth/signup", {
                email,
                username: uname,
                password,
            });

            if (data?.requiresVerification) {
                alert("Account created! Please verify your email to continue.");
                navigate("/login");
                return;
            }

            // Log in and send to profile (they can upgrade from there)
            login(data);
            navigate("/profile");
             } catch (err) {
            console.error(err);

            const resp = err.response;

            if (!resp) {
                if (err.code === "ECONNABORTED") {
                    setStatus("Our servers are waking up. Please try again in a few seconds.");
                } else {
                    setStatus("Server is starting up or unavailable. Please try again shortly.");
                }
            } else if ([502, 503, 504].includes(resp.status)) {
                setStatus("Server is waking up. Please wait a moment and retry.");
            } else {
                const data = resp.data;
                let msg = "Signup failed.";

                if (typeof data === "string") {
                    msg = data;
                } else if (data && typeof data.message === "string") {
                    msg = data.message;
                } else if (data && typeof data.title === "string") {
                    msg = data.title;
                } else if (data && data.errors && typeof data.errors === "object") {
                    // Flatten ASP.NET Core validation errors
                    const all = Object.values(data.errors)
                        .flat()
                        .filter(Boolean);
                    if (all.length) {
                        msg = all.join(" ");
                    }
                }

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
                            pattern="[A-Za-z0-9_.]{3,32}"
                            title="3–32 characters: letters (A–Z, a–z), numbers (0–9), underscore (_), or dot (.)"
                            aria-describedby={isUsernameFocused ? "username-reqs" : undefined}
                            autoComplete="username"
                            onFocus={() => setIsUsernameFocused(true)}
                            onBlur={() => setIsUsernameFocused(false)}
                        />
                        {/* Show username checklist only while focused, with smooth expansion */}
                        <div className={`collapsible ${isUsernameFocused ? "open" : ""}`}>
                            <PasswordChecklist
                                requirements={usernameReqs}
                                labels={usernameLabels}
                                id="username-reqs"
                            />
                        </div>
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
                                aria-describedby={isPasswordFocused ? "password-reqs" : undefined}
                                autoComplete="new-password"
                                onFocus={() => setIsPasswordFocused(true)}
                                onBlur={() => setIsPasswordFocused(false)}
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

                        {/* Show password checklist only while focused, with smooth expansion */}
                        <div className={`collapsible ${isPasswordFocused ? "open" : ""}`}>
                            <PasswordChecklist requirements={requirements} labels={labels} id="password-reqs" />
                        </div>
                        {/* If passwords match but missing requirements, show them under Password */}
                        {passwordsMatch && !allRequirementsMet && (
                            <div className="missing-reqs">
                                <p className="missing-reqs-title">Missing requirements:</p>
                                <ul>
                                    {Object.entries(requirements)
                                        .filter(([, met]) => !met)
                                        .map(([key]) => (
                                            <li key={key} className="missing-req">
                                                {labels[key]}
                                            </li>
                                        ))}
                                </ul>
                            </div>
                        )}
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

                        {/* Match indicator always visible */}
                        <PasswordMatch confirmValue={confirm} isMatch={passwordsMatch} />
                    </div>

                    {status && <div className="signup-status" aria-live="polite">{status}</div>}

                    <button
                        type="submit"
                        className="signup-button"
                        disabled={
                            isLoading ||
                            !isUsernameValid ||
                            !allRequirementsMet ||
                            !passwordsMatch
                        }
                        title={
                            !isUsernameValid
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
                    <p className="post-signup-upgrade-hint">
                        After signup, visit your Profile to upgrade anytime.
                    </p>
                </div>
            </div>
        </div>
    )
}

export default SignupPage