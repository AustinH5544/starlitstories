"use client"

import { useState } from "react"
import { Link } from "react-router-dom"
import { Helmet } from "react-helmet-async"
import SiteFooter from "../components/SiteFooter"
import usePublicConfig from "../hooks/usePublicConfig"
import "./FAQPage.css"

const FAQPage = () => {
    const [searchTerm, setSearchTerm] = useState("")
    const [activeFilter, setActiveFilter] = useState("All")
    const [openItems, setOpenItems] = useState(new Set())
    const { config: publicConfig } = usePublicConfig()
    const pricing = publicConfig.pricing || {}
    const proPrice = pricing.pro?.price || "$4.99/month"
    const premiumPrice = pricing.premium?.price || "$9.99/month"
    const proOriginalPrice = pricing.pro?.originalPrice
    const premiumOriginalPrice = pricing.premium?.originalPrice
    const paidPlansLine = pricing.pro?.isOnSale || pricing.premium?.isOnSale
        ? `Pro is currently ${proPrice}${proOriginalPrice ? ` (normally ${proOriginalPrice})` : ""}, and Premium is currently ${premiumPrice}${premiumOriginalPrice ? ` (normally ${premiumOriginalPrice})` : ""}. Both plans keep the same included stories, saved characters, and features.`
        : `Pro (${proPrice}) gives you 5 stories per month, 5 saved characters, and all 11 art styles. Premium (${premiumPrice}) gives you 11 stories per month, 10 saved characters, all art styles, and print-ready PDF downloads.`

    const faqData = [
        {
            id: 1,
            category: "General",
            question: "What is Starlit Stories?",
            answer:
                "Starlit Stories is an AI-powered platform that generates personalized, illustrated children's storybooks in minutes. You describe your child's character — name, appearance, outfit, and more — choose a theme and a lesson you'd like the story to carry, and our AI writes and illustrates a one-of-a-kind adventure starring them.",
        },
        {
            id: 2,
            category: "General",
            question: "Who is Starlit Stories designed for?",
            answer:
                "Starlit Stories is designed for parents, grandparents, and caregivers of children roughly ages 3–12, though anyone is welcome to create a story. The stories are age-appropriate, imaginative, and safe — no scary themes, no inappropriate content.",
        },
        {
            id: 3,
            category: "General",
            question: "Is there a mobile app?",
            answer:
                "Starlit Stories is currently web-only and works great in any modern browser on phones, tablets, and computers. A native app may be on the way — if you'd like to see one, let us know through our support page!",
        },
        {
            id: 4,
            category: "General",
            question: "How can I share my stories with family and friends?",
            answer:
                "Every story gets a unique public link you can send to anyone — no account required to view it. This works on all plans, including Free. Pro and Premium subscribers can also download a print-ready PDF to save or print at home.",
        },
        {
            id: 5,
            category: "Stories",
            question: "How long does it take to generate a story?",
            answer:
                "Most stories are ready in about 1–3 minutes. Generation time can vary slightly depending on how busy our servers are. You'll see a real-time progress indicator while your story is being created, so you always know where things stand.",
        },
        {
            id: 7,
            category: "Stories",
            question: "What art styles can I choose from?",
            answer:
                "Paid plans (Pro and Premium) can choose from 11 distinct art styles: Watercolor, Comic Book, Crayon, Paper Cut, Toy 3D, Pixel Art, Ink Wash, Gouache, Pastel, Line Art, and Clay. Free plan stories are illustrated in Watercolor.",
        },
        {
            id: 8,
            category: "Stories",
            question: "How much can I customize the character?",
            answer:
                "You can customize your character's name, age, skin tone, eye color, hair, and full outfit. You can also save characters to reuse across future stories without filling everything in again.",
        },
        {
            id: 9,
            category: "Stories",
            question: "Can I choose a theme or moral for the story?",
            answer:
                "Yes! When creating a story you pick a theme (like a cozy village adventure or a space exploration) and an optional lesson or moral — things like kindness, courage, perseverance, or friendship. The AI weaves your choices naturally into the narrative.",
        },
        {
            id: 10,
            category: "Stories",
            question: "Are my stories saved permanently?",
            answer:
                "Yes. Every story you generate is saved permanently to your library on your profile page. You can read, share, or download them any time.",
        },
        {
            id: 11,
            category: "Stories",
            question: "Can I create stories in languages other than English?",
            answer:
                "Currently, Starlit Stories generates stories in English only. We'd love to add more languages in the future — if that's important to you, please let us know via our support page.",
        },
        {
            id: 12,
            category: "Account",
            question: "Do I need an account to use Starlit Stories?",
            answer:
                "Yes — a free account is required to create and save stories. Signing up only takes your email address and a password. Anyone with a share link can read a story without an account.",
        },
        {
            id: 13,
            category: "Account",
            question: "How do I verify my email address?",
            answer:
                "After signing up, we'll send a verification link to the email address you registered with. Click it to activate your account. If you don't see the email, check your spam folder. You can also request a new verification email from the login page.",
        },
        {
            id: 14,
            category: "Billing",
            question: "What's included in the Free plan?",
            answer:
                "The Free plan lets you create 1 story per month, save 1 character, and generates stories in the Watercolor art style. It's a great way to try Starlit Stories with no commitment.",
        },
        {
            id: 15,
            category: "Billing",
            question: "What do Pro and Premium plans include?",
            answer:
                pricing.pro?.isOnSale || pricing.premium?.isOnSale
                    ? `${paidPlansLine} Pro includes 5 stories per month, 5 saved characters, and all 11 art styles. Premium includes 11 stories per month, 10 saved characters, all art styles, and print-ready PDF downloads.`
                    : paidPlansLine,
        },
        {
            id: 16,
            category: "Billing",
            question: "What are add-on story credits?",
            answer:
                "If you run out of your monthly stories, you can purchase one-time add-on credit packs: +5 credits for $4, or +11 credits for $8. Unlike subscription stories, add-on credits never expire — they stay in your account until you use them.",
        },
        {
            id: 17,
            category: "Billing",
            question: "Do unused monthly stories roll over?",
            answer:
                "Subscription stories (those included with your Free, Pro, or Premium plan) reset each month and do not roll over. Add-on credits you've purchased are separate and never expire.",
        },
        {
            id: 18,
            category: "Billing",
            question: "Can I cancel my subscription?",
            answer:
                "Yes, at any time. Open your profile page and use the Manage Subscription button to access the Stripe customer portal, where you can cancel. You'll keep your plan benefits through the end of your current billing period.",
        },
        {
            id: 19,
            category: "Billing",
            question: "Can I downgrade to a lower plan?",
            answer:
                "Yes. Open your profile page and use the Manage Subscription button to access the Stripe customer portal, where you can switch to a lower plan. Changes take effect at the start of your next billing period.",
        },
        {
            id: 20,
            category: "Technical",
            question: "What browsers are supported?",
            answer:
                "Starlit Stories works on any modern browser — Chrome, Firefox, Safari, and Edge on desktop and mobile. We recommend keeping your browser up to date for the best experience.",
        },
        {
            id: 21,
            category: "Technical",
            question: "Is my data secure?",
            answer:
                "Yes. Your account information and stories are encrypted in transit and stored securely. We do not sell or share your personal data or story content with third parties. You retain full ownership of everything you create.",
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

    const faqSchema = {
        "@context": "https://schema.org",
        "@type": "FAQPage",
        "mainEntity": faqData.map((faq) => ({
            "@type": "Question",
            "name": faq.question,
            "acceptedAnswer": {
                "@type": "Answer",
                "text": faq.answer,
            },
        })),
    }

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
            <Helmet>
                <title>Frequently Asked Questions | Starlit Stories</title>
                <meta name="description" content="Got questions about Starlit Stories? Find answers about how personalized AI storybooks work, pricing, and how to get started." />
                <link rel="canonical" href="https://starlitstories.app/faq" />
                <meta property="og:title" content="Frequently Asked Questions | Starlit Stories" />
                <meta property="og:description" content="Got questions about Starlit Stories? Find answers about how personalized AI storybooks work, pricing, and how to get started." />
                <meta property="og:type" content="website" />
                <meta property="og:url" content="https://starlitstories.app/faq" />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content="Frequently Asked Questions | Starlit Stories" />
                <meta name="twitter:description" content="Got questions about Starlit Stories? Find answers about how personalized AI storybooks work, pricing, and how to get started." />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
                <script type="application/ld+json">{JSON.stringify(faqSchema)}</script>
            </Helmet>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>
            <div className="sprinkle"></div>
            <div className="sprinkle2"></div>
            <div className="sprinkle3"></div>

            <div className="faq-container">
                <div className="faq-header">
                    <h1 className="faq-title">Frequently Asked Questions</h1>
                    <p className="faq-subtitle">
                        Find answers to common questions about Starlit Stories and our AI storytelling platform
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
            <SiteFooter />
        </div>
    )
}

export default FAQPage
