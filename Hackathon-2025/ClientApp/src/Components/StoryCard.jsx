import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { publicBase } from "../utils/urls";
import PropTypes from "prop-types";
import "./StoryCard.css";

export default function StoryCard({
    story,
    canDownload,                // boolean (e.g., user?.membership === 'pro' || 'premium')
    onShare,                     // (story) => void
    onDownload,                  // (story, format) => Promise<void>
    onDelete,                    // (storyId) => Promise<void>
    onOpen,                      // optional: open viewer (story) => void
    canCustomize,
    onCustomize,
}) {
    const [menuOpen, setMenuOpen] = useState(false);
    const [downloading, setDownloading] = useState(false);
    const [format, setFormat] = useState("pdf");
    const [deleting, setDeleting] = useState(false);

    const menuRef = useRef(null);
    const btnRef = useRef(null);

    // Close on outside click or ESC
    useEffect(() => {
        if (!menuOpen) return;

        const onDocClick = (e) => {
            if (!menuRef.current || !btnRef.current) return;
            if (menuRef.current.contains(e.target) || btnRef.current.contains(e.target)) return;
            setMenuOpen(false);
        };

        const onEsc = (e) => {
            if (e.key === "Escape") setMenuOpen(false);
        };

        document.addEventListener("mousedown", onDocClick);
        document.addEventListener("keydown", onEsc);
        return () => {
            document.removeEventListener("mousedown", onDocClick);
            document.removeEventListener("keydown", onEsc);
        };
    }, [menuOpen]);

    const handleDownload = async () => {
        if (!canDownload) {
            alert("Download is available for Pro and Premium users.");
            return;
        }
        try {
            setDownloading(true);
            await onDownload?.(story, format);
            setMenuOpen(false);
        } catch (e) {
            console.error(e);
            alert(`Download failed: ${e?.message || e}`);
        } finally {
            setDownloading(false);
        }
    };

    const handleDelete = async () => {
        if (deleting) return;
        const ok = confirm(`Delete "${story?.title || "this story"}"? This cannot be undone.`);
        if (!ok) return;
        try {
            setDeleting(true);
            await onDelete?.(story.id);
            setMenuOpen(false);
        } catch (e) {
            console.error(e);
            alert("Delete failed. Please try again.");
        } finally {
            setDeleting(false);
        }
    };

    // guard open during delete
    const openStory = () => {
        if (deleting) return;
        onOpen?.(story);
    };

    return (
        <div
            className={`scard ${deleting ? "is-busy" : ""}`}
            role="article"
            aria-label={story?.title}
            aria-busy={deleting ? "true" : "false"}
        >
            <button className="scard-thumb" onClick={openStory} aria-label={`Open ${story?.title}`}>
                <img
                    src={story?.coverImageUrl || "/placeholder.svg"}
                    alt={story?.title || "Story cover"}
                    loading="lazy"
                />
            </button>

            <div className="scard-meta" onClick={openStory}>
                <div className="scard-title" title={story?.title}>
                    {story?.title || "Untitled"}
                </div>
                <div className="scard-sub">
                    {(story?.pages?.length ?? 0)} pages • {story?.createdAt ? new Date(story.createdAt).toLocaleDateString() : ""}
                </div>
            </div>

            {/* Kebab menu */}
            <button
                ref={btnRef}
                className="scard-menuBtn"
                aria-haspopup="menu"
                aria-expanded={menuOpen}
                aria-label={`Actions for ${story?.title || "story"}`}
                disabled={deleting || downloading}
                onClick={(e) => {
                    e.stopPropagation();
                    setMenuOpen((v) => !v);
                }}
            >
                ⋮
            </button>

            {menuOpen && (
                <div ref={menuRef} className="scard-menu" role="menu">
                    {canCustomize && (
                        <button
                            role="menuitem"
                            className="scard-menuItem"
                            onClick={() => { onCustomize?.(story); setMenuOpen(false); }}
                        >
                            ✏️ Customize
                        </button>
                    )}
                    <button
                        role="menuitem"
                        className="scard-menuItem"
                        onClick={() => {
                            onShare?.(story);
                            setMenuOpen(false);
                        }}
                    >
                        📤 Share
                    </button>

                    <div
                        className={`scard-menuGroup ${canDownload ? "" : "is-disabled"}`}
                        role="group"
                        aria-label="Download"
                    >
                        <div className="scard-menuRow">
                            <button
                                role="menuitem"
                                className="scard-menuItem"
                                disabled={!canDownload || downloading}
                                onClick={handleDownload}
                            >
                                {downloading ? "Downloading…" : "📥 Download"}
                            </button>
                            <select
                                className="scard-select"
                                value={format}
                                onChange={(e) => setFormat(e.target.value)}
                                disabled={!canDownload || downloading}
                                aria-label="Download format"
                            >
                                <option value="pdf">PDF</option>
                                <option value="images">Images (ZIP)</option>
                            </select>
                        </div>
                        {!canDownload && <small className="scard-note">Upgrade to download</small>}
                    </div>

                    <hr className="scard-sep" />

                    <button
                        role="menuitem"
                        className="scard-menuItem is-danger"
                        onClick={handleDelete}
                        disabled={deleting}
                    >
                        {deleting ? "Deleting…" : "🗑️ Delete"}
                    </button>
                </div>
            )}
        </div>
    );
}

StoryCard.propTypes = {
    story: PropTypes.shape({
        id: PropTypes.oneOfType([PropTypes.number, PropTypes.string]),
        title: PropTypes.string,
        coverImageUrl: PropTypes.string,
        pages: PropTypes.arrayOf(
            PropTypes.shape({
                text: PropTypes.string,
                imageUrl: PropTypes.string,
            })
        ),
        createdAt: PropTypes.oneOfType([PropTypes.string, PropTypes.number, PropTypes.instanceOf(Date)]),
    }).isRequired,
    canDownload: PropTypes.bool,
    onShare: PropTypes.func,
    onDownload: PropTypes.func,
    onDelete: PropTypes.func,
    onOpen: PropTypes.func,
    canCustomize: PropTypes.bool,
    onCustomize: PropTypes.func,
};

StoryCard.defaultProps = {
    canDownload: false,
    onShare: undefined,
    onDownload: undefined,
    onDelete: undefined,
    onOpen: undefined,
    canCustomize: false,
    onCustomize: undefined,
};