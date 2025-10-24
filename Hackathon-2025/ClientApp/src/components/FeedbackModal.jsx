import React, { useEffect, useMemo, useState } from "react";
import "./FeedbackModal.css";

export default function FeedbackModal({
    open,
    onClose,
    storyMeta = { id: null, title: "", pageCount: 0, estReadMin: null },
    apiBase = "/api",
    emailTargets = ["austintylerdevelopment@gmail.com", "support@starlitstories.app"],
    onSubmitted,
}) {
    const [form, setForm] = useState({
        enjoyment: "",
        personalization: "",
        illustrations: "",
        navigation: "",
        readTimeAccuracy: "",
        actualReadMin: "",
        bugs: "",
        performance: "",
        likes: "",
        improvements: "",
        futureInterest: "",
        name: "",
        email: "",
    });
    const [error, setError] = useState("");
    const [submitting, setSubmitting] = useState(false);
    const [submitted, setSubmitted] = useState(false);

    const estReadMin = useMemo(() => {
        if (storyMeta?.estReadMin != null) return storyMeta.estReadMin;
        const pages = Number(storyMeta?.pageCount || 0);
        return Math.max(1, Math.ceil(pages * 1.5));
    }, [storyMeta]);

    useEffect(() => {
        if (!open) return;
        const onKey = (e) => { if (e.key === "Escape") onClose?.(); };
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, [open, onClose]);

    const update = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));

    async function submit(e) {
        e.preventDefault();
        setError("");
        setSubmitted(false);

        if (!form.enjoyment) {
            setError("Please rate your overall enjoyment.");
            return;
        }

        setSubmitting(true);
        try {
            const payload = {
                storyId: storyMeta?.id ?? null,
                storyTitle: storyMeta?.title ?? null,
                pageCount: storyMeta?.pageCount ?? null,
                estReadMin,
                ...form,
                actualReadMin: form.actualReadMin === "" ? null : Number(form.actualReadMin),
                notify: emailTargets,
            };

            const res = await fetch(`${apiBase.replace(/\/$/, "")}/feedback`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });
            if (!res.ok) throw new Error(`Server responded ${res.status}`);

            setSubmitted(true);
            onSubmitted?.(payload);
            setTimeout(() => onClose?.(), 900);
        } catch {
            setError("Could not send feedback. Please try again.");
        } finally {
            setSubmitting(false);
        }
    }

    if (!open) return null;

    return (
        <>
            <div className="feedback-backdrop" onClick={onClose} aria-hidden="true" />
            <div role="dialog" aria-modal="true" aria-labelledby="feedback-title" className="feedback-dialog">
                <div className="feedback-card">
                    <div className="feedback-head">
                        <div>
                            <h2 id="feedback-title" className="feedback-title">Starlit Stories — Quick Feedback</h2>
                            <p className="feedback-sub">Thanks for reading! Your feedback helps us make the experience more magical.</p>
                            {storyMeta && (
                                <p className="feedback-meta">
                                    <strong>Story:</strong> {storyMeta.title || "Untitled"} &nbsp;•&nbsp;
                                    <strong>Pages:</strong> {storyMeta.pageCount} &nbsp;•&nbsp;
                                    <strong>Est. Read:</strong> ~{estReadMin} min
                                </p>
                            )}
                        </div>
                        <button className="feedback-close" onClick={onClose} aria-label="Close">✕</button>
                    </div>

                    <form className="feedback-form" onSubmit={submit}>
                        {/* Story Experience */}
                        <div className="section">
                            <h3>Story Experience</h3>
                            <div className="grid-2">
                                <label className="label">
                                    Overall enjoyment
                                    <select
                                        className="select select--rating"
                                        value={form.enjoyment}
                                        onChange={update("enjoyment")}
                                    >
                                        <option value="" disabled hidden>Select…</option>
                                        {[1, 2, 3, 4, 5].map(n => (
                                            <option key={n} value={n}>{n} ⭐</option>
                                        ))}
                                    </select>
                                </label>
                                <label className="label">
                                    Matched your prompt/characters?
                                    <select className="select" value={form.personalization} onChange={update("personalization")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes, very much</option>
                                        <option>Somewhat</option>
                                        <option>Not really</option>
                                    </select>
                                </label>
                                <label className="label">
                                    Illustrations fit the story?
                                    <select className="select" value={form.illustrations} onChange={update("illustrations")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes</option>
                                        <option>Somewhat</option>
                                        <option>No</option>
                                    </select>
                                </label>
                                <label className="label">
                                    Navigation & readability
                                    <select className="select" value={form.navigation} onChange={update("navigation")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Smooth and easy</option>
                                        <option>A few small issues</option>
                                        <option>Hard to read or navigate</option>
                                    </select>
                                </label>
                            </div>
                        </div>

                        {/* Read Time & Flow */}
                        <div className="section">
                            <h3>Read Time & Flow</h3>
                            <div className="grid-2">
                                <label className="label">
                                    Was the estimated read time accurate?
                                    <select className="select" value={form.readTimeAccuracy} onChange={update("readTimeAccuracy")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes, pretty close</option>
                                        <option>Slightly off</option>
                                        <option>Not at all accurate</option>
                                    </select>
                                </label>
                                <label className="label">
                                    How long did it actually take? (min)
                                    <input
                                        type="number" min="0" step="1"
                                        className="input"
                                        value={form.actualReadMin}
                                        onChange={update("actualReadMin")}
                                        placeholder={String(estReadMin)}
                                    />
                                </label>
                            </div>
                        </div>

                        {/* Bugs & Performance */}
                        <div className="section">
                            <h3>Bugs & Performance</h3>
                            <div className="grid-2">
                                <label className="label" style={{ gridColumn: "1 / -1" }}>
                                    Any bugs or glitches? (include steps to reproduce if possible)
                                    <textarea className="textarea" value={form.bugs} onChange={update("bugs")} />
                                </label>
                                <label className="label">
                                    Site performance
                                    <select className="select" value={form.performance} onChange={update("performance")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Fast and responsive</option>
                                        <option>Occasionally slow</option>
                                        <option>Laggy or unresponsive</option>
                                    </select>
                                </label>
                            </div>
                        </div>

                        {/* Final Thoughts */}
                        <div className="section">
                            <h3>Final Thoughts</h3>
                            <div className="grid-1">
                                <label className="label">
                                    What did you like most?
                                    <textarea className="textarea" value={form.likes} onChange={update("likes")} />
                                </label>
                                <label className="label">
                                    What could be improved?
                                    <textarea className="textarea" value={form.improvements} onChange={update("improvements")} />
                                </label>
                                <label className="label">
                                    Interested in future features (e.g., narration)?
                                    <select className="select" value={form.futureInterest} onChange={update("futureInterest")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes</option>
                                        <option>Maybe</option>
                                        <option>No</option>
                                    </select>
                                </label>
                            </div>
                        </div>

                        {/* Optional Contact */}
                        <div className="section">
                            <h3>Optional Contact</h3>
                            <div className="grid-2">
                                <label className="label">
                                    Name (optional)
                                    <input type="text" className="input" value={form.name} onChange={update("name")} />
                                </label>
                                <label className="label">
                                    Email (optional)
                                    <input type="email" className="input" value={form.email} onChange={update("email")} />
                                </label>
                            </div>
                        </div>

                        <div className="form-footer">
                            {error && <span className="error">{error}</span>}
                            <button type="button" className="btn" onClick={onClose}>Cancel</button>
                            <button type="submit" className="btn btn-primary" disabled={submitting}>
                                {!submitting ? "Submit Feedback" : "Submitting…"}
                            </button>
                        </div>

                        {submitted && <div className="success">✨ Thank you! Your feedback was sent.</div>}
                    </form>
                </div>
            </div>
        </>
    );
}
