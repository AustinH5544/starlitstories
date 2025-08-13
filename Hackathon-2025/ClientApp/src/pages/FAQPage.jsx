"use client"

import { useState } from "react"
import { Link } from "react-router-dom"
import "./FAQPage.css"

const FAQPage = () => {
    const [searchTerm, setSearchTerm] = useState("")
    const [activeFilter, setActiveFilter] = useState("All")
    const [openItems, setOpenItems] = useState(new Set())

    const faqData = [
        {
            id: 1,
            category: "General",
            question: "What is CozyPages?",
            answer:
                "CozyPages is an AI-powered storytelling platform that creates personalized children's stories with beautiful illustrations. Simply provide character details, themes, and preferences, and our AI will generate a unique story just for you.",
        },
        {
            id: 2,
            category: "General",
            question: "How does the AI story generation work?",
            answer:
                "Our AI uses advanced language models to create engaging narratives based on your inputs. You can specify characters, settings, themes, and story length. The AI then crafts a coherent, age-appropriate story with accompanying illustrations.",
        },
        {
            id: 3,
            category: "Account",
            question: "Do I need to create an account to use CozyPages?",
            answer:
                "Yes, you need to create a free account to save your stories, access your story library, and use our story generation features. Registration is quick and only requires an email address.",
        },
        {
            id: 4,
            category: "Account",
            question: "How do I verify my email address?",
            answer:
                "After signing up, check your email for a verification link from CozyPages. Click the link to verify your account. If you don't see the email, check your spam folder or request a new verification email from your profile page.",
        },
        {
            id: 5,
            category: "Stories",
            question: "How long does it take to generate a story?",
            answer:
                "Story generation typically takes 30-60 seconds, depending on the complexity and length requested. Image generation may take an additional 15-30 seconds per illustration.",
        },
        {
            id: 6,
            category: "Stories",
            question: "Can I edit or customize my generated stories?",
            answer:
                "Currently, stories are generated as complete works. However, you can create new variations by adjusting your character descriptions, themes, or story prompts and generating a new story.",
        },
        {
            id: 7,
            category: "Stories",
            question: "What age groups are the stories suitable for?",
            answer:
                "Our stories are primarily designed for children ages 3-12. You can specify the target age range when creating a story, and our AI will adjust the vocabulary, themes, and complexity accordingly.",
        },
        {
            id: 8,
            category: "Billing",
            question: "What's included in the free plan?",
            answer:
                "The free plan includes 3 story generations per month, basic character customization, and access to your story library. Stories include simple illustrations and are perfect for trying out the platform.",
        },
        {
            id: 9,
            category: "Billing",
            question: "What are the premium plan benefits?",
            answer:
                "Premium plans offer unlimited story generation, advanced character customization, high-quality illustrations, priority generation speed, and the ability to download stories as PDFs.",
        },
        {
            id: 10,
            category: "Billing",
            question: "Can I cancel my subscription anytime?",
            answer:
                "Yes, you can cancel your subscription at any time from your account settings. You'll continue to have premium access until the end of your current billing period.",
        },
        {
            id: 11,
            category: "Technical",
            question: "What browsers are supported?",
            answer:
                "CozyPages works best on modern browsers including Chrome, Firefox, Safari, and Edge. We recommend keeping your browser updated for the best experience.",
        },
        {
            id: 12,
            category: "Technical",
            question: "Can I download my stories?",
            answer:
                "Premium subscribers can download their stories as high-quality PDF files, perfect for printing or sharing. Free users can view and share stories online through our platform.",
        },
        {
            id: 13,
            category: "Technical",
            question: "Is my data secure and private?",
            answer:
                "Yes, we take privacy seriously. Your stories and personal information are encrypted and stored securely. We never share your content with third parties, and you maintain full ownership of your generated stories.",
        },
        {
            id: 14,
            category: "Stories",
            question: "Can I create stories in different languages?",
            answer:
                "Currently, CozyPages generates stories in English. We're working on adding support for additional languages in future updates.",
        },
        {
            id: 15,
            category: "General",
            question: "How can I share my stories with others?",
            answer:
                "You can share your stories by sending a direct link to family and friends. Premium users can also download PDFs to share via email or print physical copies.",
        },
    ]

    const categories = ["All", "General", "Account", "Stories", "Billing", "Technical"]

    const filteredFAQs = faqData.filter((faq) => {
        const matchesSearch =
            faq.question.toLowerCase().includes(searchTerm.toLowerCase()) ||
            faq.answer.toLowerCase().includes(searchTerm.toLowerCase())
        const matchesFilter = activeFilter === "All" || faq.category === activeFilter
        return matchesSearch && matchesFilter
    })

    const toggleItem = (id) => {
        const newOpenItems = new Set(openItems)
        if (newOpenItems.has(id)) {
            newOpenItems.delete(id)
        } else {
            newOpenItems.add(id)
        }
        setOpenItems(newOpenItems)
    }

    return (
        <div className="faq-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="faq-container">
                <div className="faq-header">
                    <h1 className="faq-title">Frequently Asked Questions</h1>
                    <p className="faq-subtitle">
                        Find answers to common questions about CozyPages and our AI storytelling platform
                    </p>

                    <div className="faq-search">
                        <span className="search-icon">🔍</span>
                        <input
                            type="text"
                            className="search-input"
                            placeholder="Search questions..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                    </div>

                    <div className="faq-filters">
                        {categories.map((category) => (
                            <button
                                key={category}
                                className={`filter-btn ${activeFilter === category ? "active" : ""}`}
                                onClick={() => setActiveFilter(category)}
                            >
                                {category}
                            </button>
                        ))}
                    </div>
                </div>

                <div className="faq-list">
                    {filteredFAQs.length > 0 ? (
                        filteredFAQs.map((faq) => (
                            <div key={faq.id} className={`faq-item ${openItems.has(faq.id) ? "open" : ""}`}>
                                <button className="faq-question" onClick={() => toggleItem(faq.id)}>
                                    <div>
                                        <div className="faq-category">{faq.category}</div>
                                        <div>{faq.question}</div>
                                    </div>
                                    <span className="faq-icon">▼</span>
                                </button>
                                <div className="faq-answer">{faq.answer}</div>
                            </div>
                        ))
                    ) : (
                        <div className="no-results">
                            <div className="no-results-icon">🔍</div>
                            <h3>No questions found</h3>
                            <p>Try adjusting your search terms or filter selection.</p>
                        </div>
                    )}
                </div>

                <div className="support-link">
                    <h3>Still need help?</h3>
                    <p>Can't find what you're looking for? Our support team is here to help!</p>
                    <Link to="/support" className="support-btn">
                        <span>💬</span>
                        <span>Contact Support</span>
                    </Link>
                </div>
            </div>
        </div>
    )
}

export default FAQPage
