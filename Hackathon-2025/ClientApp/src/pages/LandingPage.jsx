"use client"

import { useState, useEffect } from "react"
import { useNavigate } from "react-router-dom"
import { useAuth } from "../context/AuthContext"
import "./LandingPage.css"

const SHOW_COMPANY = false
const SHOW_RESOURCES = false

const LandingPage = () => {
    const disableFancy =
        window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ||
        window.matchMedia?.('(max-width: 1024px)').matches;
    const navigate = useNavigate()
    const { user } = useAuth()
    const [showWarning, setShowWarning] = useState(false)
    const [isVisible, setIsVisible] = useState({
        hero: true,
        features: false,
        parents: false,
        testimonials: false,
        cta: false,
    })

    // Handle scroll animations
    useEffect(() => {
        const handleScroll = () => {
            const scrollPosition = window.scrollY
            const windowHeight = window.innerHeight

            // Check each section's visibility based on scroll position
            setIsVisible({
                hero: true, // Always visible
                features: scrollPosition > windowHeight * 0.2,
                parents: scrollPosition > windowHeight * 0.5,
                testimonials: scrollPosition > windowHeight * 0.7,
                cta: scrollPosition > windowHeight * 0.9,
            })
        }

        window.addEventListener("scroll", handleScroll)
        return () => window.removeEventListener("scroll", handleScroll)
    }, [])

    const handleCreateClick = () => {
        if (!user) {
            setShowWarning(true)
            setTimeout(() => setShowWarning(false), 3000)
            return
        }
        navigate("/create")
    }

    return (
        <div className="landing-page">
            {/* Hero Section */}
            <section className="hero-section">
                <div className="stars"></div>
                {/* only render sprinkles when not mobile / reduced motion */}
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
                        Created Just For Your Child
                    </h1>
                    <p className="hero-subtitle">
                        Where imagination meets personalization to create unforgettable bedtime adventures
                    </p>

                    <div className="hero-buttons">
                        <button className="primary-button" onClick={handleCreateClick}>
                            <span className="button-icon">✨</span>
                            Create Your Story
                        </button>
                        <a href="#how-it-works" className="secondary-button">
                            Learn More
                        </a>
                    </div>

                    {showWarning && (
                        <div className="warning-message">
                            <p>You need to be logged in to create a story</p>
                            <div className="warning-actions">
                                <button onClick={() => navigate("/login")} className="warning-button">
                                    Log In
                                </button>
                                <button onClick={() => navigate("/signup")} className="warning-button">
                                    Sign Up
                                </button>
                            </div>
                        </div>
                    )}

                    <div className="hero-image">
                        <img src="/background.jpg" alt="Magical storybook" className="floating" />
                    </div>
                </div>

                {/* Scroll Indicator */}
                {!disableFancy && (
                    <div className="scroll-indicator">
                        <div className="mouse"><div className="wheel"></div></div>
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
                                <div className="step-icon">📝</div>
                                <h3>Tell Us About Your Hero</h3>
                                <p>Add a name (real or made-up), pick a reading level and art style,
                                    choose a theme and optional lesson, and customize the hero’s look—age,
                                    gender, skin/hair/eye and outfit colors, plus accessories.</p>
                            </div>
                            <div className="step">
                                <div className="step-number">2</div>
                                <div className="step-icon">🧙‍♂️</div>
                                <h3>Our Magic Happens</h3>
                                <p>Our storytellers craft a unique tale personalized for the
                                    hero you choose, weaving in their interests and personality.</p>
                            </div>
                            <div className="step">
                                <div className="step-number">3</div>
                                <div className="step-icon">📖</div>
                                <h3>Read Together</h3>
                                <p>Enjoy a special moment with a story that speaks to them—whether
                                    they’re starring as themselves or as a character they created.</p>
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
                                <div className="parent-icon">⏰</div>
                                <h3>Save Precious Time</h3>
                                <p>
                                    Create beautiful bedtime stories in minutes, not hours. Perfect for busy parents who still want quality
                                    bedtime moments.
                                </p>
                            </div>
                            <div className="parent-card">
                                <div className="parent-icon">🔄</div>
                                <h3>Always Fresh Content</h3>
                                <p>
                                    No more reading the same books over and over. Create new stories whenever you want, keeping bedtime
                                    exciting.
                                </p>
                            </div>
                            <div className="parent-card">
                                <div className="parent-icon">💡</div>
                                <h3>Educational Value</h3>
                                <p>
                                    Stories can include educational themes and valuable life lessons tailored to what you want your child to
                                    learn.
                                </p>
                            </div>
                            <div className="parent-card">
                                <div className="parent-icon">❤️</div>
                                <h3>Strengthen Bonds</h3>
                                <p>
                                    Create special moments you’ll both remember with stories that star your child's crafted character.
                                </p>
                            </div>
                        </div>
                    </div>
                </section>

                {/* Kids Love It Section */}
                <section className="section kids-section">
                    <div className={`section-content ${isVisible.testimonials ? "slide-up" : ""}`}>
                        <h2 className="section-title">Kids Can't Get Enough!</h2>

                        <div className="testimonials-container">
                            <div className="testimonial">
                                <div className="testimonial-stars">★★★★★</div>
                                {/*<p className="testimonial-text">*/}
                                {/*    "My daughter asks for her personalized unicorn story every night now. She loves being the main*/}
                                {/*    character!"*/}
                                {/*</p>*/}
                                <p className="testimonial-text">
                                    "Review Placeholder"
                                </p>
                                <div className="testimonial-author">
                                    <img src="/parent.jpg" alt="Parent" className="testimonial-avatar" />
                                    <div>
                                        <p className="testimonial-name">Sarah T.</p>
                                        <p className="testimonial-relation">Mom of Emma, 5</p>
                                    </div>
                                </div>
                            </div>

                            <div className="testimonial">
                                <div className="testimonial-stars">★★★★★</div>
                                {/*<p className="testimonial-text">*/}
                                {/*    "My son was never interested in bedtime stories until we found Starlit Stories. Now he's excited for bedtime!"*/}
                                {/*</p>*/}
                                <p className="testimonial-text">
                                    "Review Placeholder"
                                </p>
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
                                {/*<p className="testimonial-text">*/}
                                {/*    "The stories have helped my twins develop their imagination and vocabulary. They love discussing the*/}
                                {/*    adventures!"*/}
                                {/*</p>*/}
                                <p className="testimonial-text">
                                    "Review Placeholder"
                                </p>
                                <div className="testimonial-author">
                                    <img src="/parent.jpg" alt="Parent" className="testimonial-avatar" />
                                    <div>
                                        <p className="testimonial-name">Jennifer L.</p>
                                        <p className="testimonial-relation">Mom of Lily & Lucas, 7</p>
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
            </div>

            {/* Call to Action Section */}
            <section className="section cta-section">
                <div className={`section-content ${isVisible.cta ? "fade-in" : ""}`}>
                    <h2 className="section-title">Start Your Magical Journey Today</h2>
                    <p className="cta-text">
                        Create your first personalized story in minutes and make bedtime the best time of day
                    </p>

                    <button className="primary-button large" onClick={handleCreateClick}>
                        <span className="button-icon">✨</span>
                        Create Your First Story
                    </button>

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
                                {/*<a href="/blog">Blog</a>*/}
                                <a href="/faq">FAQ</a>
                                <a href="/support">Support</a>
                            </div>
                        )}

                        <div className="footer-column">
                            <h4>Connect</h4>
                            <div className="social-links">
                                <a href="#" className="social-link">📱</a>
                                <a href="#" className="social-link">📘</a>
                                <a href="#" className="social-link">📸</a>
                                <a href="#" className="social-link">🐦</a>
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