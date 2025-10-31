import api from "../api";
import { useEffect, useState, useRef, useCallback } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import "./StoryViewerPage.css";
import { useAuth } from "../context/AuthContext";
import FeedbackModal from "../components/FeedbackModal";

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

    const [showFeedback, setShowFeedback] = useState(false);
    const [feedbackSent, setFeedbackSent] = useState(false);

    const indicatorsRef = useRef(null);
    const dotRefs = useRef([]);

    // ====== SWIPE / DRAG (only essentials) ======
    const contentRef = useRef(null); // swipe surface
    const pointer = useRef({
        active: false,
        startX: 0,
        startY: 0,
        lastX: 0,
        lastY: 0
    });

    const SWIPE_THRESHOLD = 10;  // px horizontal
    const MAX_ANGLE_DEG = 40;    // <=40° from horizontal counts
    const EDGE_GUARD = 24;       // ignore swipes that start near the left edge (OS gestures)

    // Map raw 0..180° so 0° (right) and 180° (left) both become 0° (perfect horizontal)
    const horizAngle = (dx, dy) => {
        const deg = Math.abs(Math.atan2(dy, dx)) * (180 / Math.PI);
        return Math.min(deg, 180 - deg); // 0 = horizontal, 90 = vertical
    };

    // Load story: public (by token) or private (from state/localStorage)
    useEffect(() => {
        let alive = true;

        async function load() {
            if (mode === "public" && token) {
                try {
                    const { data } = await api.get(`/share/${token}`, {
                        skipAuth401Handler: true,
                    });
                    if (alive) setStory(data);
                } catch (e) {
                    if (alive) setError("This shared story link is invalid or expired.");
                    console.error("Share load failed:", e);
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

    // Scroll active dot into view
    useEffect(() => {
        const activeIndex = currentPage === -1 ? 0 : currentPage + 1;
        const el = dotRefs.current[activeIndex];
        if (el) el.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
    }, [currentPage]);

    useEffect(() => {
        if (!story?.id) return;
        const key = `fb:${story.id}`;
        setFeedbackSent(localStorage.getItem(key) === "1");
    }, [story?.id]);

    // Auto-hide controls (only when reading)
    useEffect(() => {
        if (!isReading || !showControls) return;
        const timer = setTimeout(() => setShowControls(false), 1000000);
        return () => clearTimeout(timer);
    }, [isReading, showControls]);

    // Handlers (memoized so effects can safely depend on them)
    const nextPage = useCallback(() => {
        if (!story) return;
        setShowControls(true);
        setShowCompletion(false);
        setCurrentPage((p) => {
            if (p < (story.pages?.length ?? 0) - 1) return p + 1;
            // last page -> show completion
            setShowCompletion(true);
            return p;
        });
    }, [story]); // depends only on story length

    const prevPage = useCallback(() => {
        setShowControls(true);
        setShowCompletion(false);
        setCurrentPage((p) => (p > -1 ? p - 1 : p));
    }, []); // no external deps

    const startReading = useCallback(() => {
        setCurrentPage(0);
        setIsReading(true);
        setShowControls(true);
        setShowCompletion(false);
    }, []);

    const goToPage = (pageIndex) => {
        setCurrentPage(pageIndex);
        setShowControls(true);
        setShowCompletion(false);
    };

    // Manual finish button (also triggers feedback)
    const finishStory = () => {
        setShowCompletion(true);
        if (!feedbackSent) setShowFeedback(true);
    };

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
    };

    // Keyboard navigation (←/→)
    useEffect(() => {
        const onKey = (e) => {
            if (enlargedImage || showCompletion) return;
            if (e.key === "ArrowRight") {
                e.preventDefault();
                if (currentPage === -1) startReading();
                else nextPage();
            } else if (e.key === "ArrowLeft") {
                e.preventDefault();
                if (currentPage === -1) return;
                prevPage();
            }
        };
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, [currentPage, enlargedImage, showCompletion, startReading, nextPage, prevPage]);

    // ====== Pointer/touch swipe navigation (minimal) ======
    useEffect(() => {
        const el = contentRef.current;
        if (!el) return;

        const guard = () => enlargedImage || showCompletion;

        const isInteractive = (t) =>
            t.closest?.("button, a, input, textarea, select, [role='button'], .page-indicators, .nav-button");

        const onPointerDown = (e) => {
            if (guard()) return;
            if (e.button !== undefined && e.button !== 0) return; // primary mouse only

            const touch = e.touches ? e.touches[0] : e;
            const sx = touch.clientX;
            const sy = touch.clientY;

            // left-edge guard (avoid browser back swipe conflicts)
            if (sx < EDGE_GUARD) return;

            if (isInteractive(e.target)) return;

            pointer.current.active = true;
            pointer.current.startX = sx;
            pointer.current.startY = sy;
            pointer.current.lastX = sx;
            pointer.current.lastY = sy;
        };

        const onPointerMove = (e) => {
            if (!pointer.current.active) return;

            const touch = e.touches ? e.touches[0] : e;
            const x = touch.clientX;
            const y = touch.clientY;

            pointer.current.lastX = x;
            pointer.current.lastY = y;

            const dx = x - pointer.current.startX;
            const dy = y - pointer.current.startY;

            // Only block native scroll if the gesture is mostly horizontal
            const hAngle = horizAngle(dx, dy);
            if (hAngle <= MAX_ANGLE_DEG && e.cancelable) {
                e.preventDefault();
            }
        };

        const onPointerUp = () => {
            if (!pointer.current.active) return;

            const dx = pointer.current.lastX - pointer.current.startX;
            const dy = pointer.current.lastY - pointer.current.startY;

            pointer.current.active = false;

            const hAngle = horizAngle(dx, dy);
            const isMostlyHorizontal = hAngle <= MAX_ANGLE_DEG;
            if (!isMostlyHorizontal) return;
            if (Math.abs(dx) < SWIPE_THRESHOLD) return;

            if (dx < 0) {
                // swipe left = next
                if (currentPage === -1) startReading();
                else nextPage();
            } else {
                // swipe right = prev (no-op on cover)
                if (currentPage >= 0) prevPage();
            }
        };

        // Attach both mouse & touch
        el.addEventListener("touchstart", onPointerDown, { passive: false });
        el.addEventListener("touchmove", onPointerMove, { passive: false });
        el.addEventListener("touchend", onPointerUp);
        el.addEventListener("touchcancel", onPointerUp);
        el.addEventListener("mousedown", onPointerDown);
        window.addEventListener("mousemove", onPointerMove);
        window.addEventListener("mouseup", onPointerUp);

        return () => {
            el.removeEventListener("touchstart", onPointerDown);
            el.removeEventListener("touchmove", onPointerMove);
            el.removeEventListener("touchend", onPointerUp);
            el.removeEventListener("touchcancel", onPointerUp);
            el.removeEventListener("mousedown", onPointerDown);
            window.removeEventListener("mousemove", onPointerMove);
            window.removeEventListener("mouseup", onPointerUp);
        };
    }, [currentPage, enlargedImage, showCompletion, startReading, nextPage, prevPage]);

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
            <div className="story-content" ref={contentRef}>
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

                <div className="page-indicators" ref={indicatorsRef}>
                    <button
                        ref={(el) => (dotRefs.current[0] = el)}
                        onClick={() => goToPage(-1)}
                        className={`page-dot ${isCover ? "active" : ""}`}
                        title="Cover"
                    >
                        <span className="dot-icon">📖</span>
                    </button>

                    {story.pages.map((_, index) => (
                        <button
                            key={index}
                            ref={(el) => (dotRefs.current[index + 1] = el)}
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

                            {!feedbackSent ? (
                                <button onClick={() => setShowFeedback(true)} className="give-feedback-btn">
                                    <span className="button-icon">💬</span>
                                    Give Feedback
                                </button>
                            ) : (
                                <div className="give-feedback-sent" aria-live="polite" title="Feedback sent">
                                    ✅ Feedback sent — thank you!
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Feedback Modal */}
            <FeedbackModal
                open={showFeedback && !feedbackSent}
                onClose={() => setShowFeedback(false)}
                storyMeta={{
                    id: story?.id,
                    title: story?.title,
                    pageCount: story?.pages?.length ?? 0,
                    estReadMin: Math.ceil((story?.pages?.length ?? 0) * 1.5),
                }}
                apiBase="/api"
                emailTargets={[
                    //"austintylerdevelopment@gmail.com",
                    "support@starlitstories.app",
                ]}
                onSubmitted={() => {
                    setFeedbackSent(true);
                    if (story?.id) localStorage.setItem(`fb:${story.id}`, "1");
                }}
            />
        </div>
    );
}