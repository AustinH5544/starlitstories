"use client"

import { useState } from "react"
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
    const navigate = useNavigate()

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
            </div>
        </div>
    )
}

export default CreatePage