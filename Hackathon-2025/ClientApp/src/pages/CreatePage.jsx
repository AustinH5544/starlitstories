import { useEffect, useRef, useState } from "react"
import { useAuth } from "../context/AuthContext"
import StoryForm from "../components/StoryForm"
import api from "../api"
import "./CreatePage.css"
import { useNavigate } from "react-router-dom"
import useUserProfile from "../hooks/useUserProfile"
import useWarmup from "../hooks/useWarmup";

const CreatePage = () => {
    useWarmup();
    const { user } = useAuth()
    const [story, setStory] = useState(null)
    const [isLoading, setIsLoading] = useState(false)
    const [storyReady, setStoryReady] = useState(false)
    const [error, setError] = useState(null)
    const [progress, setProgress] = useState(0)
    const [progressPhase, setProgressPhase] = useState("idle") // "idle" | "upload" | "generating" | "download" | "done"
    const [progressHint, setProgressHint] = useState("")
    const [lastFormData, setLastFormData] = useState(null)

    const { profile: userProfile, loading: profileLoading, error: profileError } = useUserProfile()
    const navigate = useNavigate()

    // Keep a ref to the current SSE connection to close it on unmount / finish
    const esRef = useRef(null)

    useEffect(() => {
        return () => {
            if (esRef.current) {
                esRef.current.close()
                esRef.current = null
            }
        }
    }, [])

    const resetProgress = () => {
        setProgress(0)
        setProgressPhase("idle")
        setProgressHint("")
    }

    /**
     * Try the SSE flow first:
     * 1) POST /api/story/generate-full/start  -> { jobId }
     * 2) Open EventSource /api/story/progress/{jobId} -> { percent?, stage?, index?, total?, message?, done?, story? }
     * 3) On done: if story not included, GET /api/story/result/{jobId}
     *
     * If any step fails (endpoint not found, SSE blocked, etc.), fall back to the single-call flow.
     */
    const generateStory = async (formData) => {
        setLastFormData(formData);
        setIsLoading(true)
        setStoryReady(false)
        setError(null)
        setStory(null)
        resetProgress()

        const payload = { ...formData }

        try {
            // ---- Attempt SSE job flow ----
            const startRes = await api.post("/story/generate-full/start", payload, { timeout: 20000 })
            const jobId = startRes?.data?.jobId

            if (jobId) {
                await runSSE(jobId)
                return
            }

            // If no jobId returned, go to fallback
            await runSingleCallFallback(payload)
        } catch (err) {
            // If /start endpoint is missing or errors, do the fallback single-call path
            console.warn("SSE start failed; falling back to single-call:", err)
            try {
                await runSingleCallFallback(payload)
            } catch (fallbackErr) {
                console.error("Fallback API Error:", fallbackErr)
                const message = fallbackErr?.response?.data ?? "Oops! Something went wrong generating your story."
                setError(message)
            }
        } finally {
            setIsLoading(false)
        }
    }

    // ---- SSE runner ----
    const runSSE = (jobId) =>
        new Promise((resolve, reject) => {
            setProgressPhase("generating")
            setProgressHint("Creating your magical pages…")
            setProgress((p) => Math.max(p, 5)) // nudge off 0

            // Close any existing stream
            if (esRef.current) {
                esRef.current.close()
                esRef.current = null
            }

            // IMPORTANT: path should match your server route
            const url = `/api/story/progress/${encodeURIComponent(jobId)}`
            const es = new EventSource(url)
            esRef.current = es

            // Helper: map stages when server doesn't send a percent
            // Rough allocation:
            // - text generation:   0% -> 30%
            // - image generation: 30% -> 95% (based on index/total)
            // - db save:          95% -> 100%
            const stageToPercent = (stage, index, total) => {
                if (!stage) return null
                const s = String(stage).toLowerCase()
                if (s.includes("text") || s.includes("chat")) {
                    return 10 + Math.min(30, (index ?? 1) * 10) // coarse bumps during multiple text calls
                }
                if (s.includes("image")) {
                    if (!total || total <= 0) return 60
                    const done = Math.max(0, Math.min(total, (index ?? 0)))
                    const frac = done / total
                    return Math.round(30 + frac * 65) // 30 -> 95
                }
                if (s.includes("db") || s.includes("save") || s.includes("persist")) {
                    return 97
                }
                if (s.includes("finish") || s.includes("done") || s.includes("complete")) {
                    return 100
                }
                return null
            }

            es.onmessage = async (evt) => {
                try {
                    const data = JSON.parse(evt.data || "{}")

                    // If server emits a friendly message string
                    if (data.message) setProgressHint(data.message)

                    // Prefer server-provided percent if present
                    if (typeof data.percent === "number") {
                        const safe = Math.max(0, Math.min(100, data.percent))
                        setProgress((p) => Math.max(p, safe))
                        if (safe >= 100 || data.done) {
                            await finalizeSSE(data, jobId)
                            resolve()
                        }
                        return
                    }

                    // Otherwise, estimate based on stage/index/total
                    const est = stageToPercent(data.stage, data.index, data.total)
                    if (est != null) {
                        setProgress((p) => Math.max(p, Math.min(99, est)))
                        if (est >= 100 || data.done) {
                            await finalizeSSE(data, jobId)
                            resolve()
                        }
                    }
                } catch (e) {
                    console.warn("SSE parse error:", e)
                }
            }

            es.onerror = (e) => {
                console.error("SSE error:", e)
                try {
                    es.close()
                } catch { }
                esRef.current = null
                reject(new Error("SSE connection error"))
            }
        })

    const finalizeSSE = async (data, jobId) => {
        // Close stream
        if (esRef.current) {
            try {
                esRef.current.close()
            } catch { }
            esRef.current = null
        }

        // If server already sent the story, use it; otherwise fetch the result
        let result = data?.story
        if (!result) {
            try {
                const resultRes = await api.get(`/story/result/${encodeURIComponent(jobId)}`, { timeout: 60000 })
                result = resultRes?.data
            } catch (e) {
                console.error("Fetching story result failed:", e)
                setError("We finished generating, but couldn’t fetch your story. Please check your profile.")
                return
            }
        }

        setProgressPhase("done")
        setProgress(100)
        setProgressHint("Done!")
        setStory(result)
        setStoryReady(true)
    }

    // ---- Existing single-call fallback (kept, improved) ----
    const runSingleCallFallback = async (payload) => {
        setProgressPhase("upload")
        setProgressHint("Uploading details…")

        const res = await api.post("/story/generate-full", payload, {
            onUploadProgress: (evt) => {
                if (!evt.total) return
                const pct = Math.min(30, Math.round((evt.loaded / evt.total) * 30)) // 0–30%
                setProgress(pct)
            },
            onDownloadProgress: (evt) => {
                setProgressPhase("download")
                setProgressHint("Downloading your story…")
                if (evt.total) {
                    const downloadedPortion = Math.round((evt.loaded / evt.total) * 30) // 70–100%
                    setProgress((prev) => Math.max(prev, 70 + downloadedPortion))
                } else {
                    setProgress((prev) => Math.min(95, prev + 1))
                }
            },
            timeout: 180000,
        })

        // If server took a long time to compute and we never saw download progress,
        // at least show a "generating" phase so the bar isn't stuck low.
        setProgress((p) => {
            if (p < 70) {
                setProgressPhase("generating")
                setProgressHint("Creating your magical pages…")
                return 70
            }
            return p
        })

        setProgressPhase("done")
        setProgress(100)
        setProgressHint("Done!")
        setStory(res.data)
        setStoryReady(true)
    }

    const isValidStory =
        story &&
        Array.isArray(story.pages) &&
        story.pages.length > 0 &&
        story.pages[0].text?.toLowerCase().startsWith("oops") === false &&
        !error

    const isFreeUserAtLimit = userProfile && user?.membership === "free" && userProfile.booksGenerated >= 1

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
        )
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
        )
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
    )
}

export default CreatePage