"use client";

import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import "./StoryViewerPage.css";
import { useAuth } from "../context/AuthContext";

export default function StoryViewerPage({ mode = "private" }) {
    const navigate = useNavigate();
    const { state } = useLocation();           // carries { story } on private route
    const { token } = useParams();             // present only on /s/:token
    const { user } = useAuth();

    const [story, setStory] = useState(null);
    const [loading, setLoading] = useState(mode === "public" && !!token);
    const [error, setError] = useState("");

    const [currentPage, setCurrentPage] = useState(-1); // -1 = cover
    const [isReading, setIsReading] = useState(false);
    const [showControls, setShowControls] = useState(true);
    const [enlargedImage, setEnlargedImage] = useState(null);
    const [showCompletion, setShowCompletion] = useState(false);

    // Load story: public (by token) or private (from state/localStorage)
    useEffect(() => {
        let alive = true;

        async function load() {
            if (mode === "public" && token) {
                try {
                    const res = await fetch(`/api/share/${token}`);
                    if (!res.ok) throw new Error("Link expired or not found.");
                    const data = await res.json(); // { id, title, coverImageUrl, pages:[...] }
                    if (alive) setStory(data);
                } catch (e) {
                    if (alive) setError(e.message || String(e));
                } finally {
                    if (alive) setLoading(false);
                }
                return;
            }

            // private
            if (state?.story) {
                if (alive) {
                    setStory(state.story);
                    localStorage.setItem("story", JSON.stringify(state.story));
                }
            } else {
                const saved = localStorage.getItem("story");
                if (saved && alive) setStory(JSON.parse(saved));
            }
        }

        load();
        return () => { alive = false; };
    }, [mode, token, state]);

    // Auto-hide controls (only when reading)
    useEffect(() => {
        if (!isReading || !showControls) return;
        const timer = setTimeout(() => setShowControls(false), 1000000);
        return () => clearTimeout(timer);
    }, [isReading, showControls]);

    // Handlers
    const nextPage = () => {
        if (!story) return;
        if (currentPage < story.pages.length - 1) {
            setCurrentPage((p) => p + 1);
            setShowControls(true);
        } else if (currentPage === story.pages.length - 1) {
            setShowCompletion(true);
        }
    };

    const prevPage = () => {
        if (currentPage > -1) {
            setCurrentPage((p) => p - 1);
            setShowControls(true);
            setShowCompletion(false);
        }
    };

    const startReading = () => {
        setCurrentPage(0);
        setIsReading(true);
        setShowControls(true);
        setShowCompletion(false);
    };

    const goToPage = (pageIndex) => {
        setCurrentPage(pageIndex);
        setShowControls(true);
        setShowCompletion(false);
    };

    const finishStory = () => setShowCompletion(true);
    const readAgain = () => {
        setCurrentPage(-1);
        setIsReading(false);
        setShowCompletion(false);
        setShowControls(true);
    };

    const enlargeImage = (imageUrl, e) => {
        e.stopPropagation();
        setEnlargedImage(imageUrl);
    };
    const closeEnlargedImage = () => setEnlargedImage(null);

    const handleContentClick = (e) => {
        if (
            e.target.closest(".story-header") ||
            e.target.closest(".story-navigation") ||
            e.target.closest(".completion-overlay") ||
            e.target.closest("button") ||
            e.target.closest(".page-image-container")
        ) return;

        if (isReading && currentPage >= 0) {
            setShowControls((v) => !v);
        }
    };

    // Loading / error states
    if (loading) return <div className="page pad">Loading…</div>;
    if (error) return <div className="page pad">⚠️ {error}</div>;

    // No story
    if (!story || !Array.isArray(story.pages)) {
        return (
            <div className="story-viewer">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="error-container">
                    <div className="error-content">
                        <div className="error-icon">📚</div>
                        <h2>Oops! No story found.</h2>
                        <p>It looks like your magical story got lost in the clouds!</p>
                        <button onClick={() => navigate("/create")} className="create-new-btn">
                            <span className="button-icon">✨</span>
                            Create New Story
                        </button>
                        <button onClick={() => navigate("/profile")} className="back-to-profile-btn">
                            <span className="button-icon">👤</span>
                            Back to Profile
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    // Render cover/pages
    const isCover = currentPage === -1;
    const page = isCover ? null : story.pages[currentPage];
    const isLastPage = !isCover && currentPage === story.pages.length - 1;

    return (
        <div className="story-viewer" onClick={handleContentClick}>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            {/* Header Controls (progress only) */}
            <div className={`story-header only-progress ${showControls ? "visible" : "hidden"}`}>
                <div className="story-progress">
                    <span className="progress-text">
                        {isCover ? "Cover" : `Page ${currentPage + 1} of ${story.pages.length}`}
                    </span>
                    <div className="progress-bar">
                        <div
                            className="progress-fill"
                            style={{
                                width: isCover ? "0%" : `${((currentPage + 1) / story.pages.length) * 100}%`,
                            }}
                        />
                    </div>
                </div>
            </div>

            {/* Main Content */}
            <div className="story-content">
                {isCover ? (
                    <div className="cover-page">
                        <div className="cover-container">
                            <h1 className="story-title">{story.title}</h1>
                            <div className="cover-image-container">
                                <img
                                    src={story.coverImageUrl || "/placeholder.svg"}
                                    alt="Story Cover"
                                    className="cover-image"
                                    onClick={(e) => enlargeImage(story.coverImageUrl || "/placeholder.svg", e)}
                                />
                                <div className="cover-overlay">
                                    <button onClick={startReading} className="start-reading-btn">
                                        <span className="button-icon">📖</span>
                                        Start Reading
                                    </button>
                                </div>
                            </div>
                            <div className="cover-details">
                                <p className="story-info">A magical adventure awaits!</p>
                                <div className="story-stats">
                                    <span className="stat">
                                        <span className="stat-icon">📄</span>
                                        {story.pages.length} pages
                                    </span>
                                    <span className="stat">
                                        <span className="stat-icon">⏱️</span>
                                        ~{Math.ceil(story.pages.length * 1.5)} min read
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className="story-page">
                        <div className="page-container">
                            <div className="page-image-container">
                                <img
                                    src={page.imageUrl || "/placeholder.svg"}
                                    alt={`Page ${currentPage + 1}`}
                                    className="page-image"
                                    onClick={(e) => enlargeImage(page.imageUrl || "/placeholder.svg", e)}
                                />
                                <div className="image-enlarge-hint">
                                    <span className="enlarge-icon">🔍</span>
                                    <span className="enlarge-text">Click to enlarge</span>
                                </div>
                            </div>
                            <div className="page-text-container">
                                <p className="page-text">{page.text}</p>
                                {isLastPage && (
                                    <div className="finish-story-section">
                                        <button onClick={finishStory} className="finish-story-btn">
                                            <span className="button-icon">🌟</span>
                                            Finish Story
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}
            </div>

            {/* Navigation Controls */}
            <div className={`story-navigation ${showControls ? "visible" : "hidden"}`}>
                <button onClick={prevPage} disabled={currentPage === -1} className="nav-button prev-button">
                    <span className="nav-icon">←</span>
                    <span className="nav-text">Previous</span>
                </button>

                <div className="page-indicators">
                    <button onClick={() => goToPage(-1)} className={`page-dot ${isCover ? "active" : ""}`} title="Cover">
                        <span className="dot-icon">📖</span>
                    </button>
                    {story.pages.map((_, index) => (
                        <button
                            key={index}
                            onClick={() => goToPage(index)}
                            className={`page-dot ${currentPage === index ? "active" : ""}`}
                            title={`Page ${index + 1}`}
                        >
                            {index + 1}
                        </button>
                    ))}
                </div>

                <button onClick={nextPage} className="nav-button next-button">
                    <span className="nav-text">{isLastPage ? "Finish" : "Next"}</span>
                    <span className="nav-icon">{isLastPage ? "🌟" : "→"}</span>
                </button>
            </div>

            {/* Image Enlargement Modal */}
            {enlargedImage && (
                <div className="image-modal" onClick={closeEnlargedImage}>
                    <div className="image-modal-content">
                        <button className="close-modal-btn" onClick={closeEnlargedImage}>
                            <span>✕</span>
                        </button>
                        <img src={enlargedImage || "/placeholder.svg"} alt="Enlarged view" className="enlarged-image" />
                        <div className="modal-hint">
                            <p>Click anywhere to close</p>
                        </div>
                    </div>
                </div>
            )}

            {/* Completion Screen */}
            {showCompletion && (
                <div className="completion-overlay visible">
                    <div className="completion-content">
                        <div className="completion-icon">🌟</div>
                        <h2>The End!</h2>
                        <p>What a magical adventure! Did you enjoy the story?</p>
                        <div className="completion-actions">
                            <button onClick={readAgain} className="read-again-btn">
                                <span className="button-icon">🔄</span>
                                Read Again
                            </button>
                            <button onClick={() => navigate("/create")} className="create-another-btn">
                                <span className="button-icon">✨</span>
                                Create Another Story
                            </button>
                            <button onClick={() => navigate("/profile")} className="back-profile-btn">
                                <span className="button-icon">👤</span>
                                Back to Profile
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Reading Instructions */}
            {/*{!isReading && !isCover && (*/}
            {/*    <div className="reading-hint">*/}
            {/*        <p>💡 Tap anywhere to show/hide controls</p>*/}
            {/*    </div>*/}
            {/*)}*/}
        </div>
    );
}