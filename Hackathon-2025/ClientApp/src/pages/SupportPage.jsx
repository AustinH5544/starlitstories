"use client"

import { useState } from "react"
import { Link } from "react-router-dom"
import "./SupportPage.css"

const SupportPage = () => {
    const [formData, setFormData] = useState({
        name: "",
        email: "",
        subject: "",
        category: "",
        message: "",
        priority: "medium",
    })
    const [isSubmitting, setIsSubmitting] = useState(false)
    const [submitSuccess, setSubmitSuccess] = useState(false)

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

        try {
            // Simulate API call
            await new Promise((resolve) => setTimeout(resolve, 2000))
            setSubmitSuccess(true)
            setFormData({
                name: "",
                email: "",
                subject: "",
                category: "",
                message: "",
                priority: "medium",
            })
        } catch (error) {
            console.error("Error submitting support request:", error)
        } finally {
            setIsSubmitting(false)
        }
    }

    return (
        <div className="support-page">
            <div className="support-container">
                <div className="support-header">
                    <h1>Support Center</h1>
                    <p>We're here to help! Get in touch with our support team or find answers to your questions.</p>
                </div>

                <div className="status-banner">
                    <strong>🟢 All Systems Operational</strong> - Our services are running smoothly
                </div>

                <div className="support-content">
                    <div className="support-card">
                        <h2>
                            <span className="support-icon">📧</span>
                            Contact Support
                        </h2>

                        {submitSuccess && (
                            <div className="success-message">
                                <strong>Thank you!</strong> Your support request has been submitted. We'll get back to you within 24
                                hours.
                            </div>
                        )}

                        <form className="contact-form" onSubmit={handleSubmit}>
                            <div className="form-group">
                                <label htmlFor="name">Full Name *</label>
                                <input type="text" id="name" name="name" value={formData.name} onChange={handleInputChange} required />
                            </div>

                            <div className="form-group">
                                <label htmlFor="email">Email Address *</label>
                                <input
                                    type="email"
                                    id="email"
                                    name="email"
                                    value={formData.email}
                                    onChange={handleInputChange}
                                    required
                                />
                            </div>

                            <div className="form-group">
                                <label htmlFor="category">Category *</label>
                                <select id="category" name="category" value={formData.category} onChange={handleInputChange} required>
                                    <option value="">Select a category</option>
                                    <option value="account">Account Issues</option>
                                    <option value="billing">Billing & Payments</option>
                                    <option value="technical">Technical Problems</option>
                                    <option value="stories">Story Generation</option>
                                    <option value="feature">Feature Request</option>
                                    <option value="other">Other</option>
                                </select>
                            </div>

                            <div className="form-group">
                                <label htmlFor="priority">Priority</label>
                                <select id="priority" name="priority" value={formData.priority} onChange={handleInputChange}>
                                    <option value="low">Low</option>
                                    <option value="medium">Medium</option>
                                    <option value="high">High</option>
                                    <option value="urgent">Urgent</option>
                                </select>
                            </div>

                            <div className="form-group">
                                <label htmlFor="subject">Subject *</label>
                                <input
                                    type="text"
                                    id="subject"
                                    name="subject"
                                    value={formData.subject}
                                    onChange={handleInputChange}
                                    placeholder="Brief description of your issue"
                                    required
                                />
                            </div>

                            <div className="form-group">
                                <label htmlFor="message">Message *</label>
                                <textarea
                                    id="message"
                                    name="message"
                                    value={formData.message}
                                    onChange={handleInputChange}
                                    placeholder="Please provide as much detail as possible about your issue or question..."
                                    required
                                />
                            </div>

                            <button type="submit" className="submit-button" disabled={isSubmitting}>
                                {isSubmitting ? "Submitting..." : "Submit Request"}
                            </button>
                        </form>
                    </div>

                    <div className="support-card">
                        <h2>
                            <span className="support-icon">📞</span>
                            Get in Touch
                        </h2>

                        <div className="contact-info">
                            <div className="contact-method">
                                <div className="contact-method-icon">📧</div>
                                <div className="contact-method-info">
                                    <h3>Email Support</h3>
                                    <p>
                                        <a href="mailto:support@cozypages.com">support@cozypages.com</a>
                                    </p>
                                    <p>Response within 24 hours</p>
                                </div>
                            </div>

                            <div className="contact-method">
                                <div className="contact-method-icon">💬</div>
                                <div className="contact-method-info">
                                    <h3>Live Chat</h3>
                                    <p>Available Mon-Fri, 9 AM - 6 PM EST</p>
                                    <p>Click the chat bubble in the bottom right</p>
                                </div>
                            </div>

                            <div className="contact-method">
                                <div className="contact-method-icon">📱</div>
                                <div className="contact-method-info">
                                    <h3>Phone Support</h3>
                                    <p>+1 (555) 123-4567</p>
                                    <p>Premium subscribers only</p>
                                </div>
                            </div>

                            <div className="contact-method">
                                <div className="contact-method-icon">🐦</div>
                                <div className="contact-method-info">
                                    <h3>Social Media</h3>
                                    <p>
                                        <a href="https://twitter.com/cozypages" target="_blank" rel="noopener noreferrer">
                                            @CozyPages
                                        </a>
                                    </p>
                                    <p>Follow us for updates</p>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className="quick-help">
                        <h2>Quick Help</h2>
                        <div className="help-categories">
                            <Link to="/faq" className="help-category">
                                <div className="help-category-icon">❓</div>
                                <h3>FAQ</h3>
                                <p>Find answers to frequently asked questions</p>
                            </Link>

                            <div className="help-category">
                                <div className="help-category-icon">📚</div>
                                <h3>User Guide</h3>
                                <p>Learn how to use CozyPages effectively</p>
                            </div>

                            <div className="help-category">
                                <div className="help-category-icon">🎥</div>
                                <h3>Video Tutorials</h3>
                                <p>Watch step-by-step video guides</p>
                            </div>

                            <div className="help-category">
                                <div className="help-category-icon">🔧</div>
                                <h3>Troubleshooting</h3>
                                <p>Solve common technical issues</p>
                            </div>

                            <div className="help-category">
                                <div className="help-category-icon">💡</div>
                                <h3>Tips & Tricks</h3>
                                <p>Get the most out of your stories</p>
                            </div>

                            <div className="help-category">
                                <div className="help-category-icon">🔒</div>
                                <h3>Privacy & Security</h3>
                                <p>Learn about data protection</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default SupportPage
