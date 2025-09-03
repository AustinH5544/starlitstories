"use client"

import { useState } from "react"
import { Link } from "react-router-dom"
import "./SupportPage.css"

const SupportPage = () => {
    const [formData, setFormData] = useState({
        name: "",
        email: "",
        category: "",
        priority: "medium",
        subject: "",
        message: "",
    })
    const [isSubmitting, setIsSubmitting] = useState(false)
    const [showSuccess, setShowSuccess] = useState(false)

    const handleInputChange = (e) => {
        const { name, value } = e.target
        setFormData((prev) => ({
            ...prev,
            [name]: value,
        }))
    }

    const handleSubmit = async (e) => {
        e.preventDefault()
        setIsSubmitting(true)

        // Simulate form submission
        setTimeout(() => {
            setIsSubmitting(false)
            setShowSuccess(true)
            setFormData({
                name: "",
                email: "",
                category: "",
                priority: "medium",
                subject: "",
                message: "",
            })

            // Hide success message after 5 seconds
            setTimeout(() => setShowSuccess(false), 5000)
        }, 1000)
    }

    return (
        <div className="support-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="support-container">
                <div className="support-header">
                    <h1 className="support-title">Support Center</h1>
                    <p className="support-subtitle">
                        We're here to help! Get in touch with our support team or find answers to your questions.
                    </p>

                    <div className="status-banner">
                        <span className="status-icon">✅</span>
                        <strong>All systems operational</strong> - Our services are running smoothly
                    </div>
                </div>

                <div className="support-content">
                    <div className="contact-form-section">
                        <h2 className="section-title">
                            <span>📝</span>
                            Send us a Message
                        </h2>

                        <form className="contact-form" onSubmit={handleSubmit}>
                            <div className="form-row">
                                <div className="form-group">
                                    <label className="form-label">Name *</label>
                                    <input
                                        type="text"
                                        name="name"
                                        className="form-input"
                                        value={formData.name}
                                        onChange={handleInputChange}
                                        required
                                        placeholder="Your full name"
                                    />
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Email *</label>
                                    <input
                                        type="email"
                                        name="email"
                                        className="form-input"
                                        value={formData.email}
                                        onChange={handleInputChange}
                                        required
                                        placeholder="your@email.com"
                                    />
                                </div>
                            </div>

                            <div className="form-row">
                                <div className="form-group">
                                    <label className="form-label">Category</label>
                                    <select
                                        name="category"
                                        className="form-select"
                                        value={formData.category}
                                        onChange={handleInputChange}
                                    >
                                        <option value="">Select a category</option>
                                        <option value="technical">Technical Issue</option>
                                        <option value="billing">Billing & Payments</option>
                                        <option value="account">Account Management</option>
                                        <option value="stories">Story Generation</option>
                                        <option value="feature">Feature Request</option>
                                        <option value="other">Other</option>
                                    </select>
                                </div>
                                <div className="form-group">
                                    <label className="form-label">Priority</label>
                                    <select
                                        name="priority"
                                        className="form-select"
                                        value={formData.priority}
                                        onChange={handleInputChange}
                                    >
                                        <option value="low">Low</option>
                                        <option value="medium">Medium</option>
                                        <option value="high">High</option>
                                        <option value="urgent">Urgent</option>
                                    </select>
                                </div>
                            </div>

                            <div className="form-group">
                                <label className="form-label">Subject *</label>
                                <input
                                    type="text"
                                    name="subject"
                                    className="form-input"
                                    value={formData.subject}
                                    onChange={handleInputChange}
                                    required
                                    placeholder="Brief description of your issue"
                                />
                            </div>

                            <div className="form-group">
                                <label className="form-label">Message *</label>
                                <textarea
                                    name="message"
                                    className="form-textarea"
                                    value={formData.message}
                                    onChange={handleInputChange}
                                    required
                                    placeholder="Please provide as much detail as possible about your issue or question..."
                                />
                            </div>

                            <button type="submit" className="submit-btn" disabled={isSubmitting}>
                                {isSubmitting ? (
                                    <>
                                        <span>⏳</span>
                                        <span>Sending...</span>
                                    </>
                                ) : (
                                    <>
                                        <span>📤</span>
                                        <span>Send Message</span>
                                    </>
                                )}
                            </button>

                            {showSuccess && (
                                <div className="success-message">
                                    <span>✅</span>
                                    <strong>Message sent successfully!</strong>
                                    <p>We'll get back to you within 24 hours.</p>
                                </div>
                            )}
                        </form>
                    </div>

                    <div className="contact-methods-section">
                        <h2 className="section-title">
                            <span>📞</span>
                            Other Ways to Reach Us
                        </h2>

                        <div className="contact-methods">
                            <a href="mailto:support@StarlitStories.com" className="contact-method">
                                <div className="contact-icon">📧</div>
                                <div className="contact-info">
                                    <h4>Email Support</h4>
                                    <p>support@StarlitStories.com</p>
                                </div>
                            </a>

                            <div className="contact-method">
                                <div className="contact-icon">💬</div>
                                <div className="contact-info">
                                    <h4>Live Chat</h4>
                                    <p>Available 9 AM - 6 PM EST</p>
                                </div>
                            </div>

                            <div className="contact-method">
                                <div className="contact-icon">📱</div>
                                <div className="contact-info">
                                    <h4>Phone Support</h4>
                                    <p>1-800-COZY-PAGES</p>
                                </div>
                            </div>

                            <a
                                href="https://twitter.com/StarlitStories"
                                className="contact-method"
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                <div className="contact-icon">🐦</div>
                                <div className="contact-info">
                                    <h4>Twitter</h4>
                                    <p>@StarlitStories</p>
                                </div>
                            </a>

                            <a
                                href="https://facebook.com/StarliStories"
                                className="contact-method"
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                <div className="contact-icon">📘</div>
                                <div className="contact-info">
                                    <h4>Facebook</h4>
                                    <p>StarlitStories Official</p>
                                </div>
                            </a>
                        </div>
                    </div>
                </div>

                <div className="quick-help">
                    <h2 className="section-title">
                        <span>🚀</span>
                        Quick Help & Resources
                    </h2>

                    <div className="help-links">
                        <Link to="/faq" className="help-link">
                            <span className="help-icon">❓</span>
                            <span>FAQ</span>
                        </Link>

                        <a href="/user-guide" className="help-link">
                            <span className="help-icon">📖</span>
                            <span>User Guide</span>
                        </a>

                        <a href="/video-tutorials" className="help-link">
                            <span className="help-icon">🎥</span>
                            <span>Video Tutorials</span>
                        </a>

                        <a href="/community" className="help-link">
                            <span className="help-icon">👥</span>
                            <span>Community Forum</span>
                        </a>

                        <a href="/status" className="help-link">
                            <span className="help-icon">📊</span>
                            <span>System Status</span>
                        </a>

                        <a href="/api-docs" className="help-link">
                            <span className="help-icon">⚙️</span>
                            <span>API Documentation</span>
                        </a>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default SupportPage