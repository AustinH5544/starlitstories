"use client"

import { useState } from "react"
import { Helmet } from "react-helmet-async"
import SiteFooter from "../components/SiteFooter"
import "./SupportPage.css"

const SparklesIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 2l1.76 4.74L18.5 8.5l-4.74 1.76L12 15l-1.76-4.74L5.5 8.5l4.74-1.76L12 2zm7 11l.94 2.56L22.5 16.5l-2.56.94L19 20l-.94-2.56-2.56-.94 2.56-.94L19 13zm-14 1l.94 2.56L8.5 17.5l-2.56.94L5 21l-.94-2.56-2.56-.94 2.56-.94L5 14z" />
    </svg>
)

const MailIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M3 5h18a1 1 0 011 1v12a1 1 0 01-1 1H3a1 1 0 01-1-1V6a1 1 0 011-1zm0 2v.24l9 6.43 9-6.43V7H3zm18 11V9.7l-8.42 6.01a1 1 0 01-1.16 0L3 9.7V18h18z" />
    </svg>
)

const ClockIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 2a10 10 0 100 20 10 10 0 000-20zm1 5v4.59l3.3 3.3-1.42 1.41L11 12.41V7h2z" />
    </svg>
)

const CheckCircleIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 2a10 10 0 100 20 10 10 0 000-20zm-1 14l-4-4 1.41-1.41L11 13.17l5.59-5.58L18 9l-7 7z" />
    </svg>
)

const FormIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M6 3h9l5 5v13a1 1 0 01-1 1H6a2 2 0 01-2-2V5a2 2 0 012-2zm8 1.5V9h4.5L14 4.5zM8 12h8v-2H8v2zm0 4h8v-2H8v2z" />
    </svg>
)

const ArrowIcon = () => (
    <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12.29 6.71l1.42-1.42L20.41 12l-6.7 6.71-1.42-1.42L16.59 13H4v-2h12.59l-4.3-4.29z" />
    </svg>
)

const supportHighlights = [
    {
        icon: ClockIcon,
        title: "Fast follow-up",
        text: "Most questions get a reply within one business day.",
    },
    {
        icon: MailIcon,
        title: "Real help",
        text: "Tell us what happened and we will point you to the next step.",
    },
    {
        icon: CheckCircleIcon,
        title: "Story-friendly support",
        text: "Account, billing, and story issues all come through one place.",
    },
]

const contactMethods = [
    {
        icon: MailIcon,
        title: "Email support",
        detail: "support@StarlitStories.app",
        note: "Best for account help, bug reports, and billing questions.",
        href: "mailto:support@StarlitStories.app",
    },
    {
        icon: ClockIcon,
        title: "Response window",
        detail: "Usually under 24 hours",
        note: "Urgent issues are reviewed first when priority is marked high.",
    },
]

