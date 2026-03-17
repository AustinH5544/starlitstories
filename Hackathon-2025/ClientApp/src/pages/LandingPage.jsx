"use client"

import { useState, useEffect, useRef } from "react"
import { useNavigate } from "react-router-dom"
import { useAuth } from "../context/AuthContext"
import posthog from "../analytics"
import { Helmet } from "react-helmet-async"
import "./LandingPage.css"

const webAppSchema = {
    "@context": "https://schema.org",
    "@type": "WebApplication",
    "name": "Starlit Stories",
    "url": "https://starlitstories.app",
    "description": "Create personalized illustrated storybooks where your child is the hero.",
    "applicationCategory": "EntertainmentApplication",
    "operatingSystem": "Web",
    "offers": { "@type": "Offer", "price": "0", "priceCurrency": "USD", "description": "Free tier available" },
    "audience": { "@type": "PeopleAudience", "audienceType": "Parents", "suggestedMinAge": "2", "suggestedMaxAge": "10" }
}

const orgSchema = {
    "@context": "https://schema.org",
    "@type": "Organization",
    "name": "Starlit Stories",
    "url": "https://starlitstories.app",
    "logo": "https://starlitstories.app/og-image.png",
    "sameAs": []
}

import instagramIcon from "../assets/social/instagram.png";
import facebookIcon from "../assets/social/facebook.png";
import twitterIcon from "../assets/social/twitter.png";
import xIcon from "../assets/social/x.png";

import writeIcon from "../assets/step-icons/write.png";
import magicIcon from "../assets/step-icons/magic.png"
import readIcon from "../assets/step-icons/read.png"

import timeIcon from "../assets/parent-icons/time.png"
import refreshIcon from "../assets/parent-icons/refresh.png"
import ideaIcon from "../assets/parent-icons/idea.png"
import heartIcon from "../assets/parent-icons/heart.png"

const SHOW_COMPANY = false
const SHOW_RESOURCES = true
const SHOW_TESTIMONIALS = false

const artStyleShowcase = [
    {
        key: "watercolor",
        name: "Watercolor",
        note: "Soft and dreamy",
        examples: ["/art-style-examples/watercolor-1.jpg", "/art-style-examples/watercolor-2.jpg", "/art-style-examples/watercolor-3.jpg"],
    },
    {
        key: "comic",
        name: "Comic",
        note: "Bold lines and pop color",
        examples: ["/art-style-examples/comic-1.jpg", "/art-style-examples/comic-2.jpg", "/art-style-examples/comic-3.jpg"],
    },
    {
        key: "crayon",
        name: "Crayon",
        note: "Kid-drawn texture",
        examples: ["/art-style-examples/crayon-1.jpg", "/art-style-examples/crayon-2.jpg", "/art-style-examples/crayon-3.jpg"],
    },
    {
        key: "papercut",
        name: "Paper Cutout",
        note: "Layered collage shapes",
        examples: ["/art-style-examples/papercut-1.jpg", "/art-style-examples/papercut-2.jpg", "/art-style-examples/papercut-3.jpg"],
    },
    {
        key: "toy3d",
        name: "3D Toy Render",
        note: "Playful toy-world depth",
        examples: ["/art-style-examples/toy3d-1.jpg", "/art-style-examples/toy3d-2.jpg", "/art-style-examples/toy3d-3.jpg"],
    },
    {
        key: "pixel",
        name: "Pixel Art",
        note: "Retro 16-bit charm",
        examples: ["/art-style-examples/pixel-1.jpg", "/art-style-examples/pixel-2.jpg", "/art-style-examples/pixel-3.jpg"],
    },
    {
        key: "inkwash",
        name: "Ink & Wash",
        note: "Minimal and elegant",
        examples: ["/art-style-examples/inkwash-1.jpg", "/art-style-examples/inkwash-2.jpg", "/art-style-examples/inkwash-3.jpg"],
    },
    {
        key: "gouache",
        name: "Gouache",
        note: "Matte painterly storybook",
        examples: ["/art-style-examples/gouache-1.jpg", "/art-style-examples/gouache-2.jpg", "/art-style-examples/gouache-3.jpg"],
    },
    {
        key: "pastel",
        name: "Soft Pastel",
        note: "Chalky and cozy",
        examples: ["/art-style-examples/pastel-1.jpg", "/art-style-examples/pastel-2.jpg", "/art-style-examples/pastel-3.jpg"],
    },
    {
        key: "lineart",
        name: "Clean Line Art",
        note: "Modern and crisp",
        examples: ["/art-style-examples/lineart-1.jpg", "/art-style-examples/lineart-2.jpg", "/art-style-examples/lineart-3.jpg"],
    },
    {
        key: "clay",
        name: "Clay Animation",
        note: "Handmade clay look",
        examples: ["/art-style-examples/clay-1.jpg", "/art-style-examples/clay-2.jpg", "/art-style-examples/clay-3.jpg"],
    },
]

