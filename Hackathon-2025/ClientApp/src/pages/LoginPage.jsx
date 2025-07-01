"use client"

import { useState } from "react"
import axios from "axios"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"
import "./LoginPage.css"

const LoginPage = () => {
    const [email, setEmail] = useState("")
    const [password, setPassword] = useState("")

    const { login } = useAuth()
    const navigate = useNavigate()

    const handleSubmit = async (e) => {
        e.preventDefault()

        try {
            const response = await axios.post("http://localhost:5275/api/auth/login", {
                email,
                password,
            })

            login(response.data) // { email, membership }
            navigate("/profile")
        } catch (err) {
            console.error(err)
            alert(err.response?.data || "Login failed")
        }
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
                            onChange={(e) => setEmail(e.target.value)}
                            required
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
                        />
                    </div>

                    <button type="submit" className="login-button">
                        <span className="button-icon">🔮</span>
                        <span>Sign In</span>
                    </button>
                </form>

                <div className="login-footer">
                    <p>
                        Don't have an account? <a href="/signup">Create one here</a>
                    </p>
                    <p>
                        <a href="/forgot-password" className="forgot-link">
                            Forgot your password?
                        </a>
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