const requestTips = [
    "Tell us what you were trying to do.",
    "Include the page or feature where the problem happened.",
    "Share any error text you saw so we can reproduce it faster.",
]

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

            setTimeout(() => setShowSuccess(false), 5000)
        }, 1000)
    }

    return (
        <div className="support-page">
            <Helmet>
                <title>Support | Starlit Stories</title>
                <meta name="description" content="Need help with Starlit Stories? Contact our support team and we'll get you back to creating magical stories." />
                <link rel="canonical" href="https://starlitstories.app/support" />
                <meta property="og:title" content="Support | Starlit Stories" />
                <meta property="og:description" content="Need help with Starlit Stories? Contact our support team and we'll get you back to creating magical stories." />
                <meta property="og:type" content="website" />
                <meta property="og:url" content="https://starlitstories.app/support" />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content="Support | Starlit Stories" />
                <meta name="twitter:description" content="Need help with Starlit Stories? Contact our support team and we'll get you back to creating magical stories." />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
            </Helmet>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="support-container">
                <header className="support-hero">
                    <div className="support-hero-copy">
                        <h1 className="support-title">We'll help you get back to story time</h1>
                        <p className="support-subtitle">
                            Whether you hit a bug, have an account question, or need help with a purchase, send us a note and we'll take it from there.
                        </p>
                    </div>

                    <div className="support-status-card">
                        <div className="status-badge">
                            <span className="status-badge-icon">
                                <CheckCircleIcon />
                            </span>
                            All systems operational
                        </div>
                        <p>Story creation and account services are currently running normally.</p>
                    </div>
                </header>

                <section className="support-highlights" aria-label="Support highlights">
                    {supportHighlights.map(({ icon: Icon, title, text }) => (
                        <article key={title} className="support-highlight-card">
                            <span className="support-highlight-icon">
                                <Icon />
                            </span>
                            <h2>{title}</h2>
                            <p>{text}</p>
                        </article>
                    ))}
                </section>

                <div className="support-content">
                    <section className="support-panel support-form-panel">
                        <div className="section-heading">
                            <span className="section-heading-icon">
                                <FormIcon />
                            </span>
                            <div>
                                <h2>Send us a message</h2>
                                <p>Fill out the form and we'll route it to the right place.</p>
                            </div>
                        </div>

                        <form className="contact-form" onSubmit={handleSubmit}>
                            <div className="form-row">
                                <div className="form-group">
                                    <label className="form-label" htmlFor="support-name">Name *</label>
                                    <input
                                        id="support-name"
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
                                    <label className="form-label" htmlFor="support-email">Email *</label>
                                    <input
                                        id="support-email"
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
                                    <label className="form-label" htmlFor="support-category">Category</label>
                                    <select
                                        id="support-category"
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
                                    <label className="form-label" htmlFor="support-priority">Priority</label>
                                    <select
                                        id="support-priority"
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
                                <label className="form-label" htmlFor="support-subject">Subject *</label>
                                <input
                                    id="support-subject"
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
                                <label className="form-label" htmlFor="support-message">Message *</label>
                                <textarea
                                    id="support-message"
                                    name="message"
                                    className="form-textarea"
                                    value={formData.message}
                                    onChange={handleInputChange}
                                    required
                                    placeholder="Tell us what happened, what you expected, and anything else that will help us investigate."
                                />
                            </div>

                            <button type="submit" className="submit-btn" disabled={isSubmitting}>
                                <span className="submit-btn-icon">
                                    {isSubmitting ? <ClockIcon /> : <ArrowIcon />}
                                </span>
                                <span>{isSubmitting ? "Sending..." : "Send message"}</span>
                            </button>

                            {showSuccess && (
                                <div className="success-message" role="status">
                                    <span className="success-message-icon">
                                        <CheckCircleIcon />
                                    </span>
                                    <div>
                                        <strong>Message sent successfully.</strong>
                                        <p>We'll get back to you within 24 hours.</p>
                                    </div>
                                </div>
                            )}
                        </form>
                    </section>

                    <aside className="support-sidebar">
                        <section className="support-panel support-contact-panel">
                            <div className="section-heading">
                                <span className="section-heading-icon">
                                    <MailIcon />
                                </span>
                                <div>
                                    <h2>Other ways to reach us</h2>
                                    <p>Choose the channel that fits your question best.</p>
                                </div>
                            </div>

                            <div className="contact-methods">
                                {contactMethods.map(({ icon: Icon, title, detail, note, href }) => {
                                    const content = (
                                        <>
                                            <span className="contact-icon">
                                                <Icon />
                                            </span>
                                            <div className="contact-info">
                                                <h3>{title}</h3>
                                                <p className="contact-detail">{detail}</p>
                                                <p className="contact-note">{note}</p>
                                            </div>
                                        </>
                                    )

                                    return href ? (
                                        <a key={title} href={href} className="contact-method">
                                            {content}
                                        </a>
                                    ) : (
                                        <div key={title} className="contact-method">
                                            {content}
                                        </div>
                                    )
                                })}
                            </div>
                        </section>

                        <section className="support-panel support-tips-panel">
                            <div className="section-heading">
                                <span className="section-heading-icon">
                                    <SparklesIcon />
                                </span>
                                <div>
                                    <h2>What to include</h2>
                                    <p>A few details help us solve issues much faster.</p>
                                </div>
                            </div>

                            <ul className="request-tips">
                                {requestTips.map((tip) => (
                                    <li key={tip}>
                                        <span className="request-tip-icon">
                                            <CheckCircleIcon />
                                        </span>
                                        <span>{tip}</span>
                                    </li>
                                ))}
                            </ul>
                        </section>
                    </aside>
                </div>
            </div>
            <SiteFooter />
        </div>
    )
}

export default SupportPage
