import React, { useEffect, useMemo, useState } from "react";
import "./FeedbackModal.css";
import api from "../api";

export default function FeedbackModal({
    open,
    onClose,
    storyMeta = { id: null, title: "", pageCount: 0, estReadMin: null },
    onSubmitted,
}) {
    const [form, setForm] = useState({
        enjoyment: "",
        promptMatch: "",
        charactersMatch: "",
        promptCharactersOff: "",
        illustrationsSatisfaction: "",
        illustrationsOff: "",
        navigation: "",
        actualReadMin: "",
        storyLength: "",
        storyFlow: "",
        encounteredBugs: "",
        bugs: "",
        performance: "",
        likes: "",
        improvements: "",
        nextFeature: "",
        readWith: "",
        createAgainLikelihood: "",
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

    const promptOrCharactersNeedsFollowup = useMemo(() => (
        form.promptMatch === "Not really" || form.charactersMatch === "Not really"
    ), [form.promptMatch, form.charactersMatch]);

    const illustrationsNeedsFollowup = useMemo(() => (
        form.illustrationsSatisfaction === "Mixed feelings" || form.illustrationsSatisfaction === "Not satisfied"
    ), [form.illustrationsSatisfaction]);

    const showBugDetails = form.encounteredBugs === "Yes";

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
                storyId: storyMeta?.id ? String(storyMeta.id) : null,
                storyTitle: storyMeta?.title ?? null,
                pageCount: storyMeta?.pageCount ?? null,
                estReadMin,
                ...form,
                actualReadMin: form.actualReadMin === "" ? null : Number(form.actualReadMin),
                promptCharactersOff: promptOrCharactersNeedsFollowup ? form.promptCharactersOff : null,
                illustrationsOff: illustrationsNeedsFollowup ? form.illustrationsOff : null,
                bugs: showBugDetails ? form.bugs : null,
            };

            await api.post("/feedback", payload);

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
                                        {[1, 2, 3, 4, 5].map((n) => (
                                            <option key={n} value={n}>{n} ⭐</option>
                                        ))}
                                    </select>
                                    <span className="field-note">1 = Didn&apos;t enjoy it, 5 = Loved it</span>
                                </label>
                                <label className="label">
                                    Did the story match what you described?
                                    <select className="select" value={form.promptMatch} onChange={update("promptMatch")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes, very much</option>
                                        <option>Somewhat</option>
                                        <option>Not really</option>
                                    </select>
                                </label>
                                <label className="label">
                                    Matched your characters?
                                    <select className="select" value={form.charactersMatch} onChange={update("charactersMatch")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes, very much</option>
                                        <option>Somewhat</option>
                                        <option>Not really</option>
                                    </select>
                                </label>
                                {promptOrCharactersNeedsFollowup && (
                                    <label className="label" style={{ gridColumn: "1 / -1" }}>
                                        What felt off?
                                        <textarea className="textarea" value={form.promptCharactersOff} onChange={update("promptCharactersOff")} />
                                    </label>
                                )}
                                <label className="label" style={{ gridColumn: "1 / -1" }}>
                                    How satisfied were you with the illustrations?
                                    <select className="select" value={form.illustrationsSatisfaction} onChange={update("illustrationsSatisfaction")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Loved them</option>
                                        <option>Pretty good</option>
                                        <option>Mixed feelings</option>
                                        <option>Not satisfied</option>
                                    </select>
                                </label>
                                {illustrationsNeedsFollowup && (
                                    <label className="label" style={{ gridColumn: "1 / -1" }}>
                                        What felt off about the illustrations?
                                        <textarea className="textarea" value={form.illustrationsOff} onChange={update("illustrationsOff")} />
                                    </label>
                                )}
                            </div>
                        </div>

                        <div className="section">
                            <h3>Read Time & Flow</h3>
                            <div className="grid-2">
                                <label className="label">
                                    About how many minutes did it take to read the story?
                                    <input
                                        type="number"
                                        min="0"
                                        step="1"
                                        className="input"
                                        value={form.actualReadMin}
                                        onChange={update("actualReadMin")}
                                        placeholder={String(estReadMin)}
                                    />
                                </label>
                                <label className="label">
                                    How did the story feel to read?
                                    <select className="select" value={form.storyFlow} onChange={update("storyFlow")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Engaging throughout</option>
                                        <option>Started strong, lost me</option>
                                        <option>Felt rushed</option>
                                        <option>Felt slow</option>
                                    </select>
                                </label>
                                <label className="label" style={{ gridColumn: "1 / -1" }}>
                                    How did the story length feel?
                                    <select className="select" value={form.storyLength} onChange={update("storyLength")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Too short</option>
                                        <option>Just right</option>
                                        <option>Too long</option>
                                    </select>
                                </label>
                            </div>
                        </div>

                        <div className="section">
                            <h3>Bugs & Performance</h3>
                            <div className="grid-2">
                                <label className="label">
                                    Did you encounter any bugs or glitches?
                                    <select className="select" value={form.encounteredBugs} onChange={update("encounteredBugs")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Yes</option>
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
                                {showBugDetails && (
                                    <label className="label" style={{ gridColumn: "1 / -1" }}>
                                        Any bugs or glitches? (include steps to reproduce if possible)
                                        <textarea className="textarea" value={form.bugs} onChange={update("bugs")} />
                                    </label>
                                )}
                                <label className="label" style={{ gridColumn: "1 / -1" }}>
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
                                    Who did you read this with?
                                    <select className="select" value={form.readWith} onChange={update("readWith")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Alone</option>
                                        <option>With my child</option>
                                        <option>Other</option>
                                    </select>
                                </label>
                                <label className="label">
                                    How likely are you to create another story?
                                    <select className="select" value={form.createAgainLikelihood} onChange={update("createAgainLikelihood")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Definitely</option>
                                        <option>Probably</option>
                                        <option>Not sure yet</option>
                                        <option>Unlikely</option>
                                    </select>
                                </label>
                                <label className="label">
                                    If we could add one of these features next, which would be most valuable to you?
                                    <select className="select" value={form.nextFeature} onChange={update("nextFeature")}>
                                        <option value="" disabled hidden>Select…</option>
                                        <option>Audio narration</option>
                                        <option>Printable keepsake version</option>
                                        <option>Multiple language options</option>
                                    </select>
                                </label>
                            </div>
                        </div>

                        <div className="section">
                            <h3>Optional Contact</h3>
                            <p className="feedback-note">Leave your contact info if you&apos;d be open to a quick follow-up conversation.</p>
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
