"use client"

import { useState, useMemo } from "react"
import { Link } from "react-router-dom"
import "./FAQPage.css"

const FAQPage = () => {
    const [searchTerm, setSearchTerm] = useState("")
    const [activeCategory, setActiveCategory] = useState("all")
    const [openItems, setOpenItems] = useState(new Set())

    const faqData = [
        {
            id: 1,
            category: "general",
            question: "What is CozyPages?",
            answer:
                "CozyPages is an AI-powered platform that creates personalized children's stories with beautiful illustrations. Simply provide some details about your child, and our AI will generate a unique, engaging story tailored just for them.",
        },
        {
            id: 2,
            category: "general",
            question: "How does the story generation work?",
            answer:
                "Our advanced AI analyzes the information you provide about your child (name, age, interests, etc.) and creates a custom story. The process includes generating both the narrative text and accompanying illustrations to bring the story to life.",
        },
        {
            id: 3,
            category: "account",
            question: "Do I need to create an account?",
            answer:
                "Yes, creating an account allows you to save your stories, access them anytime, and manage your preferences. It also enables us to provide a more personalized experience and keep track of your story library.",
        },
        {
            id: 4,
            category: "account",
            question: "How do I verify my email address?",
            answer:
                "After signing up, you'll receive a verification email. Click the verification link in the email to activate your account. If you don't see the email, check your spam folder or request a new verification email from your profile page.",
        },
        {
            id: 5,
            category: "stories",
            question: "How long does it take to generate a story?",
            answer:
                "Story generation typically takes 2-5 minutes, depending on the complexity and current system load. You'll see a progress indicator while your story is being created, and you'll be notified when it's ready.",
        },
        {
            id: 6,
            category: "stories",
            question: "Can I customize the stories?",
            answer:
                "Yes! You can specify your child's name, age, favorite activities, pets, and other preferences. You can also choose themes, story length, and even include family members or friends as characters in the story.",
        },
        {
            id: 7,
            category: "stories",
            question: "What age groups are the stories suitable for?",
            answer:
                "Our stories are designed for children aged 2-12 years. The AI automatically adjusts the vocabulary, story complexity, and themes based on the age you specify to ensure age-appropriate content.",
        },
        {
            id: 8,
            category: "billing",
            question: "How much does CozyPages cost?",
            answer:
                "We offer both free and premium plans. Free users can generate a limited number of stories per month, while premium subscribers get unlimited story generation, priority processing, and access to premium themes and features.",
        },
        {
            id: 9,
            category: "billing",
            question: "Can I cancel my subscription anytime?",
            answer:
                "Yes, you can cancel your subscription at any time from your account settings. Your premium features will remain active until the end of your current billing period, and you can still access all previously generated stories.",
        },
        {
            id: 10,
            category: "technical",
            question: "What file formats are the stories available in?",
            answer:
                "Stories can be viewed online in our interactive reader, downloaded as PDF files for printing, or saved as digital books. Premium users also get access to additional formats like EPUB for e-readers.",
        },
        {
            id: 11,
            category: "technical",
            question: "Can I print the stories?",
            answer:
                "All stories are optimized for printing. You can download them as high-quality PDF files that are perfect for home printing or professional printing services.",
        },
        {
            id: 12,
            category: "technical",
            question: "Is my data safe and private?",
            answer:
                "Yes, we take privacy seriously. All personal information is encrypted and stored securely. We never share your data with third parties, and you can delete your account and all associated data at any time.",
        },
        {
            id: 13,
            category: "stories",
            question: "Can I edit the generated stories?",
            answer:
                "Currently, stories are generated as complete works. However, we're working on editing features that will allow you to make minor adjustments to character names, settings, and other story elements.",
        },
        {
            id: 14,
            category: "technical",
            question: "What browsers are supported?",
            answer:
                "CozyPages works on all modern browsers including Chrome, Firefox, Safari, and Edge. For the best experience, we recommend using the latest version of your preferred browser.",
        },
        {
            id: 15,
            category: "billing",
            question: "Do you offer refunds?",
            answer:
                "We offer a 30-day money-back guarantee for premium subscriptions. If you're not satisfied with the service, contact our support team within 30 days of your purchase for a full refund.",
        },
    ]

    const categories = [
        { id: "all", name: "All Questions" },
        { id: "general", name: "General" },
        { id: "account", name: "Account" },
        { id: "stories", name: "Stories" },
        { id: "billing", name: "Billing" },
        { id: "technical", name: "Technical" },
    ]

    const filteredFAQs = useMemo(() => {
        return faqData.filter((faq) => {
            const matchesSearch =
                faq.question.toLowerCase().includes(searchTerm.toLowerCase()) ||
                faq.answer.toLowerCase().includes(searchTerm.toLowerCase())
            const matchesCategory = activeCategory === "all" || faq.category === activeCategory
            return matchesSearch && matchesCategory
        })
    }, [searchTerm, activeCategory])

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
            <div className="faq-container">
                <div className="faq-header">
                    <h1>Frequently Asked Questions</h1>
                    <p>Find answers to common questions about CozyPages and our AI-powered story generation platform.</p>
                </div>

                <div className="faq-content">
                    <div className="faq-search">
                        <input
                            type="text"
                            placeholder="Search for questions..."
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                    </div>

                    <div className="faq-categories">
                        {categories.map((category) => (
                            <button
                                key={category.id}
                                className={`category-filter ${activeCategory === category.id ? "active" : ""}`}
                                onClick={() => setActiveCategory(category.id)}
                            >
                                {category.name}
                            </button>
                        ))}
                    </div>

                    <div className="faq-list">
                        {filteredFAQs.length > 0 ? (
                            filteredFAQs.map((faq) => (
                                <div key={faq.id} className="faq-item">
                                    <button
                                        className={`faq-question ${openItems.has(faq.id) ? "active" : ""}`}
                                        onClick={() => toggleItem(faq.id)}
                                    >
                                        <span>{faq.question}</span>
                                        <span className={`faq-icon ${openItems.has(faq.id) ? "rotated" : ""}`}>▼</span>
                                    </button>
                                    <div className={`faq-answer ${openItems.has(faq.id) ? "open" : ""}`}>
                                        <div className="faq-answer-content">
                                            <p>{faq.answer}</p>
                                        </div>
                                    </div>
                                </div>
                            ))
                        ) : (
                            <div className="no-results">
                                <h3>No questions found</h3>
                                <p>Try adjusting your search terms or browse different categories.</p>
                            </div>
                        )}
                    </div>

                    <div className="contact-support">
                        <h3>Still have questions?</h3>
                        <p>Can't find what you're looking for? Our support team is here to help!</p>
                        <Link to="/support" className="support-button">
                            Contact Support
                        </Link>
                    </div>
                </div>
            </div>
        </div>
    )
}

export default FAQPage
