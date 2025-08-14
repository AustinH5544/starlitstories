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

    const [showImageModal, setShowImageModal] = useState(false)
    const [selectedImage, setSelectedImage] = useState(user?.profileImage || "/default-avatar.png")

    const profileImages = [
        "avatars/wizard-avatar.png",
        "avatars/princess-avatar.png",
        "avatars/knight-avatar.png",
        "avatars/whimsical-fairy-avatar.png",
        "avatars/dragon-avatar.png",
        "avatars/unicorn-avatar.png",
        "avatars/pirate-avatar.png",
        "avatars/astronaut-avatar.png",
        "avatars/whimsical-mermaid-avatar.png",
        "avatars/superhero-avatar.png",
        "avatars/cat-avatar.png",
        "placeholder.svg?height=100&width=100",
    ]

    const handleImageSelect = (imageUrl) => {
        setSelectedImage(imageUrl)
        // Here you would typically save to backend
        // await api.put('/profile/image', { profileImage: imageUrl })
        setShowImageModal(false)
    }

    const openImageModal = () => {
        setShowImageModal(true)
    }

    const closeImageModal = () => {
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

        if (user?.email) {
            fetchStories()
        }
    }, [user])

    if (!user) {
        return (
            <div className="profile-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>
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
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            <div className="profile-container">
                <div className="profile-header">
                    <div className="profile-avatar-container">
                        <div className="user-avatar-large" onClick={openImageModal}>
                            <img
                                src={selectedImage || "/placeholder.svg"}
                                alt="Profile"
                                className="avatar-image"
                                onError={(e) => {
                                    e.target.style.display = "none"
                                    e.target.nextSibling.style.display = "flex"
                                }}
                            />
                            <div className="avatar-fallback">
                                {user.name ? user.name.charAt(0).toUpperCase() : user.email.charAt(0).toUpperCase()}
                            </div>
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
                    <div className="image-modal-overlay" onClick={closeImageModal}>
                        <div className="image-modal" onClick={(e) => e.stopPropagation()}>
                            <div className="modal-header">
                                <h3>Choose Your Avatar</h3>
                                <button className="close-btn" onClick={closeImageModal}>
                                    ✕
                                </button>
                            </div>
                            <div className="image-grid">
                                {profileImages.map((imageUrl, index) => (
                                    <div
                                        key={index}
                                        className={`image-option ${selectedImage === imageUrl ? "selected" : ""}`}
                                        onClick={() => handleImageSelect(imageUrl)}
                                    >
                                        <img src={imageUrl || "/placeholder.svg"} alt={`Avatar option ${index + 1}`} />
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