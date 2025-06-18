"use client"

import { useState, useEffect } from "react"
import "./AboutPage.css"

const teamMembers = [
    {
        name: "Tyler Woody",
        image: "/tyler.jpg",
        role: "Co-Founder & Developer",
        bio: "Passionate about combining technology with storytelling to create magical experiences for children.",
    },
    {
        name: "Austin Harrison",
        image: "/austin.jpg",
        role: "Co-Founder & Creative Director",
        bio: "Believes in the power of personalized stories to inspire imagination and foster a love for reading.",
    },
]

const AboutPage = () => {
    const [isVisible, setIsVisible] = useState({
        hero: true,
        mission: false,
        howItWorks: false,
        benefits: false,
        team: false,
        vision: false,
    })

    // Handle scroll animations
    useEffect(() => {
        const handleScroll = () => {
            const scrollPosition = window.scrollY
            const windowHeight = window.innerHeight

            setIsVisible({
                hero: true,
                mission: scrollPosition > windowHeight * 0.1,
                howItWorks: scrollPosition > windowHeight * 0.3,
                benefits: scrollPosition > windowHeight * 0.5,
                team: scrollPosition > windowHeight * 0.7,
                vision: scrollPosition > windowHeight * 0.9,
            })
        }

        window.addEventListener("scroll", handleScroll)
        return () => window.removeEventListener("scroll", handleScroll)
    }, [])

    return (
        <div className="about-page">
            {/* Hero Section */}
            <section className="about-hero">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className={`about-hero-content ${isVisible.hero ? "fade-in" : ""}`}>
                    <h1 className="about-title">Our Story</h1>
                    <p className="about-subtitle">
                        Creating magical moments between parents and children, one personalized story at a time
                    </p>
                </div>

                <div className="scroll-indicator">
                    <div className="mouse">
                        <div className="wheel"></div>
                    </div>
                    <div className="arrow-down"></div>
                    <p className="scroll-text">Scroll to learn more</p>
                </div>
            </section>

            {/* Mission Section */}
            <section className="about-section mission-section">
                <div className="section-content mission-visible">
                    <div className="section-image">
                        <img src="/ChildReading.jpg" alt="Child reading a book" className="floating-image" />
                    </div>
                    <div className="section-text">
                        <h2>Our Mission</h2>
                        <p>
                            At CozyPages, we believe every child deserves to be the hero of their own story. We're on a mission to
                            transform bedtime reading by creating personalized, magical stories that capture children's imagination
                            and create lasting memories.
                        </p>
                        <p>
                            We founded CozyPages because we saw how children light up when they recognize themselves in stories. By
                            combining the power of AI with the timeless tradition of bedtime stories, we're making storytelling more
                            accessible, creative, and meaningful for families everywhere.
                        </p>
                    </div>
                </div>
            </section>

            {/* How It Works Section */}
            <section className="about-section how-section">
                <div className={`section-content ${isVisible.howItWorks ? "slide-up" : ""}`}>
                    <h2>The Magic Behind Our Stories</h2>
                    <div className="process-steps">
                        <div className="process-step">
                            <div className="step-icon">
                                <span>1</span>
                            </div>
                            <h3>Share Your Details</h3>
                            <p>
                                Tell us about your child - their name, interests, favorite animals, or any special elements you'd like
                                included in the story.
                            </p>
                        </div>
                        <div className="process-step">
                            <div className="step-icon">
                                <span>2</span>
                            </div>
                            <h3>Our Storytellers Create</h3>
                            <p>
                                Our magical storytellers craft a unique narrative featuring your child as the main character, weaving in
                                their interests and preferences.
                            </p>
                        </div>
                        <div className="process-step">
                            <div className="step-icon">
                                <span>3</span>
                            </div>
                            <h3>Illustrations Come to Life</h3>
                            <p>
                                Beautiful, consistent illustrations are created to accompany each part of the story, bringing the
                                characters and scenes to life.
                            </p>
                        </div>
                        <div className="process-step">
                            <div className="step-icon">
                                <span>4</span>
                            </div>
                            <h3>Enjoy Together</h3>
                            <p>
                                Read the story together with your child and watch their eyes light up as they discover themselves in a
                                magical adventure.
                            </p>
                        </div>
                    </div>
                </div>
            </section>

            {/* Benefits Section */}
            <section className="about-section benefits-section">
                <div className={`section-content ${isVisible.benefits ? "fade-in" : ""}`}>
                    <h2>Why Families Love CozyPages</h2>
                    <div className="benefits-grid">
                        <div className="benefit-card">
                            <div className="benefit-icon">💫</div>
                            <h3>Boosts Imagination</h3>
                            <p>
                                Our personalized stories spark creativity and encourage children to dream big, expanding their
                                imaginative horizons.
                            </p>
                        </div>
                        <div className="benefit-card">
                            <div className="benefit-icon">📚</div>
                            <h3>Fosters Love for Reading</h3>
                            <p>
                                When children see themselves in stories, they develop a deeper connection to reading that can last a
                                lifetime.
                            </p>
                        </div>
                        <div className="benefit-card">
                            <div className="benefit-icon">❤️</div>
                            <h3>Creates Special Bonds</h3>
                            <p>
                                Sharing personalized stories creates meaningful moments between parents and children, strengthening your
                                relationship.
                            </p>
                        </div>
                        <div className="benefit-card">
                            <div className="benefit-icon">🌈</div>
                            <h3>Celebrates Uniqueness</h3>
                            <p>
                                Our stories celebrate what makes your child special, helping to build confidence and a positive
                                self-image.
                            </p>
                        </div>
                    </div>
                </div>
            </section>

            {/* Team Section */}
            <section className="about-section team-section">
                <div className={`section-content ${isVisible.team ? "slide-up" : ""}`}>
                    <h2>Meet Our Storytellers</h2>
                    <p className="team-intro">
                        We're a small team with a big passion for stories, technology, and creating magical experiences for
                        children.
                    </p>

                    <div className="team-grid">
                        {teamMembers.map((member, idx) => (
                            <div className="team-card" key={idx}>
                                <div className="team-photo-wrapper">
                                    <img src={member.image || "/placeholder.svg"} alt={member.name} className="team-photo" />
                                </div>
                                <h3>{member.name}</h3>
                                <p className="team-role">{member.role}</p>
                                <p className="team-bio">{member.bio}</p>
                            </div>
                        ))}
                    </div>
                </div>
            </section>

            {/* Vision Section */}
            <section className="about-section vision-section">
                <div className={`section-content ${isVisible.vision ? "fade-in" : ""}`}>
                    <div className="vision-content">
                        <h2>Our Vision for the Future</h2>
                        <p>
                            We're just getting started on our mission to revolutionize bedtime stories. Our roadmap includes exciting
                            new features to make your storytelling experience even more magical:
                        </p>

                        <div className="vision-features">
                            <div className="vision-feature">
                                <div className="vision-icon">🖨️</div>
                                <div>
                                    <h3>Physical Books</h3>
                                    <p>Turn your digital stories into beautiful printed keepsakes to treasure for years to come.</p>
                                </div>
                            </div>

                            <div className="vision-feature">
                                <div className="vision-icon">🎭</div>
                                <div>
                                    <h3>Themed Story Collections</h3>
                                    <p>
                                        Explore different worlds and genres with themed story packs - from space adventures to underwater
                                        explorations.
                                    </p>
                                </div>
                            </div>

                            <div className="vision-feature">
                                <div className="vision-icon">🔊</div>
                                <div>
                                    <h3>Audio Narration</h3>
                                    <p>Listen to professionally narrated versions of your personalized stories.</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </section>

            {/* Contact Section */}
            <section className="about-section contact-section">
                <div className="section-content">
                    <h2>Connect With Us</h2>
                    <p className="contact-intro">
                        We'd love to hear from you! Share your story experiences, suggestions, or just say hello.
                    </p>

                    <div className="contact-methods">
                        <a href="mailto:hello@cozypages.com" className="contact-method">
                            <div className="contact-icon">✉️</div>
                            <span>hello@cozypages.com</span>
                        </a>
                        <a href="https://twitter.com/cozypages" className="contact-method">
                            <div className="contact-icon">🐦</div>
                            <span>@cozypages</span>
                        </a>
                        <a href="https://instagram.com/cozypages" className="contact-method">
                            <div className="contact-icon">📸</div>
                            <span>@cozypages</span>
                        </a>
                    </div>

                    <div className="newsletter-signup">
                        <h3>Join Our Storytelling Community</h3>
                        <p>Subscribe to receive updates, story tips, and special offers.</p>
                        <form className="newsletter-form">
                            <input type="email" placeholder="Your email address" required />
                            <button type="submit">Subscribe</button>
                        </form>
                    </div>
                </div>
            </section>
        </div>
    )
}

export default AboutPage