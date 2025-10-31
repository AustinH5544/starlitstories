import api from "../api";
import { useEffect, useState, useRef, useCallback } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import "./StoryViewerPage.css";
import { useAuth } from "../context/AuthContext";
import FeedbackModal from "../components/FeedbackModal";

export default function StoryViewerPage({ mode = "private" }) {
    const navigate = useNavigate();
    const { state } = useLocation();
    const { token } = useParams();
    const { user } = useAuth();

    // ---------- Book-mode eligibility (>= 768 x 1024) ----------
    const computeEligibility = () => {
        const w = window.innerWidth || 0;
        const h = window.innerHeight || 0;
        const minSide = Math.min(w, h);
        const maxSide = Math.max(w, h);
        return minSide >= 768 && maxSide >= 1024;
    };
    const [bookEligible, setBookEligible] = useState(() => computeEligibility());

    useEffect(() => {
        const onResize = () => setBookEligible(computeEligibility());
        window.addEventListener("resize", onResize);
        window.addEventListener("orientationchange", onResize);
        return () => {
            window.removeEventListener("resize", onResize);
            window.removeEventListener("orientationchange", onResize);
        };
    }, []);

    const [story, setStory] = useState(null);
    const [loading, setLoading] = useState(mode === "public" && !!token);
    const [error, setError] = useState("");

    // In Book Mode, currentPage is the RIGHT page index of the open spread
    const [currentPage, setCurrentPage] = useState(-1); // -1 = cover
    const [isReading, setIsReading] = useState(false);
    const [showControls, setShowControls] = useState(true);
    const [showCompletion, setShowCompletion] = useState(false);

    const [showFeedback, setShowFeedback] = useState(false);
    const [feedbackSent, setFeedbackSent] = useState(false);

    // ===== Book Mode & Flip =====
    const [bookMode, setBookMode] = useState(() => localStorage.getItem("bookMode") === "1");
    const isBook = bookMode && bookEligible;

    useEffect(() => {
        if (!bookEligible && bookMode) {
            setBookMode(false);
            localStorage.setItem("bookMode", "0");
        }
    }, [bookEligible, bookMode]);

    const [isFlipping, setIsFlipping] = useState(false);
    // flipDir: "next" | "prev" | "to-cover" | null
    const [flipDir, setFlipDir] = useState(null);
    const performAfterFlip = useRef(null);

    // opening choreography (slide then flip from left edge)
    const [openingFromCover, setOpeningFromCover] = useState(false);

    // Halfway swap timer (to render next page mid-animation)
    const FLIP_MS = 650;
    const HALF_FLIP_MS = Math.round(FLIP_MS / 2);
    const flipHalfTimer = useRef(null);
    const pendingTargetAfterOpen = useRef(null);
    const [openingTargetRight, setOpeningTargetRight] = useState(null);

    const indicatorsRef = useRef(null);
    const dotRefs = useRef([]);
    const contentRef = useRef(null);

    // ===== NEW: flip queuing to play multi-step close sequence =====
    const flipQueue = useRef([]);              // [{ dir: "prev"|"to-cover", targetRight?: number }, ...]
    const currentPageRef = useRef(currentPage);
    useEffect(() => { currentPageRef.current = currentPage; }, [currentPage]);

    const startFlip = useCallback((dir, targetRight, afterEnd) => {
        // Prevent starting if a flip is already running
        if (isFlipping) return;

        setFlipDir(dir);
        setIsFlipping(true);

        if (dir === "prev" || dir === "next") {
            // For normal page turns, we update the visible page at half time
            if (flipHalfTimer.current) clearTimeout(flipHalfTimer.current);
            flipHalfTimer.current = setTimeout(() => {
                setCurrentPage(targetRight);
            }, HALF_FLIP_MS);
            performAfterFlip.current = () => {
                setCurrentPage(targetRight);
                if (afterEnd) afterEnd();
            };
        } else if (dir === "to-cover") {
            // Special close: jump to cover at the end of the animation
            performAfterFlip.current = () => {
                setCurrentPage(-1);
                if (afterEnd) afterEnd();
            };
        }
    }, [HALF_FLIP_MS, isFlipping]);

    // ====== SWIPE / DRAG (only essentials) ======
    const pointer = useRef({ active: false, startX: 0, startY: 0, lastX: 0, lastY: 0 });
    const SWIPE_THRESHOLD = 10;
    const MAX_ANGLE_DEG = 40;
    const EDGE_GUARD = 24;

    const horizAngle = (dx, dy) => {
        const deg = Math.abs(Math.atan2(dy, dx)) * (180 / Math.PI);
        return Math.min(deg, 180 - deg);
    };

    // Load story
    useEffect(() => {
        let alive = true;
        async function load() {
            if (mode === "public" && token) {
                try {
                    const { data } = await api.get(`/share/${token}`, { skipAuth401Handler: true });
                    if (alive) setStory(data);
                } catch (e) {
                    if (alive) setError("This shared story link is invalid or expired.");
                    console.error("Share load failed:", e);
                } finally {
                    if (alive) setLoading(false);
                }
                return;
            }
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

    const pageCount = story?.pages?.length ?? 0;

    // --- helpers for book spreads ---
    const lastRightIndex = (() => {
        if (pageCount <= 0) return -1;
        const last = pageCount - 1;
        if (last % 2 === 1) return last;            // odd -> already right page
        return (pageCount - 2) >= 1 ? (pageCount - 2) : 0; // fallback to 0 if only one page
    })();

    const isCover = currentPage === -1;
    const isLastSpread = !isCover && currentPage >= lastRightIndex;
    const onFirstSpread = !isCover && currentPage <= 1;

    // ----- spread helpers -----
    const totalSpreads = Math.ceil(pageCount / 2);
    const spreadOf = (idx) => Math.ceil((idx + 1) / 2);
    const rightIndexOfSpread = (s) => Math.min(s * 2 - 1, pageCount - 1);
    const spreadNumber = isCover ? 0 : spreadOf(currentPage);

    // Scroll active dot into view
    useEffect(() => {
        let activeIndex;
        if (isBook) {
            activeIndex = isCover ? 0 : spreadOf(currentPage);
        } else {
            activeIndex = isCover ? 0 : currentPage + 1;
        }
        const el = dotRefs.current[activeIndex];
        if (el) el.scrollIntoView({ behavior: "smooth", block: "nearest", inline: "center" });
    }, [isBook, currentPage, isCover, pageCount]);

    useEffect(() => {
        if (!story?.id) return;
        setFeedbackSent(localStorage.getItem(`fb:${story.id}`) === "1");
    }, [story?.id]);

    // Auto-hide controls
    useEffect(() => {
        if (!isReading || !showControls) return;
        const t = setTimeout(() => setShowControls(false), 1000000);
        return () => clearTimeout(t);
    }, [isReading, showControls]);

    // ---------- FACE MAPPING ----------
    const isClosingToCover = isFlipping && flipDir === "to-cover";
    const leftIndex = Math.max(currentPage - 1, -1);
    const rightIndex = currentPage;

    // Default mapping
    let computedFrontIndex =
        flipDir === "next" ? (isCover ? -1 : rightIndex) :
            flipDir === "prev" ? leftIndex :
                flipDir === "to-cover" ? leftIndex : null;

    let computedBackIndex =
        flipDir === "next" ? (isCover ? 0 : Math.min(currentPage + 1, pageCount - 1)) :
            flipDir === "prev" ? rightIndex :
                flipDir === "to-cover" ? -1 : null;

    // Override mapping specifically for OPENING FROM COVER
    if (openingFromCover && isFlipping && flipDir === "next") {
        // Front of the sheet is the cover; back should show the destination right page
        computedFrontIndex = -1;
        // If we already know the destination, show it; otherwise fall back to page 1 (safe default)
        const fallbackRight = pageCount >= 2 ? 1 : 0;
        computedBackIndex = openingTargetRight ?? fallbackRight;
    }

    const frontIndex = computedFrontIndex;
    const backIndex = computedBackIndex;

    // --- Navigation (Book Mode moves by TWO pages) ---
    const nextPage = useCallback(() => {
        if (!story) return;

        if (isBook) {
            if (isCover && pageCount === 0) return;
            if (!isCover && (currentPage >= lastRightIndex)) {
                setShowCompletion(true);
                return;
            }
        } else {
            if (currentPage >= pageCount - 1) {
                setShowCompletion(true);
                return;
            }
        }

        setShowControls(true);
        setShowCompletion(false);

        if (isBook) {
            if (isFlipping) return;
            setFlipDir("next");
            setIsFlipping(true);

            const targetRight = isCover
                ? (pageCount >= 2 ? 1 : 0)
                : Math.min(currentPage + 2, lastRightIndex);

            if (flipHalfTimer.current) clearTimeout(flipHalfTimer.current);
            flipHalfTimer.current = setTimeout(() => {
                setCurrentPage(targetRight);
            }, HALF_FLIP_MS);

            performAfterFlip.current = () => setCurrentPage(targetRight);
        } else {
            setCurrentPage((p) => Math.min(p + 1, pageCount - 1));
        }
    }, [story, isBook, isCover, currentPage, pageCount, lastRightIndex, isFlipping, HALF_FLIP_MS]);

    const prevPage = useCallback(() => {
        if (isCover) return;
        setShowControls(true);
        setShowCompletion(false);

        if (isBook) {
            if (isFlipping) return;

            if (currentPage <= 1) {
                setFlipDir("to-cover");
                setIsFlipping(true);
                performAfterFlip.current = () => setCurrentPage(-1);
                return;
            }

            setFlipDir("prev");
            setIsFlipping(true);

            const targetRight = currentPage - 2;

            if (flipHalfTimer.current) clearTimeout(flipHalfTimer.current);
            flipHalfTimer.current = setTimeout(() => {
                setCurrentPage(targetRight);
            }, HALF_FLIP_MS);

            performAfterFlip.current = () => setCurrentPage(targetRight);
        } else {
            setCurrentPage((p) => (p > -1 ? p - 1 : p));
        }
    }, [isBook, isCover, currentPage, isFlipping, HALF_FLIP_MS]);

    const startReading = useCallback(() => {
        setIsReading(true);
        setShowControls(true);
        setShowCompletion(false);

        if (isBook) {
            if (isFlipping) return;

            setOpeningFromCover(true);
            setFlipDir("next");
            setIsFlipping(true);
            const firstRight = pageCount >= 2 ? 1 : 0;

            if (flipHalfTimer.current) clearTimeout(flipHalfTimer.current);
            flipHalfTimer.current = setTimeout(() => {
                setCurrentPage(firstRight);
            }, HALF_FLIP_MS);

            performAfterFlip.current = () => setCurrentPage(firstRight);
        } else {
            setCurrentPage(0);
        }
    }, [isBook, isFlipping, pageCount, HALF_FLIP_MS]);

    // build & run multi-step sequence to close to cover from anywhere =====
    const playCloseToCoverSequence = useCallback(() => {
        if (!isBook || isCover || isFlipping) return;

        if (currentPage > 1) {
            // 1) Snap state to the first spread (no animation)
            setCurrentPage(1);

            // 2) On the next frame, trigger the close animation so faces map to 0 (left) and -1 (cover)
            requestAnimationFrame(() => {
                if (!isFlipping) startFlip("to-cover"); // no targetRight needed for "to-cover"
            });
        } else {
            // Already on first spread → just close
            startFlip("to-cover");
        }
    }, [isBook, isCover, isFlipping, currentPage, startFlip]);

    const goToPage = (pageIndex) => {
        if (pageIndex === currentPage) return;
        setShowControls(true);
        setShowCompletion(false);

        if (!isBook) {
            setCurrentPage(pageIndex);
            return;
        }
        if (isFlipping) return;

        // === Cover clicked → any page: play opening animation showing the target spread ===
        if (isCover && pageIndex !== -1) {
            // compute desired targetRight (right page of target spread)
            let desiredRight = pageIndex;
            if (desiredRight % 2 === 0) {
                desiredRight = (desiredRight + 1 <= pageCount - 1) ? desiredRight + 1 : desiredRight;
            }
            if (desiredRight > lastRightIndex) desiredRight = lastRightIndex;

            // record which spread we’re opening to
            setOpeningTargetRight(desiredRight);

            // kick the special opening choreography (full-width edge-open)
            setOpeningFromCover(true);

            // IMPORTANT: use desiredRight for the mid-flip swap so the destination spread
            // is visible during the second half of the animation.
            startFlip("next", desiredRight, () => {
                // after open finishes, clear flags; we're already on desiredRight
                setOpeningFromCover(false);
                setOpeningTargetRight(null);
            });
            return;
        }

        // === Existing: cover dot (to-cover) handled elsewhere ===
        if (pageIndex === -1) {
            playCloseToCoverSequence();
            return;
        }

        // === Normal jump between spreads while not on cover ===
        let targetRight = pageIndex;
        if (targetRight % 2 === 0) {
            targetRight = (targetRight + 1 <= pageCount - 1) ? targetRight + 1 : targetRight;
        }
        if (targetRight > lastRightIndex) targetRight = lastRightIndex;

        setFlipDir(targetRight > currentPage ? "next" : "prev");
        setIsFlipping(true);

        if (flipHalfTimer.current) clearTimeout(flipHalfTimer.current);
        flipHalfTimer.current = setTimeout(() => {
            setCurrentPage(targetRight);
        }, HALF_FLIP_MS);

        performAfterFlip.current = () => setCurrentPage(targetRight);
    };

    const onFlipEnd = () => {
        if (flipHalfTimer.current) {
            clearTimeout(flipHalfTimer.current);
            flipHalfTimer.current = null;
        }
        if (performAfterFlip.current) performAfterFlip.current();
        performAfterFlip.current = null;

        setIsFlipping(false);
        setFlipDir(null);
        setOpeningFromCover(false);
    };

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

    const handleContentClick = (e) => {
        if (
            e.target.closest(".story-header") ||
            e.target.closest(".story-navigation") ||
            e.target.closest(".completion-overlay") ||
            e.target.closest("button") ||
            e.target.closest(".page-image-container")
        ) return;
    };

    // Keyboard navigation
    useEffect(() => {
        const onKey = (e) => {
            if (showCompletion) return;
            if (e.key === "ArrowRight") {
                e.preventDefault();
                if (isCover) startReading();
                else nextPage();
            } else if (e.key === "ArrowLeft") {
                e.preventDefault();
                if (isCover) return;
                prevPage();
            }
        };
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, [isCover, showCompletion, startReading, nextPage, prevPage]);

    // Pointer/touch swipe navigation
    useEffect(() => {
        const el = contentRef.current;
        if (!el) return;

        const guard = () => showCompletion || isFlipping;
        const isInteractive = (t) =>
            t.closest?.("button, a, input, textarea, select, [role='button'], .page-indicators, .nav-button");

        const onPointerDown = (e) => {
            if (guard()) return;
            if (e.button !== undefined && e.button !== 0) return;
            const touch = e.touches ? e.touches[0] : e;
            const sx = touch.clientX, sy = touch.clientY;
            if (sx < EDGE_GUARD) return;
            if (isInteractive(e.target)) return;
            pointer.current = { active: true, startX: sx, startY: sy, lastX: sx, lastY: sy };
        };

        const onPointerMove = (e) => {
            if (!pointer.current.active) return;
            const t = e.touches ? e.touches[0] : e;
            const x = t.clientX, y = t.clientY;
            pointer.current.lastX = x; pointer.current.lastY = y;
            const dx = x - pointer.current.startX, dy = y - pointer.current.startY;
            if (horizAngle(dx, dy) <= MAX_ANGLE_DEG && e.cancelable) e.preventDefault();
        };

        const onPointerUp = () => {
            if (!pointer.current.active) return;
            const dx = pointer.current.lastX - pointer.current.startX;
            const dy = pointer.current.lastY - pointer.current.startY;
            pointer.current.active = false;
            if (horizAngle(dx, dy) > MAX_ANGLE_DEG) return;
            if (Math.abs(dx) < SWIPE_THRESHOLD) return;
            if (dx < 0) { if (isCover) startReading(); else nextPage(); }
            else { if (!isCover) prevPage(); }
        };

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
    }, [isCover, showCompletion, startReading, nextPage, prevPage, isFlipping]);

    // Early exits
    if (loading) return <div className="page pad">Loading…</div>;
    if (error) return <div className="page pad">⚠️ {error}</div>;
    if (!story || !Array.isArray(story.pages)) {
        return (
            <div className="story-viewer">
                <div className="stars"></div><div className="twinkling"></div><div className="clouds"></div>
                <div className="error-container">
                    <div className="error-content">
                        <div className="error-icon">📚</div>
                        <h2>Oops! No story found.</h2>
                        <p>It looks like your magical story got lost in the clouds!</p>
                        <button onClick={() => navigate("/create")} className="create-new-btn"><span className="button-icon">✨</span>Create New Story</button>
                        <button onClick={() => navigate("/profile")} className="back-to-profile-btn"><span className="button-icon">👤</span>Back to Profile</button>
                    </div>
                </div>
            </div>
        );
    }

    const page = isCover ? null : story.pages[currentPage];
    const isLastPage = !isCover && currentPage === story.pages.length - 1;

    // Face renderer for a page index (-1 = cover)
    const PageFace = ({ idx }) => {
        if (idx === -1) {
            return (
                <div className="paper-face-content">
                    <div className="paper-cover">
                        <div className="cover-ribbon" aria-hidden />
                        <div className="cover-badge" aria-hidden>⭐ Reader Favorite</div>

                        <div className="cover-art">
                            <img
                                src={story.coverImageUrl || "/placeholder.svg"}
                                alt="Story Cover"
                                className="cover-image"
                            />
                            <div className="cover-vignette" aria-hidden />
                            <div className="cover-sparkles" aria-hidden />
                        </div>

                        <h1 className="story-title cover-title">
                            <span className="foil">{story.title}</span>
                            <span className="foil-shine" aria-hidden />
                        </h1>
                    </div>
                </div>
            );
        }
        const pg = story.pages[idx];
        if (!pg) return <div className="paper-face-content" />;
        const pageNo = idx + 1;
        const sideClass = idx % 2 === 0 ? "left" : "right";

        return (
            <div className="paper-face-content">
                <div className="paper-page">
                    <div className="page-image-container">
                        <img
                            src={pg.imageUrl || "/placeholder.svg"}
                            alt={`Page ${idx + 1}`}
                            className="page-image"
                        />
                    </div>
                    <div className="page-text-container">
                        <p className="page-text">{pg.text}</p>
                        {idx === story.pages.length - 1 && (
                            <div className="finish-inline">
                                <button onClick={finishStory} className="finish-story-btn">
                                    <span className="button-icon">🌟</span>
                                    Finish Story
                                </button>
                            </div>
                        )}
                    </div>
                </div>
                {isBook && <div className={`page-number ${sideClass}`}>{pageNo}</div>}
            </div>
        );
    };

    const SpreadBackFace = ({ rightIdx }) => {
        if (rightIdx == null) return null;
        const leftIdx = Math.max(rightIdx - 1, 0);

        return (
            <div className="spread-back">
                <div className="spread-half left">
                    <PageFace idx={leftIdx} />
                </div>
                <div className="spread-half right">
                    <PageFace idx={rightIdx} />
                </div>
            </div>
        );
    };

    // Header progress (hidden in effective Book Mode)
    const headerText = !isBook
        ? (isCover ? "Cover" : `Page ${currentPage + 1} of ${story.pages.length}`)
        : "";
    const headerFillWidth = !isBook
        ? (isCover ? "0%" : `${((currentPage + 1) / story.pages.length) * 100}%`)
        : "0%";

    // During close-to-cover we keep the right page visible
    const showCoverOnRight = false; // intentionally false; right stays page 2 while closing

    return (
        <div
            className={`story-viewer ${isBook ? "is-book" : "is-classic"}`}
            onClick={handleContentClick}
        >
            <div className="stars"></div><div className="twinkling"></div><div className="clouds"></div>

            {!isBook && (
                <div className={`story-header only-progress ${showControls ? "visible" : "hidden"}`}>
                    <div className="story-progress">
                        <span className="progress-text">{headerText}</span>
                        <div className="progress-bar">
                            <div className="progress-fill" style={{ width: headerFillWidth }} />
                        </div>
                    </div>
                </div>
            )}

            <div className="story-content" ref={contentRef}>
                {isBook ? (
                    <div className="book-wrapper">
                        <div
                            className={[
                                "book",
                                ((isCover && !isClosingToCover) ||
                                    (openingFromCover && isFlipping && flipDir === "next"))
                                    ? "cover-state" : "",
                                openingFromCover && isFlipping && flipDir === "next" ? "opening-from-cover" : "",
                                isClosingToCover ? "closing-to-cover" : "",
                            ].join(" ")}
                        >
                            {/* LEFT column (hide while closing to cover) */}
                            {!isCover && !isClosingToCover && (
                                <div className="book-left">
                                    <div className="paper static">
                                        <PageFace idx={currentPage - 1} />
                                    </div>
                                </div>
                            )}

                            {/* RIGHT column — keep current right page visible */}
                            <div className="book-right">
                                <div className={`paper static ${showCoverOnRight ? "keep-visible" : ""}`}>
                                    {isCover ? (
                                        <>
                                            <PageFace idx={-1} />
                                            <div className="cover-overlay">
                                                <button onClick={startReading} className="start-reading-btn">
                                                    <span className="button-icon">📖</span>Start Reading
                                                </button>
                                            </div>
                                        </>
                                    ) : (
                                        <PageFace idx={showCoverOnRight ? -1 : currentPage} />
                                    )}
                                </div>
                            </div>

                            {/* Flipping sheet */}
                            {isFlipping && (
                                <div
                                    className={[
                                        "paper sheet",
                                        flipDir === "next"
                                            ? (openingFromCover ? "edge-open" : "turn-left")
                                            : (flipDir === "prev"
                                                ? "turn-right"
                                                : (flipDir === "to-cover" ? "close-book" : "")),
                                    ].join(" ")}
                                    onAnimationEnd={onFlipEnd}
                                >
                                    <div className="paper-face front">
                                        <PageFace idx={frontIndex} />
                                    </div>
                                    <div className="paper-face back">
                                        {openingFromCover && isFlipping && flipDir === "next" ? (
                                            <SpreadBackFace rightIdx={openingTargetRight ?? (pageCount >= 2 ? 1 : 0)} />
                                        ) : (
                                            <PageFace idx={backIndex} />
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                ) : (
                    <>
                        {isCover ? (
                            <div className="cover-page">
                                <div className="cover-container">
                                    <h1 className="story-title">{story.title}</h1>
                                    <div className="cover-image-container">
                                        <img
                                            src={story.coverImageUrl || "/placeholder.svg"}
                                            alt="Story Cover"
                                            className="cover-image"
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
                                            <span className="stat"><span className="stat-icon">📄</span>{story.pages.length} pages</span>
                                            <span className="stat"><span className="stat-icon">⏱️</span>~{Math.ceil(story.pages.length * 1.5)} min read</span>
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
                                        />
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
                    </>
                )}
            </div>

            {/* Navigation Controls */}
            <div className={`story-navigation ${showControls ? "visible" : "hidden"}`}>
                <button
                    onClick={prevPage}
                    disabled={isCover || isFlipping}
                    className="nav-button prev-button"
                    title={isBook && onFirstSpread ? "Close book" : "Previous"}
                >
                    <span className="nav-icon">←</span>
                    <span className="nav-text">{isBook && onFirstSpread ? "Close" : "Previous"}</span>
                </button>

                <div className="nav-middle">
                    {bookEligible && (
                        <label className="flip-toggle" title="Toggle book mode">
                            <input
                                type="checkbox"
                                checked={bookMode}
                                onChange={(e) => {
                                    const on = e.target.checked;
                                    const final = on && bookEligible;
                                    setBookMode(final);
                                    localStorage.setItem("bookMode", final ? "1" : "0");
                                }}
                            />
                            <span>Book mode</span>
                        </label>
                    )}

                    <div className="page-indicators" ref={indicatorsRef}>
                        <button
                            ref={(el) => (dotRefs.current[0] = el)}
                            onClick={() => goToPage(-1)}
                            className={`page-dot ${isCover ? "active" : ""}`}
                            title="Cover"
                            disabled={isFlipping}
                        >
                            <span className="dot-icon">📖</span>
                        </button>

                        {isBook
                            ? (
                                Array.from({ length: totalSpreads }, (_, i) => {
                                    const spreadNum = i + 1;
                                    const active = !isCover && spreadOf(currentPage) === spreadNum;
                                    return (
                                        <button
                                            key={`spread-${spreadNum}`}
                                            ref={(el) => (dotRefs.current[spreadNum] = el)}
                                            onClick={() => goToPage(rightIndexOfSpread(spreadNum))}
                                            className={`page-dot ${active ? "active" : ""}`}
                                            title={`Spread ${spreadNum}`}
                                            disabled={isFlipping}
                                        >
                                            {spreadNum}
                                        </button>
                                    );
                                })
                            )
                            : (
                                story.pages.map((_, index) => (
                                    <button
                                        key={index}
                                        ref={(el) => (dotRefs.current[index + 1] = el)}
                                        onClick={() => goToPage(index)}
                                        className={`page-dot ${currentPage === index ? "active" : ""}`}
                                        title={`Page ${index + 1}`}
                                        disabled={isFlipping}
                                    >
                                        {index + 1}
                                    </button>
                                ))
                            )
                        }
                    </div>
                </div>

                <button
                    onClick={nextPage}
                    disabled={isFlipping}
                    className="nav-button next-button"
                >
                    <span className="nav-text">
                        {isBook && isLastSpread ? "Finish" : isLastPage ? "Finish" : "Next"}
                    </span>
                    <span className="nav-icon">{(isBook && isLastSpread) || isLastPage ? "🌟" : "→"}</span>
                </button>
            </div>

            {/* Completion Screen */}
            {showCompletion && (
                <div className="completion-overlay visible">
                    <div className="completion-content">
                        <div className="completion-icon">🌟</div>
                        <h2>The End!</h2>
                        <p>What a magical adventure! Did you enjoy the story?</p>
                        <div className="completion-actions">
                            <button onClick={readAgain} className="read-again-btn"><span className="button-icon">🔄</span>Read Again</button>
                            <button onClick={() => navigate("/create")} className="create-another-btn"><span className="button-icon">✨</span>Create Another Story</button>
                            <button onClick={() => navigate("/profile")} className="back-to-profile-btn"><span className="button-icon">👤</span>Back to Profile</button>
                            {!feedbackSent ? (
                                <button onClick={() => setShowFeedback(true)} className="give-feedback-btn">
                                    <span className="button-icon">💬</span>Give Feedback
                                </button>
                            ) : (
                                <div className="give-feedback-sent" aria-live="polite" title="Feedback sent">✅ Feedback sent — thank you!</div>
                            )}
                        </div>
                    </div>
                </div>
            )}

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
                emailTargets={["support@starlitstories.app"]}
                onSubmitted={() => {
                    setFeedbackSent(true);
                    if (story?.id) localStorage.setItem(`fb:${story.id}`, "1");
                }}
            />
        </div>
    );
}