const LandingPage = () => {
    const disableFancy = typeof window !== 'undefined' && (
        window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ||
        window.matchMedia?.("(max-width: 1024px)").matches
    )

    const navigate = useNavigate()
    const { user } = useAuth()

    // which sections are visible (for animations)
    const [isVisible, setIsVisible] = useState({
        hero: true,
        features: false,
        artStyles: false,
        parents: false,
        testimonials: false,
        cta: false,
    })

    // warn the user inline (top "hero" button vs bottom "cta" button)
    const [showWarning, setShowWarning] = useState(false)
    const [warningAt, setWarningAt] = useState/** @type {'hero' | 'cta' | null} */(null)
    const warnBtnRef = useRef(null)
    const [selectedArtStyle, setSelectedArtStyle] = useState(artStyleShowcase[0].key)
    const [activeExampleIndex, setActiveExampleIndex] = useState(0)
    const [imageLoadError, setImageLoadError] = useState(false)

    useEffect(() => {
        posthog.capture("landing_page_viewed")
    }, [])

    // focus the first action on warn (a11y)
    useEffect(() => {
        if (showWarning && warnBtnRef.current) {
            warnBtnRef.current.focus()
        }
    }, [showWarning])

    // Handle scroll animations
    useEffect(() => {
        const handleScroll = () => {
            const scrollPosition = window.scrollY
            const windowHeight = window.innerHeight
            setIsVisible({
                hero: true,
                features: scrollPosition > windowHeight * 0.2,
                artStyles: scrollPosition > windowHeight * 0.38,
                parents: scrollPosition > windowHeight * 0.55,
                testimonials: scrollPosition > windowHeight * 0.75,
                cta: scrollPosition > windowHeight * 0.95,
            })
        }
        window.addEventListener("scroll", handleScroll)
        return () => window.removeEventListener("scroll", handleScroll)
    }, [])

    const handleCreateClick = (origin = "hero") => {
        if (!user) {
            setWarningAt(origin)
            setShowWarning(true)
            // auto-hide after 3s; remove if you want it persistent
            window.clearTimeout((handleCreateClick)._t)
                ; (handleCreateClick)._t = window.setTimeout(() => setShowWarning(false), 3000)
            return
        }
        navigate("/create")
    }

    const activeStyle = artStyleShowcase.find((s) => s.key === selectedArtStyle) ?? artStyleShowcase[0]
    const activeExamples = activeStyle.examples || []
    const safeIndex = activeExamples.length > 0 ? activeExampleIndex % activeExamples.length : 0
    const activeImage = activeExamples[safeIndex]

    const goToPreviousExample = () => {
        if (activeExamples.length <= 1) return
        setActiveExampleIndex((prev) => (prev - 1 + activeExamples.length) % activeExamples.length)
    }

    const goToNextExample = () => {
        if (activeExamples.length <= 1) return
        setActiveExampleIndex((prev) => (prev + 1) % activeExamples.length)
    }

    useEffect(() => {
        if (!isVisible.artStyles || activeExamples.length <= 1 || disableFancy) return

        const interval = window.setInterval(() => {
            setActiveExampleIndex((prev) => (prev + 1) % activeExamples.length)
        }, 4500)

        return () => window.clearInterval(interval)
    }, [isVisible.artStyles, activeExamples.length, disableFancy, selectedArtStyle])

    return (
        <div className="landing-page">
            <Helmet>
                <title>Starlit Stories — Personalized Storybooks Where Your Child Is the Hero</title>
                <meta name="description" content="Create magical, personalized illustrated storybooks for your child in minutes. Choose a character, pick a theme, and watch your child become the hero of their own bedtime story." />
                <link rel="canonical" href="https://starlitstories.app/" />
                <meta property="og:title" content="Starlit Stories — Personalized Storybooks Where Your Child Is the Hero" />
                <meta property="og:description" content="Create magical, personalized illustrated storybooks for your child in minutes. Choose a character, pick a theme, and watch your child become the hero of their own bedtime story." />
                <meta property="og:type" content="website" />
                <meta property="og:url" content="https://starlitstories.app/" />
                <meta property="og:image" content="https://starlitstories.app/og-image.png" />
                <meta property="og:site_name" content="Starlit Stories" />
                <meta name="twitter:card" content="summary_large_image" />
                <meta name="twitter:title" content="Starlit Stories — Personalized Storybooks Where Your Child Is the Hero" />
                <meta name="twitter:description" content="Create magical, personalized illustrated storybooks for your child in minutes." />
                <meta name="twitter:image" content="https://starlitstories.app/og-image.png" />
                <script type="application/ld+json">{JSON.stringify(webAppSchema)}</script>
                <script type="application/ld+json">{JSON.stringify(orgSchema)}</script>
            </Helmet>
            {/* Hero Section */}
            <section className="hero-section">
                <div className="stars"></div>
                {!disableFancy && (
                    <>
                        <div className="twinkling"></div>
                        <div className="sprinkle"></div>
                        <div className="sprinkle2"></div>
                        <div className="sprinkle3"></div>
                    </>
                )}
                <div className="clouds"></div>

                <div className={`hero-content ${isVisible.hero ? "fade-in" : ""}`}>
                    <h1 className="hero-title">
                        <span className="magic-text">Magical</span> Bedtime Stories
                        <br />
                        Created Just for Your Child
                    </h1>
                    <p className="hero-subtitle">
                        Where imagination meets personalization to create unforgettable bedtime adventures
                    </p>

                    <div className="hero-buttons">
                        <button className="primary-button" onClick={() => handleCreateClick("hero")}>
                            <span className="button-icon">✨</span>
                            Create Your Story
                        </button>
                        <a href="#how-it-works" className="secondary-button">
                            Learn More
                        </a>
                    </div>

                    {showWarning && warningAt === "hero" && (
                        <div className="warning-message" role="alert">
                            <p>You need to be logged in to create a story</p>
                            <div className="warning-actions">
                                <button ref={warnBtnRef} onClick={() => navigate("/login")} className="warning-button">
                                    Log In
                                </button>
                                <button onClick={() => navigate("/signup")} className="warning-button">
                                    Try for Free
                                </button>
                            </div>
                        </div>
                    )}

                    <div className="hero-image">
                        <img src="/background.jpg" alt="Magical storybook" className="floating" />
                    </div>
                </div>

                {!disableFancy && (
                    <div className="scroll-indicator">
                        <div className="mouse">
                            <div className="wheel"></div>
                        </div>
                        <div className="arrow-down"></div>
                    </div>
                )}
            </section>

            <div className="gradient-block">
                {/* How It Works Section */}
                <section id="how-it-works" className="section">
                    <div className={`section-content ${isVisible.features ? "slide-up" : ""}`}>
                        <h2 className="section-title">How It Works</h2>
                        <div className="steps-container">
                            <div className="step">
                                <div className="step-number">1</div>

                                <div className="step-icon">
                                    <img
                                        className="step-icon-img"
                                        src={writeIcon}
                                        alt="Tell us about your hero"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>

                                <h3>Tell Us About Your Hero</h3>
                                <p>
                                    Add a name (real or made-up), pick a reading level and art style, choose a theme and optional
                                    lesson, and customize the hero’s look—age, gender, skin/hair/eye and outfit colors, plus
                                    accessories.
                                </p>
                            </div>
                            <div className="step">
                                <div className="step-number">2</div>

                                <div className="step-icon">
                                    <img
                                        className="step-icon-img"
                                        src={magicIcon}
                                        alt="Our magic happens"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>
                                <h3>Our Magic Happens</h3>
                                <p>
                                    Our storytellers craft a unique tale personalized for the hero you choose, weaving in their
                                    interests and personality.
                                </p>
                            </div>
                            <div className="step">
                                <div className="step-number">3</div>

                                <div className="step-icon">
                                    <img
                                        className="step-icon-img"
                                        src={readIcon}
                                        alt="Read together"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>

                                <h3>Read Together</h3>
                                <p>
                                    Enjoy a special moment with a story that speaks to them—whether they’re starring as themselves
                                    or as a character they created.
                                </p>
                            </div>
                        </div>
                    </div>
                </section>

                <section className="section art-styles-section">
                    <div className={`section-content ${isVisible.artStyles ? "fade-in" : ""}`}>
                        <h2 className="section-title">Explore Art Styles</h2>
                        <p className="art-styles-intro">
                            Use the dropdown to pick a style and browse real examples.
                        </p>

                        <div className="art-style-showcase">
                            <div className="art-style-preview-panel">
                                <div className="art-style-controls">
                                    <label htmlFor="style-picker">Style</label>
                                    <select
                                        id="style-picker"
                                        value={selectedArtStyle}
                                        onChange={(e) => {
                                            setSelectedArtStyle(e.target.value)
                                            setActiveExampleIndex(0)
                                            setImageLoadError(false)
                                        }}
                                    >
                                        {artStyleShowcase.map((style) => (
                                            <option key={style.key} value={style.key}>
                                                {style.name}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                <div className="art-style-preview-header">
                                    <h3>{activeStyle.name}</h3>
                                    <p>{activeStyle.note}</p>
                                </div>

                                <div className="art-style-preview-frame">
                                    {!imageLoadError && activeImage ? (
                                        <img
                                            className="art-style-preview-image"
                                            src={activeImage}
                                            alt={`${activeStyle.name} example ${safeIndex + 1}`}
                                            loading="lazy"
                                            onError={() => setImageLoadError(true)}
                                        />
                                    ) : (
                                        <div className={`art-style-fallback art-style-${activeStyle.key}`}>
                                            <span>Add generated image files in /public/art-style-examples</span>
                                        </div>
                                    )}
                                </div>

                                <div className="art-style-rotator-controls">
                                    <button
                                        type="button"
                                        className="art-style-nav-btn"
                                        onClick={goToPreviousExample}
                                        disabled={activeExamples.length <= 1}
                                        aria-label="Previous example"
                                    >
                                        Prev
                                    </button>
                                    <span className="art-style-counter">
                                        {Math.min(safeIndex + 1, Math.max(activeExamples.length, 1))} / {Math.max(activeExamples.length, 1)}
                                    </span>
                                    <button
                                        type="button"
                                        className="art-style-nav-btn"
                                        onClick={goToNextExample}
                                        disabled={activeExamples.length <= 1}
                                        aria-label="Next example"
                                    >
                                        Next
                                    </button>
                                </div>

                                <div className="art-style-dots" role="tablist" aria-label={`${activeStyle.name} examples`}>
                                    {activeExamples.map((_, idx) => (
                                        <button
                                            key={idx}
                                            type="button"
                                            className={`art-style-dot ${idx === safeIndex ? "active" : ""}`}
                                            onClick={() => setActiveExampleIndex(idx)}
                                            aria-label={`Show example ${idx + 1}`}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                </section>

                {/* Parents Love It Section */}
                <section className="section parents-section">
                    <div className={`section-content ${isVisible.parents ? "fade-in" : ""}`}>
                        <h2 className="section-title">Parents Love Starlit Stories</h2>

                        <div className="parents-grid">
                            <div className="parent-card">
                                <div className="parent-icon">
                                    <img
                                        className="parent-icon-img"
                                        src={timeIcon}
                                        alt="Save precious time"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>
                                <h3>Save Precious Time</h3>
                                <p>
                                    Create beautiful bedtime stories in minutes, not hours. Perfect for busy parents who still want
                                    quality bedtime moments.
                                </p>
                            </div>

                            <div className="parent-card">
                                <div className="parent-icon">
                                    <img
                                        className="parent-icon-img"
                                        src={refreshIcon}
                                        alt="Always fresh content"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>
                                <h3>Always Fresh Content</h3>
                                <p>
                                    No more reading the same books over and over. Create new stories whenever you want, keeping
                                    bedtime exciting.
                                </p>
                            </div>

                            <div className="parent-card">
                                <div className="parent-icon">
                                    <img
                                        className="parent-icon-img"
                                        src={ideaIcon}
                                        alt="Educational value"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>
                                <h3>Educational Value</h3>
                                <p>
                                    Stories can include educational themes and valuable life lessons tailored to what you want your
                                    child to learn.
                                </p>
                            </div>

                            <div className="parent-card">
                                <div className="parent-icon">
                                    <img
                                        className="parent-icon-img"
                                        src={heartIcon}
                                        alt="Strengthen bonds"
                                        loading="lazy"
                                        decoding="async"
                                    />
                                </div>
                                <h3>Strengthen Bonds</h3>
                                <p>
                                    Create special moments you’ll both remember with stories that star your child—or any hero they imagine.
                                </p>
                            </div>
                        </div>
                    </div>
                </section>

                {/* Kids Love It Section — hidden until we have real reviews/stats */}
                {SHOW_TESTIMONIALS && (
                <section className="section kids-section">
                    <div className={`section-content ${isVisible.testimonials ? "slide-up" : ""}`}>
                        <h2 className="section-title">Kids Can't Get Enough!</h2>

                        <div className="testimonials-container">
                            <div className="testimonial">
                                <div className="testimonial-stars">★★★★★</div>
                                <p className="testimonial-text">"Review Placeholder"</p>
                                <div className="testimonial-author">
                                    <img src="/parent1.png" alt="Parent" className="testimonial-avatar" />
                                    <div>
                                        <p className="testimonial-name">Sarah T.</p>
                                        <p className="testimonial-relation">Mom of Emma, 5</p>
                                    </div>
                                </div>
                            </div>

                            <div className="testimonial">
                                <div className="testimonial-stars">★★★★★</div>
                                <p className="testimonial-text">"Review Placeholder"</p>
                                <div className="testimonial-author">
                                    <img src="/parent.jpg" alt="Parent" className="testimonial-avatar" />
                                    <div>
                                        <p className="testimonial-name">Michael R.</p>
                                        <p className="testimonial-relation">Dad of James, 6</p>
                                    </div>
                                </div>
                            </div>

                            <div className="testimonial">
                                <div className="testimonial-stars">★★★★★</div>
                                <p className="testimonial-text">"Review Placeholder"</p>
                                <div className="testimonial-author">
                                    <img src="/parent2.png" alt="Parent" className="testimonial-avatar" />
                                    <div>
                                        <p className="testimonial-name">Jennifer L.</p>
                                        <p className="testimonial-relation">Mom of Lily &amp; Lucas, 7</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div className="kids-stats">
                            <div className="stat">
                                <div className="stat-number">98%</div>
                                <p className="stat-text">of kids ask for repeat readings</p>
                            </div>
                            <div className="stat">
                                <div className="stat-number">15+</div>
                                <p className="stat-text">minutes of extra reading time</p>
                            </div>
                            <div className="stat">
                                <div className="stat-number">1000+</div>
                                <p className="stat-text">happy families</p>
                            </div>
                        </div>
                    </div>
                </section>
                )}
            </div>

            {/* Call to Action Section */}
            <section className="section cta-section">
                <div className={`section-content ${isVisible.cta ? "fade-in" : ""}`}>
                    <h2 className="section-title">Start Your Magical Journey Today</h2>
                    <p className="cta-text">
                        Create your first personalized story in minutes and make bedtime the best time of day
                    </p>

                    <button className="primary-button large" onClick={() => handleCreateClick("cta")}>
                        <span className="button-icon">✨</span>
                        Create Your First Story
                    </button>

                    {/* inline warning for the bottom CTA */}
                    {showWarning && warningAt === "cta" && (
                        <div className="warning-message" role="alert">
                            <p>You need to be logged in to create a story</p>
                            <div className="warning-actions">
                                <button ref={warnBtnRef} onClick={() => navigate("/login")} className="warning-button">
                                    Log In
                                </button>
                                <button onClick={() => navigate("/signup")} className="warning-button">
                                    Try for Free
                                </button>
                            </div>
                        </div>
                    )}

                    <div className="cta-features">
                        <div className="cta-feature">
                            <span className="feature-check">✓</span> Personalized for your child
                        </div>
                        <div className="cta-feature">
                            <span className="feature-check">✓</span> Ready in minutes
                        </div>
                        <div className="cta-feature">
                            <span className="feature-check">✓</span> Create unlimited stories
                        </div>
                    </div>
                </div>
            </section>

            {/* Footer */}
            <footer className="footer">
                <div className="footer-content">
                    <div className="footer-logo">
                        <span className="logo">Starlit Stories</span>
                        <p>Making bedtime magical, one story at a time</p>
                    </div>

                    <div className="footer-links">
                        {SHOW_COMPANY && (
                            <div className="footer-column">
                                <h4>Company</h4>
                                <a href="/about">About Us</a>
                                <a href="/contact">Contact</a>
                                <a href="/privacy">Privacy Policy</a>
                                <a href="/terms">Terms of Service</a>
                            </div>
                        )}

                        {SHOW_RESOURCES && (
                            <div className="footer-column">
                                <h4>Resources</h4>
                                <a href="/blog">Blog</a>
                                <a href="/faq">FAQ</a>
                                <a href="/support">Support</a>
                            </div>
                        )}

                        <div className="footer-links footer-meta-links">
                            <div className="footer-column">
                                <h4>Connect</h4>
                                <div className="social-links">
                                    <a href="https://instagram.com/starlitstoriesapp" target="_blank" rel="noopener noreferrer"
                                        className="social-link" aria-label="Instagram">
                                        <img src={instagramIcon} alt="Instagram" className="social-icon" />
                                    </a>

                                    <a href="https://facebook.com/profile.php?id=61587218416327" target="_blank" rel="noopener noreferrer"
                                        className="social-link" aria-label="Facebook">
                                        <img src={facebookIcon} alt="Facebook" className="social-icon" />
                                    </a>

                                    <a href="https://x.com/starlitapp" target="_blank" rel="noopener noreferrer"
                                        className="social-link" aria-label="X">
                                        <img src={xIcon} alt="X" className="social-icon" />
                                    </a>
                                </div>
                            </div>

                            <div className="footer-column contact-column">
                                <h4>Need help?</h4>
                                <a href="mailto:support@starlitstories.app" className="footer-email">
                                    support@starlitstories.app
                                </a>
                            </div>
                        </div>
                    </div>
                </div>

                <div className="footer-bottom">
                    <p>© 2025 Starlit Stories. All rights reserved.</p>
                </div>
            </footer>
        </div>
    )
}

export default LandingPage
