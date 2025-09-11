"use client"

import { useLocation } from "react-router-dom";
import { useEffect, useState } from "react"
import api from "../api"
import "./ProfilePage.css"
import { useAuth } from "../context/AuthContext"
import { useNavigate } from "react-router-dom"
import StoryCard from "../components/StoryCard"
import { downloadStoryPdf } from "../utils/downloadStoryPdf";
import { publicBase } from "../utils/urls";

const ProfilePage = () => {
    const { user, setUser } = useAuth();
    const [stories, setStories] = useState([])
    const [loading, setLoading] = useState(true)
    const navigate = useNavigate()
    const [avatarVersion, setAvatarVersion] = useState(0);
    const [working, setWorking] = useState(false);
    const [actionMsg, setActionMsg] = useState("");
    const [showCancelModal, setShowCancelModal] = useState(false);
    const { search } = useLocation();
    const [flash, setFlash] = useState("");
    const [billing, setBilling] = useState(null);

    const BASE = import.meta.env.BASE_URL
    const [showImageModal, setShowImageModal] = useState(false)

    const openBillingPortal = async () => {
        try {
            setWorking(true);
            setActionMsg("");

            // If your API needs POST, swap to api.post(...).
            const cfg = { skipAuth401Handler: true };
            const { data } = await api.get("payments/billing/portal", cfg);

            // Be flexible about the returned key name
            const url = data?.url || data?.portalUrl || data?.sessionUrl;
            if (url) {
                window.location.href = url; // or: window.location.assign(url)
                return;
            }

            throw new Error("No portal URL returned");
        } catch (e) {
            console.error("openBillingPortal error:", e);
            const status = e?.response?.status;
            if (status === 401) {
                setActionMsg("Please sign in again, then try opening the billing portal.");
            } else if (status === 403) {
                setActionMsg("Your account can’t access the billing portal yet.");
            } else {
                setActionMsg("Could not open the billing portal. Please try again.");
            }
        } finally {
            setWorking(false);
        }
    };

    // Converts ISO string, ms, or *seconds* into a Date
    const toDate = (v) => {
        if (!v) return null;
        if (v instanceof Date) return v;
        if (typeof v === "number") {
            // treat < 1e12 as seconds, otherwise ms
            return new Date(v < 1e12 ? v * 1000 : v);
        }
        // numeric string?
        if (typeof v === "string" && /^\d+$/.test(v)) {
            const n = Number(v);
            return new Date(n < 1e12 ? n * 1000 : n);
        }
        // ISO or other date-like string
        const d = new Date(v);
        return isNaN(d.getTime()) ? null : d;
    };

    // Picks the best renewal/cancel dates regardless of field style
    const coerceBilling = (raw) => {
        if (!raw || typeof raw !== "object") return null;

        // accept nested objects commonly returned by backends
        const src = raw.subscription ?? raw.data ?? raw;

        // small helper
        const first = (...keys) => {
            for (const k of keys) if (src?.[k] != null) return src[k];
            return null;
        };

        const current = first(
            "currentPeriodEnd", "current_period_end", "period_end",
            "nextRenewalAt", "next_renewal_at",
            "expiresAt", "expires_at", "validUntil", "valid_until"
        );

        const cancel = first(
            "cancelAt", "cancel_at",
            "accessUntil", "access_until",
            "endsAt", "ends_at"
        );

        return {
            ...src,
            currentPeriodEnd: toDate(current)?.toISOString() ?? null,
            cancelAt: toDate(cancel)?.toISOString() ?? null,
        };
    };

    // pretty format for display
    const formatDate = (iso) => {
        if (!iso) return "";
        const d = new Date(iso);
        return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
    };

    useEffect(() => {
        let alive = true;
        (async () => {
            const tier = String(user?.membership || "").toLowerCase();
            if (!user?.email || tier === "free") { if (alive) setBilling(null); return; }
            try {
                const { data } = await api.get("payments/subscription");
                if (alive) setBilling(coerceBilling(data?.subscription ?? data));
            } catch (e) {
                console.warn("Could not load subscription", e);
                if (alive) setBilling(null);
            }
        })();
        return () => { alive = false; };
    }, [user?.email, user?.membership]);

    const isPaid = (user?.membership ?? "free").toLowerCase() !== "free";
    const renewalDate = billing?.cancelAt || billing?.currentPeriodEnd;
    const renewalLabel = billing?.cancelAt ? "Access until" : "Next renewal";

    useEffect(() => {
        const q = new URLSearchParams(search);
        if (q.get("upgraded") === "1") {
            const plan = q.get("plan");
            setFlash(`You're all set! Your ${plan} plan is active.`);
            // optional: clean the url
            window.history.replaceState({}, "", "/profile");
        }
    }, [search]);

    const cancelMembership = async () => {
        try {
            setWorking(true); setActionMsg("");
            await api.post("payments/cancel"); // POST /api/payments/cancel
            // reflect locally; webhook is source of truth later
            setUser(u => (u ? { ...u, membership: "free" } : u));
            setShowCancelModal(false);
            setActionMsg("Cancellation scheduled. You’ll keep access until the current period ends.");
        } catch (e) {
            console.error(e);
            setActionMsg("We couldn't cancel your membership. Please try again.");
        } finally {
            setWorking(false);
        }
    };

    // ONE source of truth for the avatar (use public/avatars/default-avatar.png)
    const [selectedImage, setSelectedImage] = useState(
        user?.profileImage
            ? `${BASE}avatars/${user.profileImage}`
            : `${BASE}avatars/default-avatar.png`
    );

    const [imgError, setImgError] = useState(false)

    const profileImages = [
        "wizard-avatar.png",
        "princess-avatar.png",
        "knight-avatar.png",
        "whimsical-fairy-avatar.png",
        "dragon-avatar.png",
        "unicorn-avatar.png",
        "pirate-avatar.png",
        "astronaut-avatar.png",
        "whimsical-mermaid-avatar.png",
        "superhero-avatar.png",
        "cat-avatar.png",
    ]

    const handleImageSelect = async (fileName) => {
        setShowImageModal(false);
        try {
            await api.put("profile/avatar", { profileImage: fileName });
            setUser(u => (u ? { ...u, profileImage: fileName } : u));
            setAvatarVersion(v => v + 1); // <- bust cache
        } catch (e) {
            console.error("Failed to save avatar", e);
        }
    };

    useEffect(() => {
        const file = user?.profileImage || "default-avatar.png";
        const baseSrc = file.startsWith("http") ? file : `${BASE}avatars/${file}`;
        setSelectedImage(`${baseSrc}?v=${avatarVersion}`);
        setImgError(false);
    }, [user?.profileImage, BASE, avatarVersion]);

    useEffect(() => {
        const fetchStories = async () => {
            try {
                const res = await api.get("profile/me/stories")
                setStories(res.data)
            } catch (err) {
                console.error("Error loading stories:", err)
            } finally {
                setLoading(false)
            }
        }
        if (user?.email) fetchStories()
    }, [user])

    // ---- Handlers for StoryCard ----
    const canDownload = user?.membership === "pro" || user?.membership === "premium"

    const onOpen = (story) => {
        // keep your viewer navigation using state
        navigate("/view", { state: { story } })
    }

    const onShare = async (story) => {
        try {
            // use your authenticated api client, not bare fetch
            const { data } = await api.post(`stories/${story.id}/share`);
            const { token } = data;
            if (!token) throw new Error("No token returned");

            const url = new URL(`/s/${token}`, publicBase()).toString();

            if (navigator.share) {
                try { await navigator.share({ title: story.title, url }); return; } catch { }
            }
            await navigator.clipboard.writeText(url);
            alert("Share link copied!");
        } catch (e) {
            const status = e?.response?.status;
            alert(`Could not create share link${status ? ` (${status})` : ""}.`);
        }
    };

    const onDownload = async (story, format) => {
        if (format === "pdf") {
            await downloadStoryPdf(story)
        } else {
            await downloadAsImages(story)
        }
    }

    const onDelete = async (storyId) => {
        const prev = stories;
        setStories(prev.filter(s => s.id !== storyId)); // optimistic
        try {
            await api.delete(`story/${storyId}`); // /api/story/{id}
        } catch (err) {
            setStories(prev); // rollback
            alert("Could not delete the story. Please try again.");
        }
    };

    // ---- Download helpers (reusing your StoryViewer logic, but scoped to a passed story) ----
    const loadImageAsBase64 = (url) => {
        return new Promise((resolve, reject) => {
            const img = new Image()
            img.crossOrigin = "anonymous"
            img.onload = () => {
                const canvas = document.createElement("canvas")
                canvas.width = img.width
                canvas.height = img.height
                const ctx = canvas.getContext("2d")
                ctx.drawImage(img, 0, 0)
                resolve(canvas.toDataURL("image/png"))
            }
            img.onerror = reject
            img.src = url
        })
    }

    const downloadAsPDF = async (story) => {
        const { jsPDF } = await import("jspdf")
        const pdf = new jsPDF()

        // Title page
        pdf.setFontSize(24)
        pdf.text(story.title || "Untitled", 20, 30)
        pdf.setFontSize(12)
        if (story.createdAt) {
            pdf.text(`Created on ${new Date(story.createdAt).toLocaleDateString()}`, 20, 50)
        }

        // Cover
        if (story.coverImageUrl && !story.coverImageUrl.includes("placeholder")) {
            try {
                const coverImg = await loadImageAsBase64(story.coverImageUrl)
                pdf.addImage(coverImg, "PNG", 20, 70, 170, 120)
            } catch {
                console.warn("Could not load cover image for PDF")
            }
        }

        // Pages
        for (let i = 0; i < (story.pages?.length ?? 0); i++) {
            const page = story.pages[i]
            pdf.addPage()

            if (page.imageUrl && !page.imageUrl.includes("placeholder")) {
                try {
                    const pageImg = await loadImageAsBase64(page.imageUrl)
                    pdf.addImage(pageImg, "PNG", 20, 20, 170, 120)
                } catch {
                    console.warn(`Could not load image for page ${i + 1}`)
                }
            }

            pdf.setFontSize(12)
            const splitText = pdf.splitTextToSize(page.text || "", 170)
            pdf.text(splitText, 20, 160)

            pdf.setFontSize(10)
            pdf.text(`Page ${i + 1}`, 180, 280)
        }

        pdf.save(`${story.title || "story"}.pdf`)
    }

    const downloadAsImages = async (story) => {
        const zip = await import("jszip")
        const JSZip = zip.default
        const zipFile = new JSZip()

        if (story.coverImageUrl && !story.coverImageUrl.includes("placeholder")) {
            try {
                const coverBlob = await fetch(story.coverImageUrl).then(r => r.blob())
                zipFile.file("00-cover.png", coverBlob)
            } catch {
                console.warn("Could not download cover image")
            }
        }

        for (let i = 0; i < (story.pages?.length ?? 0); i++) {
            const page = story.pages[i]
            if (page.imageUrl && !page.imageUrl.includes("placeholder")) {
                try {
                    const pageBlob = await fetch(page.imageUrl).then(r => r.blob())
                    zipFile.file(`${String(i + 1).padStart(2, "0")}-page-${i + 1}.png`, pageBlob)
                } catch {
                    console.warn(`Could not download image for page ${i + 1}`)
                }
            }
        }

        const storyText = `${story.title || "Untitled"}\n\n${(story.pages || [])
            .map((p, i) => `Page ${i + 1}:\n${p.text || ""}`)
            .join("\n\n")}`
        zipFile.file("story-text.txt", storyText)

        const content = await zipFile.generateAsync({ type: "blob" })
        const url = window.URL.createObjectURL(content)
        const a = document.createElement("a")
        a.href = url
        a.download = `${story.title || "story"}-images.zip`
        a.click()
        window.URL.revokeObjectURL(url)
    }

    if (!user) {
        return (
            <div className="profile-page">
                {flash && <p className="action-msg">{flash}</p>}
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
                                    src={selectedImage || `${BASE}avatars/default-avatar.png`}
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

                    {/* NEW: Next renewal / Access until (paid users only) */}
                    {isPaid && (
                        <div className="detail-card">
                            <div className="detail-icon">🗓️</div>
                            <div className="detail-content">
                                <span className="detail-label">{renewalLabel}</span>
                                <span className="detail-value">{renewalDate ? formatDate(renewalDate) : "—"}</span>
                            </div>
                        </div>
                    )}

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

                    {String(user.membership || "").toLowerCase() === "free" ? (
                        <button onClick={() => navigate("/upgrade")} className="upgrade-plan-btn">
                            <span className="button-icon">🚀</span>
                            <span>Upgrade Plan</span>
                        </button>
                    ) : (
                        <div className="membership-actions">
                            <button className="manage-plan-btn" disabled={working} onClick={openBillingPortal}>
                                <span className="button-icon">🛠️</span>
                                <span>{working ? "Opening..." : "Change Plan"}</span>
                            </button>
                            <button className="cancel-plan-btn" disabled={working} onClick={() => setShowCancelModal(true)}>
                                <span className="button-icon">🗑️</span>
                                <span>Cancel Membership</span>
                            </button>
                        </div>
                    )}
                </div>

                {actionMsg && <p className="action-msg">{actionMsg}</p>}

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
                                <StoryCard
                                    key={story.id}
                                    story={story}
                                    canCustomize={true}
                                    canDownload={canDownload}
                                    onShare={onShare}
                                    onDownload={onDownload}
                                    onDelete={onDelete}
                                    onOpen={onOpen}
                                    onCustomize={(s) => navigate("/customize", { state: { story: s } })}
                                />
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
                                        className={`image-option ${selectedImage.endsWith('/' + imageUrl) ? 'selected' : ''}`}
                                        onClick={() => handleImageSelect(imageUrl)}
                                    >
                                        <img src={`${BASE}avatars/${imageUrl}`} alt={`Avatar option ${i + 1}`} />
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                )}

                {showCancelModal && (
                    <div className="image-modal-overlay" onClick={() => !working && setShowCancelModal(false)}>
                        <div className="image-modal" onClick={(e) => e.stopPropagation()}>
                            <div className="modal-header">
                                <h3>Cancel Membership?</h3>
                                <button className="close-btn" onClick={() => !working && setShowCancelModal(false)}>✕</button>
                            </div>
                            <p className="cancel-blurb">
                                This stops auto-renewal. You’ll keep access until the current period ends, then move to the Free plan.
                            </p>
                            <div className="cancel-actions">
                                <button className="manage-plan-btn" disabled={working} onClick={() => setShowCancelModal(false)}>
                                    Keep my plan
                                </button>
                                <button className="cancel-plan-btn" disabled={working} onClick={cancelMembership}>
                                    {working ? "Cancelling..." : "Confirm Cancel"}
                                </button>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    )
}

export default ProfilePage