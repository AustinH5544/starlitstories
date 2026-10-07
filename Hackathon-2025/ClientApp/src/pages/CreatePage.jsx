import { useEffect, useRef, useState } from "react";
import { Helmet } from "react-helmet-async";
import { useAuth } from "../context/AuthContext";
import StoryForm from "../components/StoryForm";
import DoodlePad from "../components/DoodlePad";
import PaintingFlightGame from "../components/PaintingFlightGame";
import api from "../api";
import "./CreatePage.css";
import { useNavigate } from "react-router-dom";
import useUserProfile from "../hooks/useUserProfile";
import useWarmup from "../hooks/useWarmup";
import posthog from '../analytics';

const CreatePage = () => {
    useWarmup();
    const { user } = useAuth();
    const isWaitDebug =
        import.meta.env.DEV &&
        new URLSearchParams(window.location.search).get("waitDebug") === "1";
    const [story, setStory] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [storyReady, setStoryReady] = useState(false);
    const [error, setError] = useState(null);
    const [progress, setProgress] = useState(0);
    const [progressPhase, setProgressPhase] = useState("idle"); // "idle" | "upload" | "planning" | "illustrating" | "saving" | "done"
    const [progressHint, setProgressHint] = useState("");
    const [progressStage, setProgressStage] = useState("idle");
    const [lastFormData, setLastFormData] = useState(null);
    const [waitActivity, setWaitActivity] = useState("game");

    const { profile: userProfile, loading: profileLoading, error: profileError } = useUserProfile();
    const navigate = useNavigate();

    const inFlightRef = useRef(false);
    const startedJobRef = useRef(null);
    const finalizedRef = useRef(false);

    const generationStartRef = useRef(null);
    const storyThemeRef = useRef(null);
    const storyCharNameRef = useRef(null);

    // Keep a ref to the current SSE connection to close it on unmount / finish
    const esRef = useRef(null);

    const safeClose = (es) => {
        if (!es) return;
        try {
            es.close();
        } catch (e) {
            // use e to satisfy lint and aid debugging
            console.debug("EventSource close ignored:", e);
        }
    };

    useEffect(() => {
        return () => {
            safeClose(esRef.current);
            esRef.current = null;
        };
    }, []);

    useEffect(() => {
        posthog.capture('character_customization_started')
    }, []);

    // Prefer the axios baseURL (same host your other API calls use)
    const buildApiUrl = (path) => {
        const base = (api && api.defaults && api.defaults.baseURL) || "";
        if (!base) return path; // fall back to relative if no base
        return `${base.replace(/\/+$/, "")}/${path.replace(/^\/+/, "")}`;
    };

    // Simple polling if SSE drops after a job has started
    const pollResult = async (jobId, timeoutMs = 60000, intervalMs = 1000) => {
        const end = Date.now() + timeoutMs;
        while (Date.now() < end) {
            try {
                const resp = await api.get(`/story/result/${encodeURIComponent(jobId)}`, { timeout: 5000 });
                if (resp?.data) return resp.data;
            } catch (e) {
                // keep polling; log for dev visibility
                console.debug("pollResult retry after error:", e);
            }
            await new Promise((resolve) => setTimeout(resolve, intervalMs));
        }
        return null;
    };

    const resetProgress = () => {
        setProgress(0);
        setProgressPhase("idle");
        setProgressHint("");
        setProgressStage("idle");
    };

    const getProgressMeta = (stage) => {
        const normalized = String(stage || "").toLowerCase();

        if (!normalized || normalized === "idle") return { phase: "idle", step: 0 };
        if (normalized.includes("upload")) return { phase: "saving", step: 4 };
        if (normalized.includes("db") || normalized.includes("title")) return { phase: "saving", step: 4 };
        if (normalized.includes("done")) return { phase: "done", step: 5 };
        if (normalized.includes("page-images") || normalized.includes("character-base") || normalized.includes("image")) {
            return { phase: "illustrating", step: 3 };
        }
        if (normalized.includes("scene") || normalized.includes("cover")) return { phase: "planning", step: 2 };
        if (normalized.includes("story") || normalized.includes("text") || normalized.includes("start")) {
            return { phase: "planning", step: 1 };
        }

        return { phase: "planning", step: 1 };
    };

    const progressSteps = [
        { key: "story", label: "Story" },
        { key: "scenes", label: "Scenes" },
        { key: "artwork", label: "Artwork" },
        { key: "saving", label: "Saving" },
    ];

    /**
     * Try the SSE flow first; fall back to single-call if needed.
     */
    const generateStory = async (request) => {
        if (inFlightRef.current) return;
        inFlightRef.current = true;

        startedJobRef.current = null;
        finalizedRef.current = false;

        const mainChar = request.characters?.find(c => c.role === 'main') ?? request.characters?.[0]
        posthog.capture('character_customization_completed', {
            character_name: mainChar?.name,
            character_gender: mainChar?.descriptionFields?.gender,
            story_theme: request.theme,
        })
        posthog.capture('story_generation_started', {
            story_theme: request.theme,
            character_name: mainChar?.name,
        })
        generationStartRef.current = Date.now()
        storyThemeRef.current = request.theme
        storyCharNameRef.current = mainChar?.name

        setLastFormData(request);
        setIsLoading(true);
        setStoryReady(false);
        setError(null);
        setStory(null);
        resetProgress();

        const payload = { ...request };

        try {
            // ---- Attempt SSE job flow ----
            try {
                const startRes = await api.post("/story/generate-full/start", payload, { timeout: 20000 });
                const jobId = startRes?.data?.jobId;

                if (jobId) {
                    startedJobRef.current = jobId;

                    try {
                        await runSSE(jobId);
                        return;
                    } catch (sseErr) {
                        // If the backend already reported a failure, don't poll — the error is already set.
                        if (finalizedRef.current) return;

                        console.warn("SSE dropped after job started; polling result:", sseErr);
                        const result = await pollResult(jobId, 120000, 1000);
                        if (result) {
                            setProgressPhase("done");
                            setProgress(100);
                            setProgressHint("Done!");
                            setStory(result);
                            setStoryReady(true);
                            return;
                        }
                        setError("We lost the live connection while generating your story. Your story should be in your Profile.");
                        return;
                    }
                }

                await runSingleCallFallback(payload);
            } catch (startErr) {
                console.warn("SSE start failed; using single-call fallback:", startErr);
                await runSingleCallFallback(payload);
            }
        } catch (fallbackErr) {
            console.error("Fallback API Error:", fallbackErr);
            const message = extractErrorMessage(fallbackErr);
            setError(message);
        } finally {
            inFlightRef.current = false;
            setIsLoading(false);
        }
    };

    // ---- SSE runner ----
    const runSSE = (jobId) =>
        new Promise((resolve, reject) => {
            setProgressPhase("planning");
            setProgressStage("start");
            setProgressHint("Starting your story...");
            setProgress((p) => Math.max(p, 5));

            safeClose(esRef.current);
            esRef.current = null;

            const url = buildApiUrl(`/story/progress/${encodeURIComponent(jobId)}`);
            const es = new EventSource(url);
            esRef.current = es;

            const stageToPercent = (stage, index, total) => {
                if (!stage) return null;
                const s = String(stage).toLowerCase();
                if (s.includes("story") || s.includes("text")) return 15;
                if (s.includes("scene")) return 28;
                if (s.includes("cover")) return 38;
                if (s.includes("character-base")) return 48;
                if (s.includes("page-images")) {
                    if (!total || total <= 0) return 58;
                    const done = Math.max(0, Math.min(total, index ?? 0));
                    const frac = done / total;
                    return Math.round(58 + frac * 24); // 58 -> 82
                }
                if (s.includes("title")) return 84;
                if (s.includes("upload")) {
                    if (!total || total <= 0) return 88;
                    const done = Math.max(0, Math.min(total, index ?? 0));
                    const frac = done / total;
                    return Math.round(88 + frac * 8); // 88 -> 96
                }
                if (s.includes("db") || s.includes("save") || s.includes("persist")) return 98;
                if (s.includes("finish") || s.includes("done") || s.includes("complete")) return 100;
                return null;
            };

            es.onmessage = async (evt) => {
                try {
                    const data = JSON.parse(evt.data || "{}");
                    if (data.message) setProgressHint(data.message);
                    if (data.stage) {
                        setProgressStage(data.stage);
                        setProgressPhase(getProgressMeta(data.stage).phase);
                    }

                    // Backend sent an explicit error event — surface the real message.
                    if (data.stage === "error") {
                        if (finalizedRef.current) return;
                        finalizedRef.current = true;
                        safeClose(es);
                        esRef.current = null;
                        setError(data.message || "Story generation failed. Please try again.");
                        reject(new Error(data.message || "backend error"));
                        return;
                    }

                    const markDoneOnce = async () => {
                        if (finalizedRef.current) return;
                        finalizedRef.current = true;
                        await finalizeSSE(data, jobId);
                        safeClose(es);
                        esRef.current = null;
                        resolve();
                    };

                    if (typeof data.percent === "number") {
                        const safe = Math.max(0, Math.min(100, data.percent));
                        setProgress((p) => Math.max(p, safe));
                        if (safe >= 100 || data.done) await markDoneOnce();
                        return;
                    }

                    const est = stageToPercent(data.stage, data.index, data.total);
                    if (est != null) {
                        setProgress((p) => Math.max(p, Math.min(99, est)));
                        if (est >= 100 || data.done) await markDoneOnce();
                    }
                } catch (e) {
                    console.warn("SSE parse error:", e);
                }
            };

            es.onerror = (e) => {
                if (finalizedRef.current) {
                    safeClose(es);
                    esRef.current = null;
                    return;
                }
                console.error("SSE error:", e);
                safeClose(es);
                esRef.current = null;
                reject(new Error("SSE connection error"));
            };
        });

    // Retry result fetch a few times before giving up
    const finalizeSSE = async (data, jobId) => {
        safeClose(esRef.current);
        esRef.current = null;

        let result = data?.story;

        const tryFetchResult = async () => {
            const maxTries = 6; // ~3s total
            const delayMs = 500;
            for (let i = 0; i < maxTries; i++) {
                try {
                    const r = await api.get(`/story/result/${encodeURIComponent(jobId)}`, { timeout: 5000 });
                    if (r?.data) return r.data;
                } catch (e) {
                    console.debug("result fetch retry after error:", e);
                }
                await new Promise((res) => setTimeout(res, delayMs));
            }
            return null;
        };

        if (!result) {
            result = await tryFetchResult();
            if (!result) {
                setError("We lost the live connection. Your story should be in your Profile.");
                setProgressPhase("done");
                setProgressStage("done");
                setProgress(100);
                setProgressHint("Done!");
                return;
            }
        }

        setProgressPhase("done");
        setProgressStage("done");
        setProgress(100);
        setProgressHint("Done!");
        setStory(result);
        setStoryReady(true);
    };

    // ---- Single-call fallback ----
    const runSingleCallFallback = async (payload) => {
        setProgressPhase("upload");
        setProgressStage("upload");
        setProgressHint("Uploading details…");

        const res = await api.post("/story/generate-full", payload, {
            onUploadProgress: (evt) => {
                if (!evt.total) return;
                const pct = Math.min(30, Math.round((evt.loaded / evt.total) * 30)); // 0–30%
                setProgress(pct);
            },
            onDownloadProgress: (evt) => {
                setProgressPhase("saving");
                setProgressStage("saving");
                setProgressHint("Downloading your story…");
                if (evt.total) {
                    const downloadedPortion = Math.round((evt.loaded / evt.total) * 30); // 70–100%
                    setProgress((prev) => Math.max(prev, 70 + downloadedPortion));
                } else {
                    setProgress((prev) => Math.min(95, prev + 1));
                }
            },
            timeout: 180000,
        });

        setProgress((p) => {
            if (p < 70) {
                setProgressPhase("planning");
                setProgressStage("story");
                setProgressHint("Creating your magical pages…");
                return 70;
            }
            return p;
        });

        setProgressPhase("done");
        setProgressStage("done");
        setProgress(100);
        setProgressHint("Done!");
        setStory(res.data);
        setStoryReady(true);
    };

    const extractErrorMessage = (err) => {
        const data = err?.response?.data;

        if (!data) {
            return "Oops! Something went wrong generating your story.";
        }

        if (typeof data === "string") {
            return data;
        }

        if (typeof data === "object" && typeof data.message === "string") {
            return data.message;
        }

        try {
            return JSON.stringify(data);
        } catch {
            return "Oops! Something went wrong generating your story.";
        }
    };

    const isValidStory =
        story &&
        Array.isArray(story.pages) &&
        story.pages.length > 0 &&
        story.pages[0].text?.toLowerCase().startsWith("oops") === false &&
        !error;

    const activeProgressStep = getProgressMeta(progressStage).step;

    useEffect(() => {
        if (!storyReady || !isValidStory) return;
        const elapsed = Math.round((Date.now() - (generationStartRef.current ?? Date.now())) / 1000)
        posthog.capture('story_generation_completed', {
            story_theme: storyThemeRef.current,
            character_name: storyCharNameRef.current,
            page_count: story.pages?.length ?? 0,
            generation_time_seconds: elapsed,
        })
        posthog.capture('story_saved_to_library', {
            story_id: story.id,
        })
    }, [story, storyReady, isValidStory]);

    const isFreeUserAtLimit = userProfile && user?.membership === "free" && userProfile.booksGenerated >= 1;

    useEffect(() => {
        if (isFreeUserAtLimit) {
            posthog.capture('paywall_encountered', {
                trigger: 'story_limit_reached',
                stories_created: userProfile?.booksGenerated,
            })
        }
    }, [isFreeUserAtLimit]);

    // Not logged in
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
                            You need to be signed in to create magical stories for your little ones.
                        </p>

                        <div className="auth-actions">
                            <button onClick={() => navigate("/signup")} className="signup-cta-button">
                                <span className="button-icon">🚀</span><span>Create Free Account</span>
                            </button>
                            <button onClick={() => navigate("/login")} className="login-cta-button">
                                <span className="button-icon">🔮</span><span>Sign In</span>
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // Error loading profile
    if (profileError) {
        return (
            <div className="create-page">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="create-container">
                    <div className="error-container">
                        <div className="error-icon">😞</div>
                        <p className="create-error">We couldn’t load your profile.</p>
                        <p className="error-subtext">Please refresh the page or try signing out and in again.</p>
                    </div>
                </div>
            </div>
        );
    }

    // Still loading profile
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
        );
    }

    return (
        <div className="create-page">
            <Helmet>
                <title>Create a Story | Starlit Stories</title>
                <meta name="robots" content="noindex, nofollow" />
            </Helmet>
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
                            You've created your free story! To keep going, upgrade to one of our premium plans.
                        </p>

                        <div className="upgrade-actions">
                            <button onClick={() => navigate("/upgrade")} className="upgrade-button">
                                <span className="button-icon">⭐</span><span>Upgrade Your Plan</span>
                            </button>
                            <button onClick={() => navigate("/profile")} className="back-to-profile-btn">
                                <span className="button-icon">👤</span><span>Back to Profile</span>
                            </button>
                        </div>
                    </div>
                ) : (isLoading || isWaitDebug || (storyReady && isValidStory)) ? (
                    <div className="loading-container">
                        <h2 className="wait-title">
                            {storyReady && isValidStory ? "Your story is ready!" : "Your story is being created..."}
                        </h2>
                        <p className="wait-subtitle">
                            {storyReady && isValidStory
                                ? "Keep playing or drawing as long as you want, then open your story when you're ready."
                                : "Pick an activity while we finish your magical book."}
                        </p>

                        <div className="wait-activity-switch">
                            <button
                                type="button"
                                className={`wait-activity-btn ${waitActivity === "game" ? "active" : ""}`}
                                onClick={() => setWaitActivity("game")}
                            >
                                Play Painting Flight
                            </button>
                            <button
                                type="button"
                                className={`wait-activity-btn ${waitActivity === "doodle" ? "active" : ""}`}
                                onClick={() => setWaitActivity("doodle")}
                            >
                                Open Doodle Pad
                            </button>
                        </div>

                        <div className="progress-wrap" aria-live="polite">
                            <div
                                className={`progress-bar ${progressPhase}`}
                                role="progressbar"
                                aria-valuemin={0}
                                aria-valuemax={100}
                                aria-valuenow={progress}
                                style={{ width: `${progress}%` }}
                            />
                        </div>
                        <div className="progress-steps" aria-hidden="true">
                            {progressSteps.map((step, index) => {
                                const stepNumber = index + 1;
                                const state =
                                    activeProgressStep >= stepNumber
                                        ? activeProgressStep === stepNumber && progressPhase !== "done"
                                            ? "active"
                                            : "done"
                                        : "";

                                return (
                                    <span key={step.key} className={`progress-step ${state}`.trim()}>
                                        {step.label}
                                    </span>
                                );
                            })}
                        </div>
                        <div className="story-status-slot" aria-live="polite">
                            {!storyReady || !isValidStory ? (
                                <>
                                    <p className="loading-text">
                                        <span className="loading-icon" aria-hidden="true">*</span>
                                        {progressHint || "Creating your magical story..."}
                                    </p>
                                    <p className="loading-subtext">
                                        This may take a few moments while our storytellers work their magic
                                    </p>
                                </>
                            ) : (
                                <div className="story-ready-banner">
                                    <div className="story-ready-copy">
                                        <p className="story-ready-title">Your book is finished and saved to your library.</p>
                                        <p className="story-ready-text">Open it now, or stay here and keep having fun first.</p>
                                    </div>
                                    <button className="view-story-button" onClick={() => navigate("/view", { state: { story } })}>
                                        <span>View Your Story</span>
                                    </button>
                                </div>
                            )}
                        </div>
                        <div className="wait-activity-shell">
                            <div className="wait-activity-panel">
                                {waitActivity === "game" ? (
                                    <PaintingFlightGame />
                                ) : (
                                    <DoodlePad height={280} lineWidth={5} strokeStyle="#1f2937" background="transparent" />
                                )}
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className="create-form-wrapper">
                        <StoryForm onSubmit={generateStory} />
                    </div>
                )}
            </div>

            {error && (
                <>
                    <div className="error-modal-backdrop" onClick={() => setError(null)} />
                    <div className="error-modal" role="dialog" aria-modal="true">
                        <button className="error-modal-close" onClick={() => setError(null)} aria-label="Close">✕</button>
                        <div className="error-icon">🌧️</div>
                        <h3 className="error-title">Oops! The magic fizzled out</h3>
                        <p className="create-error">{error}</p>
                        <div className="error-suggestions">
                            <p className="error-suggestions-title">A few things that might help:</p>
                            <ul>
                                <li>Try a different character name or description</li>
                                <li>Simplify the story theme or setting</li>
                                <li>Avoid unusual words or symbols in your inputs</li>
                                <li>Wait a moment and try again — it might just be a hiccup!</li>
                            </ul>
                        </div>
                        <button
                            onClick={() => { setError(null); lastFormData && generateStory(lastFormData); }}
                            disabled={!lastFormData}
                            className="retry-btn"
                            aria-disabled={!lastFormData}
                            title={!lastFormData ? "Submit the form once before retrying" : undefined}
                        >
                            <span>✨</span> Try Again
                        </button>
                    </div>
                </>
            )}
        </div>
    );
};

export default CreatePage;
