"use client"

import { useState, useEffect } from "react"
import { useAuth } from "../context/AuthContext"
import StoryForm from "../components/StoryForm"
import axios from "../api"
import "./CreatePage.css"
import { useNavigate } from "react-router-dom"

const CreatePage = () => {
    const { user } = useAuth()
    const [story, setStory] = useState(null)
    const [isLoading, setIsLoading] = useState(false)
    const [storyReady, setStoryReady] = useState(false)
    const [error, setError] = useState(null)
    const [userProfile, setUserProfile] = useState(null)
    const [profileLoading, setProfileLoading] = useState(true)
    const navigate = useNavigate()

    // Fetch user profile to check story count
    useEffect(() => {
        const fetchUserProfile = async () => {
            if (!user?.email) {
                setProfileLoading(false)
                return
            }

            try {
                const res = await axios.get(`/profile/${user.email}`)
                setUserProfile(res.data)
            } catch (err) {
                console.error("Error loading user profile:", err)
            } finally {
                setProfileLoading(false)
            }
        }

        fetchUserProfile()
    }, [user])

    const generateStory = async (formData) => {
        setIsLoading(true)
        setStoryReady(false)
        setError(null)
        setStory(null)

        try {
            // Append user email to form data
            const fullRequest = {
                ...formData,
                email: user?.email || "",
            }

            const res = await axios.post("/story/generate-full", fullRequest)
            setStory(res.data)
            setStoryReady(true)
        } catch (err) {
            console.error("API Error:", err)
            const message = err?.response?.data ?? "Oops! Something went wrong generating your story."
            setError(message)
        } finally {
            setIsLoading(false)
        }
    }

    const isValidStory =
        story &&
        Array.isArray(story.pages) &&
        story.pages.length > 0 &&
        story.pages[0].text?.toLowerCase().startsWith("oops") === false &&
        !error

    // Check if free user has reached their limit
    const isFreeUserAtLimit = userProfile && user?.membership === "free" && userProfile.booksGenerated >= 1

    // Show login prompt if user is not signed in
    if (!user) {
        return (
            <div className="create-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="create-container">
                    <div className="auth-required-prompt">
                        <div className="auth-icon">🔐</div>
                        <h2 className="auth-title">Sign In Required</h2>
                        <p className="auth-message">
                            You need to be signed in to create magical stories for your little ones. Join thousands of families
                            already creating personalized bedtime adventures!
                        </p>

                        <div className="auth-benefits">
                            <div className="benefit-item">
                                <span className="benefit-icon">✨</span>
                                <span>Create personalized stories</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">🎨</span>
                                <span>Beautiful custom illustrations</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">📚</span>
                                <span>Save and revisit your stories</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">👨‍👩‍👧‍👦</span>
                                <span>Share with family</span>
                            </div>
                        </div>

                        <div className="auth-actions">
                            <button onClick={() => navigate("/signup")} className="signup-cta-button">
                                <span className="button-icon">🚀</span>
                                <span>Create Free Account</span>
                            </button>
                            <button onClick={() => navigate("/login")} className="login-cta-button">
                                <span className="button-icon">🔮</span>
                                <span>Sign In</span>
                            </button>
                        </div>

                        <div className="auth-footer">
                            <p>
                                Already have an account? <a href="/login">Sign in here</a> | New to CozyPages?{" "}
                                <a href="/signup">Join free</a>
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        )
    }

    if (profileLoading) {
        return (
            <div className="create-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="create-container">
                    <div className="loading-container">
                        <div className="loading-spinner">
                            <div className="spinner"></div>
                        </div>
                        <p className="loading-text">
                            <span className="loading-icon">✨</span>
                            Loading your account...
                        </p>
                    </div>
                </div>
            </div>
        )
    }

    return (
        <div className="create-page">
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="create-container">
                <div className="create-header">
                    <h1 className="create-title">Create Your Magical Story</h1>
                    <p className="create-subtitle">
                        Tell us about your child and we'll craft a personalized bedtime adventure just for them
                    </p>
                </div>

                {isFreeUserAtLimit ? (
                    <div className="upgrade-prompt">
                        <div className="upgrade-icon">🚀</div>
                        <h2 className="upgrade-title">Ready for More Magic?</h2>
                        <p className="upgrade-message">
                            You've created your free story! To continue crafting magical adventures for your little ones, upgrade to
                            one of our premium plans.
                        </p>

                        <div className="upgrade-benefits">
                            <div className="benefit-item">
                                <span className="benefit-icon">📚</span>
                                <span>Create multiple stories per month</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">🎨</span>
                                <span>Access to premium illustrations</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">👨‍👩‍👧‍👦</span>
                                <span>Support for multiple characters</span>
                            </div>
                            <div className="benefit-item">
                                <span className="benefit-icon">🖨️</span>
                                <span>Download and print your stories</span>
                            </div>
                        </div>

                        <div className="upgrade-actions">
                            <button onClick={() => navigate("/upgrade")} className="upgrade-button">
                                <span className="button-icon">⭐</span>
                                <span>Upgrade Your Plan</span>
                            </button>
                            <button onClick={() => navigate("/profile")} className="back-to-profile-btn">
                                <span className="button-icon">👤</span>
                                <span>Back to Profile</span>
                            </button>
                        </div>

                        <div className="upgrade-footer">
                            <p>
                                Questions? <a href="mailto:support@cozypages.com">Contact our support team</a>
                            </p>
                        </div>
                    </div>
                ) : (
                    <div className="create-form-wrapper">
                        <StoryForm onSubmit={generateStory} />

                        {isLoading && (
                            <div className="loading-container">
                                <div className="loading-spinner">
                                    <div className="spinner"></div>
                                </div>
                                <p className="loading-text">
                                    <span className="loading-icon">✨</span>
                                    Creating your magical story...
                                </p>
                                <p className="loading-subtext">This may take a few moments while our storytellers work their magic</p>
                            </div>
                        )}

                        {!isLoading && error && (
                            <div className="error-container">
                                <div className="error-icon">😔</div>
                                <p className="create-error">{error}</p>
                                <p className="error-subtext">Please try again or contact support if the problem persists</p>
                            </div>
                        )}

                        {!isLoading && storyReady && isValidStory && (
                            <div className="success-container">
                                <div className="success-icon">🎉</div>
                                <p className="success-text">Your story is ready!</p>
                                <button className="view-story-button" onClick={() => navigate("/view", { state: { story } })}>
                                    <span className="button-icon">📖</span>
                                    <span>View Your Story</span>
                                </button>
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    )
}

export default CreatePage
