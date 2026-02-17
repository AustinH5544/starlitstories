import { useEffect, useRef, useState } from "react";
import { useAuth } from "../context/AuthContext";
import StoryForm from "../components/StoryForm";
import api from "../api";
import "./CreatePage.css";
import { useNavigate } from "react-router-dom";
import useUserProfile from "../hooks/useUserProfile";
import useWarmup from "../hooks/useWarmup";
import DoodlePad from "../Components/DoodlePad";

const CreatePage = () => {
    useWarmup();
    const { user } = useAuth();
    const [story, setStory] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [storyReady, setStoryReady] = useState(false);
    const [error, setError] = useState(null);
    const [progress, setProgress] = useState(0);
    const [progressPhase, setProgressPhase] = useState("idle"); // "idle" | "upload" | "generating" | "download" | "done"
    const [progressHint, setProgressHint] = useState("");
    const [lastFormData, setLastFormData] = useState(null);

    const { profile: userProfile, loading: profileLoading, error: profileError } = useUserProfile();
    const navigate = useNavigate();

    const inFlightRef = useRef(false);
    const startedJobRef = useRef(null);
    const finalizedRef = useRef(false);

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
    };

    /**
     * Try the SSE flow first; fall back to single-call if needed.
     */
    const generateStory = async (request) => {
        if (inFlightRef.current) return;
        inFlightRef.current = true;

        startedJobRef.current = null;
        finalizedRef.current = false;

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
            setProgressPhase("generating");
            setProgressHint("Creating your magical pages…");
            setProgress((p) => Math.max(p, 5));

            safeClose(esRef.current);
            esRef.current = null;

            const url = buildApiUrl(`/story/progress/${encodeURIComponent(jobId)}`);
            const es = new EventSource(url);
            esRef.current = es;

            const stageToPercent = (stage, index, total) => {
                if (!stage) return null;
                const s = String(stage).toLowerCase();
                if (s.includes("text") || s.includes("chat")) return 10 + Math.min(30, (index ?? 1) * 10);
                if (s.includes("image")) {
                    if (!total || total <= 0) return 60;
                    const done = Math.max(0, Math.min(total, index ?? 0));
                    const frac = done / total;
                    return Math.round(30 + frac * 65); // 30 -> 95
                }
                if (s.includes("db") || s.includes("save") || s.includes("persist")) return 97;
                if (s.includes("finish") || s.includes("done") || s.includes("complete")) return 100;
                return null;
            };

            es.onmessage = async (evt) => {
                try {
                    const data = JSON.parse(evt.data || "{}");
                    if (data.message) setProgressHint(data.message);

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
                setProgress(100);
                setProgressHint("Done!");
                return;
            }
        }

        setProgressPhase("done");
        setProgress(100);
        setProgressHint("Done!");
        setStory(result);
        setStoryReady(true);
    };

    // ---- Single-call fallback ----
    const runSingleCallFallback = async (payload) => {
        setProgressPhase("upload");
        setProgressHint("Uploading details…");

        const res = await api.post("/story/generate-full", payload, {
            onUploadProgress: (evt) => {
                if (!evt.total) return;
                const pct = Math.min(30, Math.round((evt.loaded / evt.total) * 30)); // 0–30%
                setProgress(pct);
            },
            onDownloadProgress: (evt) => {
                setProgressPhase("download");
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
                setProgressPhase("generating");
                setProgressHint("Creating your magical pages…");
                return 70;
            }
            return p;
        });

        setProgressPhase("done");
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

    const isFreeUserAtLimit = userProfile && user?.membership === "free" && userProfile.booksGenerated >= 1;

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
                ) : isLoading ? (
                    <div className="loading-container">
                        {/* Progress bar */}
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
                        <p className="loading-text">
                            <span className="loading-icon">✨</span>
                            {progressHint || "Creating your magical story…"}
                        </p>
                        <p className="loading-subtext">
                            This may take a few moments while our storytellers work their magic
                        </p>
                        <div className="wait-activity" style={{ marginTop: "1rem" }}>
                            <DoodlePad height={280} lineWidth={5} strokeStyle="#1f2937" background="transparent" />
                        </div>
                    </div>
                ) : !storyReady || !isValidStory ? (
                    <div className="create-form-wrapper">
                        <StoryForm onSubmit={generateStory} />
                        {error && (
                            <div className="error-container">
                                <div className="error-icon">😔</div>
                                <p className="create-error">{error}</p>
                                <p className="error-subtext">Please try again or contact support if the problem persists</p>
                                <button
                                    onClick={() => lastFormData && generateStory(lastFormData)}
                                    disabled={!lastFormData}
                                    className="retry-btn"
                                    aria-disabled={!lastFormData}
                                    title={!lastFormData ? "Submit the form once before retrying" : undefined}
                                >
                                    🔄 Retry
                                </button>
                            </div>
                        )}
                    </div>
                ) : (
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
    );
};

export default CreatePage;