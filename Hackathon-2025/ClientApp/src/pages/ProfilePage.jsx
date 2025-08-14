"use client"

import { useEffect, useState } from "react"
import api from "../api"
import "./ProfilePage.css"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"

const ProfilePage = () => {
    const { user, logout } = useAuth()
    const [stories, setStories] = useState([])
    const [loading, setLoading] = useState(true)
    const navigate = useNavigate()

    const BASE = import.meta.env.BASE_URL

    const [showImageModal, setShowImageModal] = useState(false)

    // ONE source of truth for the avatar (use public/avatars/default-avatar.png)
    const [selectedImage, setSelectedImage] = useState(
        user?.profileImage || `${BASE}avatars/default-avatar.png`
    )
    const [imgError, setImgError] = useState(false)

    const profileImages = [
        `${BASE}avatars/wizard-avatar.png`,
        `${BASE}avatars/princess-avatar.png`,
        `${BASE}avatars/knight-avatar.png`,
        `${BASE}avatars/whimsical-fairy-avatar.png`,
        `${BASE}avatars/dragon-avatar.png`,
        `${BASE}avatars/unicorn-avatar.png`,
        `${BASE}avatars/pirate-avatar.png`,
        `${BASE}avatars/astronaut-avatar.png`,
        `${BASE}avatars/whimsical-mermaid-avatar.png`,
        `${BASE}avatars/superhero-avatar.png`,
        `${BASE}avatars/cat-avatar.png`,
    ]

    const handleImageSelect = (imageUrl) => {
        setSelectedImage(imageUrl)
        setImgError(false)
        setShowImageModal(false)
    }

    useEffect(() => {
        const fetchStories = async () => {
            try {
                const res = await api.get("/profile/me/stories")
                setStories(res.data)
            } catch (err) {
                console.error("Error loading stories:", err)
            } finally {
                setLoading(false)
            }
        }
        if (user?.email) fetchStories()
    }, [user])

    if (!user) {
        return (
            <div className="profile-page">
                <div className="stars" />
                <div className="twinkling" />
                <div className="clouds" />
                <div className="profile-container">
                    <h2>You are not logged in.</h2>
                    <p>Please log in to view your profile.</p>
                    <button onClick={() => navigate("/login")} className="login-redirect-btn">
                        <span className="button-icon">🔮</span>
                        <span>Go to Login</span>
                    </button>
                </div>
            </div>
        )
    }

    return (
        <div className="profile-page">
            <div className="stars" />
            <div className="twinkling" />
            <div className="clouds" />

            <div className="profile-container">
                <div className="profile-header">
                    <div className="profile-avatar-container">
                        <div className="user-avatar-large" onClick={() => setShowImageModal(true)}>
                            {!imgError ? (
                                <img
                                    src={selectedImage}
                                    alt="Profile"
                                    className="avatar-image"
                                    onError={() => setImgError(true)}
                                />
                            ) : (
                                <div className="avatar-fallback">
                                    {(user.name || user.email)[0].toUpperCase()}
                                </div>
                            )}
                            <div className="avatar-edit-overlay">
                                <span>✏️</span>
                            </div>
                        </div>
                    </div>

                    <h1 className="profile-title">Welcome back, {user.name || user.email.split("@")[0]}!</h1>
                    <p className="profile-subtitle">Your magical storytelling dashboard</p>
                </div>

            <div className="profile-details">
                <div className="detail-card">
                    <div className="detail-icon">📧</div>
                    <div className="detail-content">
                        <span className="detail-label">Email</span>
                        <span className="detail-value">{user.email}</span>
                    </div>
                </div>

                <div className="detail-card">
                    <div className="detail-icon">⭐</div>
                    <div className="detail-content">
                        <span className="detail-label">Membership</span>
                        <span className="detail-value">{user.membership || "Free"}</span>
                    </div>
                </div>

                <div className="detail-card">
                    <div className="detail-icon">📚</div>
                    <div className="detail-content">
                        <span className="detail-label">Stories Created</span>
                        <span className="detail-value">{stories.length}</span>
                    </div>
                </div>
            </div>

            <div className="profile-actions">
                <button onClick={() => navigate("/create")} className="create-story-btn">
                    <span className="button-icon">✨</span>
                    <span>Create New Story</span>
                </button>

                <button onClick={() => navigate("/upgrade")} className="upgrade-plan-btn">
                    <span className="button-icon">🚀</span>
                    <span>Upgrade Plan</span>
                </button>
            </div>

            <div className="stories-section">
                <h2 className="section-title">
                    <span className="section-icon">📖</span>
                    Your Story Collection
                </h2>

                {loading ? (
                    <div className="loading-container">
                        <div className="loading-spinner">
                            <div className="spinner"></div>
                        </div>
                        <p className="loading-text">Loading your magical stories...</p>
                    </div>
                ) : stories.length === 0 ? (
                    <div className="empty-state">
                        <div className="empty-icon">📚</div>
                        <h3>No stories yet!</h3>
                        <p>Start your storytelling journey by creating your first magical adventure.</p>
                        <button onClick={() => navigate("/create")} className="create-first-story-btn">
                            <span className="button-icon">🌟</span>
                            <span>Create Your First Story</span>
                        </button>
                    </div>
                ) : (
                    <div className="story-grid">
                        {stories.map((story) => (
                            <div key={story.id} className="story-card" onClick={() => navigate("/view", { state: { story } })}>
                                <div className="story-image-container">
                                    <img
                                        src={story.coverImageUrl || "/placeholder.svg?height=200&width=200"}
                                        alt={`Cover for ${story.title}`}
                                        className="story-image"
                                    />
                                    <div className="story-overlay">
                                        <span className="read-story-text">📖 Read Story</span>
                                    </div>
                                </div>
                                <div className="story-info">
                                    <h4 className="story-title">{story.title}</h4>
                                    <p className="story-date">{new Date(story.createdAt).toLocaleDateString()}</p>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </div>
                {showImageModal && (
                    <div className="image-modal-overlay" onClick={() => setShowImageModal(false)}>
                        <div className="image-modal" onClick={(e) => e.stopPropagation()}>
                            <div className="modal-header">
                                <h3>Choose Your Avatar</h3>
                                <button className="close-btn" onClick={() => setShowImageModal(false)}>✕</button>
                            </div>
                            <div className="image-grid">
                                {profileImages.map((imageUrl, i) => (
                                    <div
                                        key={i}
                                        className={`image-option ${selectedImage === imageUrl ? "selected" : ""}`}
                                        onClick={() => handleImageSelect(imageUrl)}
                                    >
                                        <img src={imageUrl} alt={`Avatar option ${i + 1}`} />
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    )
}

export default ProfilePage