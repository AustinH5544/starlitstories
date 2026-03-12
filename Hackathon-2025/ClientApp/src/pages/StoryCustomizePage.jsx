"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import "./StoryCustomizePage.css";

/**
 * StoryCustomizePage
 *
 * A editor that lets users arrange page text like a real picture book:
 *  - Drag text boxes anywhere over the illustration
 *  - Resize text boxes via handles
 *  - Style controls (font, size, color, background, alignment)
 *  - Per-page layouts with quick copy/paste layout between pages
 *  - Autosaves to localStorage (can be swapped for a backend API later)
 */
export default function StoryCustomizePage() {
    const { state } = useLocation();
    const navigate = useNavigate();
    const { user } = useAuth();

    const [story, setStory] = useState(null);
    const [pageIndex, setPageIndex] = useState(-1);
    const [boxesByPage, setBoxesByPage] = useState({});
    const [selectedBoxId, setSelectedBoxId] = useState(null);
    const stageRef = useRef(null);
    const topbarRef = useRef(null);
    const [topbarOffset, setTopbarOffset] = useState(56);
    const [stageSize, setStageSize] = useState({ stageW: 600, stageH: 450 });
    const [layoutBaseSize, setLayoutBaseSize] = useState({ stageW: 600, stageH: 450, coverStageH: 450 });

    // Mobile drawers state: 'pages' | 'inspector' | null
    const [panel, setPanel] = useState(null);
    const [headerOpen, setHeaderOpen] = useState(true);
    const openPages = () => setPanel("pages");
    const toggleInspector = () =>
        setPanel(prev => (prev === "inspector" ? null : "inspector"));
    const closePanels = () => setPanel(null);

    function getStageMetrics() {
        const rect = stageRef.current?.getBoundingClientRect();
        const stageW = Math.round(rect?.width || stageSize.stageW || 600);
        const stageH = Math.round(rect?.height || stageSize.stageH || Math.round(stageW * 0.75));
        return { stageW, stageH };
    }

    useEffect(() => {
        const el = stageRef.current;
        if (!el) return;

        const syncStageSize = () => {
            const rect = el.getBoundingClientRect();
            const next = {
                stageW: Math.round(rect.width || 600),
                stageH: Math.round(rect.height || Math.round((rect.width || 600) * 0.75)),
            };
            setStageSize((prev) =>
                prev.stageW === next.stageW && prev.stageH === next.stageH ? prev : next
            );
        };

        syncStageSize();

        const ro = new ResizeObserver(() => syncStageSize());
        ro.observe(el);
        window.addEventListener("resize", syncStageSize);
        window.addEventListener("orientationchange", syncStageSize);

        return () => {
            ro.disconnect();
            window.removeEventListener("resize", syncStageSize);
            window.removeEventListener("orientationchange", syncStageSize);
        };
    }, [story, pageIndex]);

    useEffect(() => {
        const el = topbarRef.current;
        if (!el) return;

        const syncTopbarOffset = () => {
            setTopbarOffset(Math.round(el.getBoundingClientRect().height || 56));
        };

        syncTopbarOffset();

        const ro = new ResizeObserver(() => syncTopbarOffset());
        ro.observe(el);
        window.addEventListener("resize", syncTopbarOffset);

        return () => {
            ro.disconnect();
            window.removeEventListener("resize", syncTopbarOffset);
        };
    }, [headerOpen, story?.title]);

    // stable helper
    function createDefaultTextBox(text, page, stageW = 600, stageH = 450) {
        const x = Math.round(clampN(stageW * 0.04, 12, 24));
        const y = Math.round(clampN(stageH * 0.05, 12, 24));
        const w = Math.round(clampN(stageW * 0.46, 160, 420));
        const h = Math.round(clampN(stageH * 0.24, 88, 160));
        const fontSize = Math.round(clampN(stageW * 0.028, 14, 20));
        const padding = Math.round(clampN(stageW * 0.02, 8, 16));
        const radius = Math.round(clampN(stageW * 0.015, 10, 12));

        return {
            id: crypto.randomUUID(),
            page,
            x, y, w, h,
            text,
            style: {
                fontFamily: "Georgia, 'Times New Roman', serif",
                fontSize, fontWeight: 400, lineHeight: 1.35,
                color: "#1b1b1b", bg: "#ffffff", bgAlpha: 0.8,
                align: "left", padding, radius, shadow: true,
                textEdge: false, textEdgeColor: "#ffffff", textEdgeWidth: 1,
            },
        };
    }

    function resetCurrentPage() {
        const key = storageKey(story);

        // Cover page
        if (pageIndex === -1) {
            const rect0 = stageRef.current?.getBoundingClientRect();
            const estW = Math.round(rect0?.width || 600);
            const estH = Math.round(rect0?.height || Math.round(estW * 0.75));
            const fresh = story.title ? [createDefaultCoverBox(story.title, estW, estH)] : [];

            setBoxesByPage(prev => {
                const next = { ...prev, [-1]: fresh };
                localStorage.setItem(key, JSON.stringify(next));
                localStorage.setItem(`${key}:cover`, JSON.stringify(fresh));
                return next;
            });
            setSelectedBoxId(null);
            return;
        }

        // Regular page
        const { stageW, stageH } = getStageMetrics();
        setBoxesByPage(prev => {
            const next = {
                ...prev,
                [pageIndex]: [createDefaultTextBox(story.pages[pageIndex]?.text || "", pageIndex, stageW, stageH)]
            };
            localStorage.setItem(key, JSON.stringify(next));
            return next;
        });
        setSelectedBoxId(null);
    }

    function resetAllLayouts() {
        const key = storageKey(story);

        // Re-seed all regular pages
        const base = seedFromStory(story);

        // Fresh default cover title
        const rect0 = stageRef.current?.getBoundingClientRect();
        const estW = Math.round(rect0?.width || 600);
        const estH = Math.round(rect0?.height || Math.round(estW * 0.75));
        base[-1] = story.title ? [createDefaultCoverBox(story.title, estW, estH)] : [];

        setBoxesByPage(base);
        localStorage.setItem(key, JSON.stringify(base));
        localStorage.setItem(`${key}:cover`, JSON.stringify(base[-1]));
        setSelectedBoxId(null);
    }

    function seedFromStory(s) {
        const { stageW, stageH } = getStageMetrics();
        const init = {};
        (s?.pages || []).forEach((p, i) => {
            init[i] = [createDefaultTextBox(p?.text || "", i, stageW, stageH)];
        });
        return init;
    }

    const FONT_OPTIONS = [
        { label: "System Sans (UI)", value: `ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, "Apple Color Emoji", "Segoe UI Emoji"` },
        { label: "System Serif", value: `ui-serif, Georgia, "Times New Roman", Times, serif` },
        { label: "System Mono", value: `ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace` },
        { label: "Georgia", value: `Georgia, "Times New Roman", serif` },
        { label: "Garamond", value: `Garamond, "Times New Roman", serif` },
        { label: "Times New Roman", value: `"Times New Roman", Times, serif` },
        { label: "Arial", value: `Arial, Helvetica, sans-serif` },
        { label: "Helvetica", value: `Helvetica, Arial, sans-serif` },
        { label: "Trebuchet MS", value: `"Trebuchet MS", Tahoma, sans-serif` },
        { label: "Verdana", value: `Verdana, Geneva, sans-serif` },
        // Pre-named Google families (loaded on demand)
        { label: "Montserrat (Google)", value: `Montserrat, ui-sans-serif, system-ui, Arial` },
        { label: "Merriweather (Google)", value: `Merriweather, ui-serif, Georgia` },
        { label: "Lora (Google)", value: `Lora, ui-serif, Georgia` },
        { label: "Nunito (Google)", value: `Nunito, ui-sans-serif, Arial` },
        { label: "Pacifico (Google)", value: `Pacifico, cursive` },
    ];

    function googleCssUrl(family, weights = "300;400;600;700") {
        // Accept either raw family ("Merriweather") or a CSS stack starting with a Google family
        const first = family.split(",")[0].replace(/['"]/g, "").trim();
        const fam = first.replace(/\s+/g, "+");
        return `https://fonts.googleapis.com/css2?family=${fam}:wght@${weights}&display=swap`;
    }

    async function ensureFontLoaded(fontFamily) {
        if (!fontFamily) return;
        const id = `gf-${fontFamily.split(",")[0].replace(/['"\s]/g, "")}`;
        if (!document.getElementById(id)) {
            const link = document.createElement("link");
            link.id = id;
            link.rel = "stylesheet";
            link.href = googleCssUrl(fontFamily);
            document.head.appendChild(link);
        }
        try {
            // try to warm up one typical size; it’s okay if it resolves immediately
            await document.fonts.load(`16px ${fontFamily.split(",")[0]}`);
        } catch { /* ignore */ }
    }

    function setFontFamily(family) {
        if (!selectedBox) return;
        ensureFontLoaded(family);
        setBoxesByPage(prev => {
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map(b =>
                b.id === selectedBox.id ? { ...b, style: { ...b.style, fontFamily: family } } : b
            );
            return next;
        });
    }

    useEffect(() => {
        const membership = String(user?.membership ?? "free").toLowerCase();
        if (membership === "free") {
            alert("Story customization is available for Pro, Premium, and Storybook users.");
            navigate("/upgrade", { replace: true });
        }
    }, [navigate, user?.membership]);

    useEffect(() => {
        let s = state?.story;
        if (!s) {
            const saved = localStorage.getItem("story");
            if (saved) s = JSON.parse(saved);
        }
        if (s && Array.isArray(s.pages)) {
            setStory(s);

            const key = storageKey(s);
            const savedLayouts = JSON.parse(localStorage.getItem(key) || "null");
            const { stageW, stageH } = getStageMetrics();
            const meta = JSON.parse(localStorage.getItem(`${key}:meta`) || "null");

            setLayoutBaseSize({
                stageW: meta?.stageW || stageW,
                stageH: meta?.stageH || stageH,
                coverStageH: meta?.coverStageH || stageH,
            });

            function normalizeLayouts(saved, s) {
                if (!saved || typeof saved !== "object") return null;

                // Drop any legacy cover entry
                const clean = { ...saved };
                delete clean["-1"];

                const keys = Object.keys(clean).map(Number).sort((a, b) => a - b);

                // Old data used 1..N (cover was 0) -> shift to 0..N-1
                if (keys[0] === 1 && keys[keys.length - 1] === s.pages.length) {
                    const next = {};
                    for (let i = 0; i < s.pages.length; i++) {
                        next[i] = (clean[i + 1] || []).map(b => ({ ...b, id: crypto.randomUUID(), page: i }));
                        if (!next[i].length && (s.pages[i]?.text ?? "")) {
                            next[i] = [createDefaultTextBox(s.pages[i].text, i, stageW, stageH)];
                        }
                    }
                    return next;
                }

                // Length mismatch or missing key 0? Reseed.
                if (keys.length !== s.pages.length || !keys.includes(0)) return null;

                // Otherwise coerce shape and ensure ids/pages
                const next = {};
                for (let i = 0; i < s.pages.length; i++) {
                    next[i] = (clean[i] || []).map(b => ({ ...b, id: b.id || crypto.randomUUID(), page: i }));
                    if (!next[i].length && (s.pages[i]?.text ?? "")) {
                        next[i] = [createDefaultTextBox(s.pages[i].text, i, stageW, stageH)];
                    }
                }
                return next;
            }

            const normalized = normalizeLayouts(savedLayouts, s);
            const base = normalized || seedFromStory(s);

            // pull any previously saved cover boxes
            const coverSaved = JSON.parse(localStorage.getItem(`${key}:cover`) || "null");

            // estimate stage size if we have it
            const rect0 = stageRef.current?.getBoundingClientRect();
            const estW = Math.round(rect0?.width || 600);
            const estH = Math.round(rect0?.height || Math.round(estW * 0.75));

            // ensure a cover layout key (-1) exists so the user can edit the cover
            if (!Array.isArray(base[-1])) {
                base[-1] = Array.isArray(coverSaved)
                    ? coverSaved
                    : (s.title ? [createDefaultCoverBox(s.title, estW, estH)] : []);
            }

            // set once, save once
            setBoxesByPage(base);
            localStorage.setItem(key, JSON.stringify(base));
        }
    }, [state]);

    // Persist on change
    useEffect(() => {
        if (!story) return;
        const key = storageKey(story);
        localStorage.setItem(`${key}:cover`, JSON.stringify(boxesByPage[-1] || []));
        localStorage.setItem(key, JSON.stringify(boxesByPage));

        localStorage.setItem(`${key}:meta`, JSON.stringify(layoutBaseSize));
    }, [story, boxesByPage, layoutBaseSize]);

    // Helpers
    const currentBoxes = boxesByPage?.[pageIndex] || [];
    const selectedBox = useMemo(
        () => currentBoxes.find((b) => b.id === selectedBoxId) || null,
        [currentBoxes, selectedBoxId]
    );

    function storageKey(s) {
        return `layout:${s.id ?? s.title ?? "untitled"}`;
    }

    function addTextBox() {
        const { stageW, stageH } = getStageMetrics();
        setBoxesByPage((prev) => {
            const next = { ...prev };
            const copy = [...(next[pageIndex] || [])];
            copy.push(createDefaultTextBox("New text…", pageIndex, stageW, stageH));
            next[pageIndex] = copy;
            return next;
        });
    }

    function duplicateBox(id) {
        setBoxesByPage((prev) => {
            const next = { ...prev };
            const copy = [...(next[pageIndex] || [])];
            const src = copy.find((b) => b.id === id);
            if (!src) return prev;
            const dup = { ...src, id: crypto.randomUUID(), x: src.x + 24, y: src.y + 24 };
            copy.push(dup);
            next[pageIndex] = copy;
            return next;
        });
    }

    function removeBox(id) {
        setBoxesByPage((prev) => {
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).filter((b) => b.id !== id);
            return next;
        });
        if (selectedBoxId === id) setSelectedBoxId(null);
    }

    function onDragAbs(id, nextX, nextY) {
        setBoxesByPage((prev) => {
            const stage = stageRef.current;
            const bounds = stage ? stage.getBoundingClientRect() : null;
            const scaleX = bounds ? bounds.width / (layoutBaseSize.stageW || bounds.width) : 1;
            const scaleY = bounds ? bounds.height / (layoutBaseSize.stageH || bounds.height) : 1;
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;
                let x = nextX,
                    y = nextY;
                if (bounds) {
                    const maxX = bounds.width / scaleX - b.w - 2;
                    const maxY = bounds.height / scaleY - b.h - 2;
                    x = Math.max(2, Math.min(x, maxX));
                    y = Math.max(2, Math.min(y, maxY));
                }
                return { ...b, x, y };
            });
            return next;
        });
    }

    function onDrag(id, dx, dy) {
        setBoxesByPage((prev) => {
            const stage = stageRef.current;
            const bounds = stage ? stage.getBoundingClientRect() : null;
            const scaleX = bounds ? bounds.width / (layoutBaseSize.stageW || bounds.width) : 1;
            const scaleY = bounds ? bounds.height / (layoutBaseSize.stageH || bounds.height) : 1;
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;
                let x = b.x + (dx / scaleX);
                let y = b.y + (dy / scaleY);
                if (bounds) {
                    const maxX = bounds.width / scaleX - b.w - 2;
                    const maxY = bounds.height / scaleY - b.h - 2;
                    x = Math.max(2, Math.min(x, maxX));
                    y = Math.max(2, Math.min(y, maxY));
                }
                return { ...b, x, y };
            });
            return next;
        });
    }

    function onResize(id, dw, dh, anchor = "se") {
        setBoxesByPage((prev) => {
            const stage = stageRef.current;
            const bounds = stage ? stage.getBoundingClientRect() : null;
            const scaleX = bounds ? bounds.width / (layoutBaseSize.stageW || bounds.width) : 1;
            const scaleY = bounds ? bounds.height / (layoutBaseSize.stageH || bounds.height) : 1;
            const baseDw = dw / scaleX;
            const baseDh = dh / scaleY;

            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;

                let x = b.x,
                    y = b.y,
                    w = b.w,
                    h = b.h;
                const minW = 72,
                    minH = 48;

                const right = b.x + b.w;
                const bottom = b.y + b.h;

                if (anchor.includes("e")) {
                    w = w + baseDw;
                }
                if (anchor.includes("w")) {
                    x = b.x - baseDw;
                    w = right - x;
                }
                if (anchor.includes("s")) {
                    h = h + baseDh;
                }
                if (anchor.includes("n")) {
                    y = b.y - baseDh;
                    h = bottom - y;
                }

                w = Math.max(minW, w);
                h = Math.max(minH, h);

                if (bounds) {
                    const pad = 2;
                    const maxX = bounds.width / scaleX - minW - pad;
                    const maxY = bounds.height / scaleY - minH - pad;

                    x = Math.max(pad, Math.min(x, maxX));
                    y = Math.max(pad, Math.min(y, maxY));

                    w = Math.min(w, bounds.width / scaleX - x - pad);
                    h = Math.min(h, bounds.height / scaleY - y - pad);
                }

                return { ...b, x, y, w, h };
            });
            return next;
        });
    }

    function updateStyle(partial) {
        if (!selectedBox) return;
        setBoxesByPage((prev) => {
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) =>
                b.id === selectedBox.id ? { ...b, style: { ...b.style, ...partial } } : b
            );
            return next;
        });
    }

    function updateText(text) {
        if (!selectedBox) return;
        setBoxesByPage((prev) => {
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) =>
                b.id === selectedBox.id ? { ...b, text } : b
            );
            return next;
        });
    }

    function copyLayoutFrom(fromIndex) {
        setBoxesByPage((prev) => {
            const next = { ...prev };
            const src = prev[fromIndex] || [];
            next[pageIndex] = src.map((b) => ({ ...b, id: crypto.randomUUID(), page: pageIndex }));
            return next;
        });
    }

    function createDefaultCoverBox(title, stageW = 600, stageH = 450) {
        const w = Math.round(stageW * 0.84);
        const x = Math.round((stageW - w) / 2);
        const y = Math.round(stageH * 0.06);
        const fontSize = Math.round(clampN(stageW * 0.05, 22, 36));
        const padding = Math.round(clampN(stageW * 0.012, 6, 10));
        return {
            id: crypto.randomUUID(),
            page: -1,
            x, y, w,
            h: Math.round(stageH * 0.16),
            text: title || "My Story",
            style: {
                fontFamily: "Georgia, 'Times New Roman', serif",
                fontSize, fontWeight: 700, lineHeight: 1.2,
                color: "#1b1b1b", bg: "#ffffff", bgAlpha: 0.0,
                align: "center", padding, radius: 12, shadow: false,
                textEdge: false, textEdgeColor: "#ffffff", textEdgeWidth: 1
            },
        };
    }

    function applySelectedStyleToAllPages() {
        if (!selectedBox) return;
        setBoxesByPage((prev) => {
            const next = { ...prev };
            Object.keys(next).forEach((k) => {
                const i = Number(k);
                if (i === -1) return; // Skip cover page
                next[i] = (next[i] || []).map((b) => ({
                    ...b,
                    style: { ...b.style, ...selectedBox.style },
                }));
            });
            return next;
        });
    }

    if (!story || !Array.isArray(story.pages)) {
        return (
            <div className="customizer">
                <div className="pane empty">
                    <h2>No story loaded</h2>
                    <p>Go back and generate or open a story first.</p>
                    <button className="btn" onClick={() => navigate("/create")}>
                        Create a Story
                    </button>
                </div>
            </div>
        );
    }

    const page = story.pages[pageIndex];
    const currentStageW = stageSize.stageW;
    const currentStageH = stageSize.stageH;
    const scaleX = currentStageW / (layoutBaseSize.stageW || currentStageW || 1);
    const scaleY = currentStageH / (layoutBaseSize.stageH || currentStageH || 1);
    const contentScale = Math.min(scaleX || 1, scaleY || 1);

    return (
        <div className="customizer" style={{ "--customizer-topbar-offset": `${topbarOffset}px` }}>
            {/* Top bar */}
            <header className="topbar" data-collapsed={!headerOpen} ref={topbarRef}>
                <div className="left-actions">
                    <button className="btn" onClick={() => navigate(-1)} aria-label="Go back">
                        ← Back
                    </button>

                    {/* Mobile: open pages drawer */}
                    <button
                        className="btn show-on-mobile"
                        onClick={openPages}
                        aria-controls="drawer-pages"
                        aria-expanded={panel === "pages"}
                    >
                        ☰ Pages
                    </button>
                </div>

                <div className="title">
                    <div className="title-bar">
                        <span>Customize: <span>{story.title || "Untitled"}</span></span>
                        <button
                            className="btn ghost topbar-toggle show-on-mobile"
                            type="button"
                            onClick={() => setHeaderOpen((prev) => !prev)}
                            aria-expanded={headerOpen}
                            aria-label={headerOpen ? "Collapse customize header" : "Expand customize header"}
                        >
                            {headerOpen ? "Hide" : "Show"}
                        </button>
                    </div>
                </div>

                <div className="actions">
                    {/* Mobile: open inspector drawer */}
                    <button
                        className="btn show-on-mobile"
                        onClick={toggleInspector}
                        disabled={!selectedBox}
                        aria-controls="drawer-inspector"
                        aria-expanded={panel === "inspector"}
                        title={selectedBox ? "Open inspector" : "Select a text box to edit"}
                    >
                        🎚 Inspector
                    </button>

                    <button className="btn" onClick={addTextBox}>➕ Add Text Box</button>
                    <button
                        className="btn ghost"
                        onClick={resetCurrentPage}
                        title="Reset just this page (keeps other pages as-is)"
                    >
                        Reset page
                    </button>
                    <button
                        className="btn ghost"
                        onClick={resetAllLayouts}
                        title="Reset all pages (including a fresh cover title)"
                    >
                        Reset all
                    </button>
                    <button
                        className="btn"
                        disabled={selectedBox == null}
                        onClick={() => duplicateBox(selectedBox?.id)}
                    >
                        ⧉ Duplicate
                    </button>
                    <button
                        className="btn danger"
                        disabled={selectedBox == null}
                        onClick={() => removeBox(selectedBox?.id)}
                    >
                        🗑 Remove
                    </button>
                </div>
            </header>

            {/* Scrim for mobile drawers */}
            <div
                className="scrim"
                hidden={panel === null}
                onClick={closePanels}
                aria-hidden={panel === null}
            />

            {/* Main layout */}
            <div className="layout">
                {/* Left: page picker (drawer on mobile) */}
                <aside
                    id="drawer-pages"
                    className="sidebar drawer"
                    data-open={panel === "pages"}
                    aria-hidden={panel !== "pages"}
                >
                    <div className="pages">
                        <button
                            className={`thumb ${pageIndex === -1 ? "active" : ""}`}
                            onClick={() => { setPageIndex(-1); setSelectedBoxId(null); closePanels(); }}
                            title="Cover"
                        >
                            📖
                        </button>
                        {story.pages.map((_, i) => (
                            <button
                                key={i}
                                className={`thumb ${i === pageIndex ? "active" : ""}`}
                                onClick={() => {
                                    setPageIndex(i);
                                    closePanels();
                                }}
                            >
                                {i + 1}
                            </button>
                        ))}
                    </div>

                    <div className="copybar">
                        <label>Copy layout from:</label>
                        <div className="copybar-controls">
                            <select
                                className="input"
                                onChange={(e) => copyLayoutFrom(Number(e.target.value))}
                                value=""
                            >
                                <option value="" disabled>Choose page…</option>
                                {story.pages.map((_, i) => (
                                    <option key={i} value={i}>Page {i + 1}</option>
                                ))}
                            </select>
                            {pageIndex !== -1 && (
                                <button
                                    className="btn ghost"
                                    disabled={!selectedBox}
                                    onClick={applySelectedStyleToAllPages}
                                >
                                Apply selected style → all pages
                                </button>
                            )}
                        </div>
                    </div>
                </aside>

                {/* Center: stage */}
                <main className="stage-wrap">
                    <div className="stage" ref={stageRef}>
                        <img
                            src={page?.imageUrl || story.coverImageUrl || "/placeholder.svg"}
                            alt="Page"
                            className="page-img"
                        />
                        {(boxesByPage[pageIndex] || []).map((b) => (
                            <DraggableBox
                                key={b.id}
                                box={b}
                                selected={selectedBoxId === b.id}
                                scaleX={scaleX}
                                scaleY={scaleY}
                                contentScale={contentScale}
                                onSelect={() => setSelectedBoxId(b.id)}
                                onDrag={onDrag}
                                onResize={onResize}
                                onTextChange={updateText}
                            />
                        ))}
                    </div>
                </main>

                {/* Right: inspector (drawer on mobile) */}
                <aside
                    id="drawer-inspector"
                    className="inspector drawer"
                    data-open={panel === "inspector"}
                    aria-hidden={panel !== "inspector"}
                >
                    <h3>Text Box</h3>
                    {selectedBox ? (
                        <>
                            <label>Text</label>
                            <textarea
                                className="input"
                                value={selectedBox.text}
                                onChange={(e) => updateText(e.target.value)}
                                rows={6}
                            />

                            <label>Font family</label>
                            <div className="grid2">
                                <select
                                    className="input"
                                    value={selectedBox.style.fontFamily}
                                    onChange={(e) => setFontFamily(e.target.value)}
                                    style={{ fontFamily: selectedBox.style.fontFamily }}
                                    title="Choose a web-safe or Google font"
                                >
                                    {FONT_OPTIONS.map(opt => (
                                        <option key={opt.label} value={opt.value} style={{ fontFamily: opt.value }}>
                                            {opt.label}
                                        </option>
                                    ))}
                                </select>

                                {/* Quick custom Google family loader */}
                                <div className="row gap sm">
                                    <input
                                        className="input"
                                        placeholder='Custom Google family (e.g., "Quicksand")'
                                        onKeyDown={async (e) => {
                                            if (e.key === "Enter") {
                                                const fam = e.currentTarget.value.trim();
                                                if (!fam) return;
                                                const stack = `${fam}, ui-sans-serif, Arial`;
                                                await ensureFontLoaded(stack);
                                                setFontFamily(stack);
                                                e.currentTarget.value = "";
                                            }
                                        }}
                                        title='Press Enter to load and apply. Example: Quicksand'
                                    />
                                </div>
                            </div>

                            <div className="grid2">
                                <div>
                                    <label>Font size</label>
                                    <input
                                        type="number"
                                        className="input"
                                        min={8}
                                        max={64}
                                        value={selectedBox.style.fontSize}
                                        onChange={(e) =>
                                            updateStyle({ fontSize: clampN(e.target.value, 8, 64) })
                                        }
                                    />
                                </div>
                                <div>
                                    <label>Weight</label>
                                    <select
                                        className="input"
                                        value={selectedBox.style.fontWeight}
                                        onChange={(e) => updateStyle({ fontWeight: Number(e.target.value) })}
                                    >
                                        <option value={300}>Light</option>
                                        <option value={400}>Regular</option>
                                        <option value={600}>Semibold</option>
                                        <option value={700}>Bold</option>
                                    </select>
                                </div>
                            </div>

                            <div className="grid2">
                                <div>
                                    <label>Line height</label>
                                    <input
                                        type="number"
                                        step="0.05"
                                        className="input"
                                        min={1.1}
                                        max={2}
                                        value={selectedBox.style.lineHeight}
                                        onChange={(e) =>
                                            updateStyle({ lineHeight: clampN(e.target.value, 1.1, 2) })
                                        }
                                    />
                                </div>
                                <div>
                                    <label>Padding</label>
                                    <input
                                        type="number"
                                        className="input"
                                        min={0}
                                        max={48}
                                        value={selectedBox.style.padding}
                                        onChange={(e) =>
                                            updateStyle({ padding: clampN(e.target.value, 0, 48) })
                                        }
                                    />
                                </div>
                            </div>

                            <div className="grid2">
                                <div>
                                    <label>Text color</label>
                                    <input
                                        type="color"
                                        className="input"
                                        value={selectedBox.style.color}
                                        onChange={(e) => updateStyle({ color: e.target.value })}
                                    />
                                </div>
                                <div>
                                    <label>Background color</label>
                                    <input
                                        type="color"
                                        className="input"
                                        value={(selectedBox.style.bg || "#ffffff").slice(0, 7)}
                                        onChange={(e) => updateStyle({ bg: e.target.value })}
                                    />
                                </div>
                            </div>

                            <label className="row gap sm">
                                <input
                                    type="checkbox"
                                    checked={!!selectedBox.style.textEdge}
                                    onChange={(e) => updateStyle({ textEdge: e.target.checked })}
                                />
                                Text edge (outline)
                            </label>

                            {selectedBox.style.textEdge && (
                                <div className="grid2">
                                    <div>
                                        <label>Edge color</label>
                                        <input
                                            type="color"
                                            className="input"
                                            value={selectedBox.style.textEdgeColor || "#ffffff"}
                                            onChange={(e) => updateStyle({ textEdgeColor: e.target.value })}
                                        />
                                    </div>
                                    <div>
                                        <label>Edge width</label>
                                        <input
                                            type="number"
                                            className="input"
                                            min={1}
                                            max={6}
                                            value={selectedBox.style.textEdgeWidth ?? 1}
                                            onChange={(e) =>
                                                updateStyle({ textEdgeWidth: clampN(e.target.value, 1, 6) })
                                            }
                                        />
                                    </div>
                                </div>
                            )}

                            <div className="grid2">
                                <div>
                                    <label>Background opacity</label>
                                    <input
                                        type="range"
                                        min={0}
                                        max={100}
                                        value={Math.round((selectedBox.style.bgAlpha ?? 0.8) * 100)}
                                        onChange={(e) =>
                                            updateStyle({
                                                bgAlpha: Math.max(0, Math.min(1, Number(e.target.value) / 100)),
                                            })
                                        }
                                    />
                                </div>
                                <div>
                                    <label>Alignment</label>
                                    <select
                                        className="input"
                                        value={selectedBox.style.align}
                                        onChange={(e) => updateStyle({ align: e.target.value })}
                                    >
                                        <option value="left">Left</option>
                                        <option value="center">Center</option>
                                        <option value="right">Right</option>
                                    </select>
                                </div>
                            </div>

                            <div className="grid2">
                                <div>
                                    <label>Width (px)</label>
                                    <input
                                        type="number"
                                        className="input"
                                        min={80}
                                        value={selectedBox.w}
                                        onChange={(e) =>
                                            onResize(selectedBox.id, Number(e.target.value) - selectedBox.w, 0)
                                        }
                                    />
                                </div>
                                <div>
                                    <label>Height (px)</label>
                                    <input
                                        type="number"
                                        className="input"
                                        min={60}
                                        value={selectedBox.h}
                                        onChange={(e) =>
                                            onResize(selectedBox.id, 0, Number(e.target.value) - selectedBox.h)
                                        }
                                    />
                                </div>
                            </div>

                            <label className="row gap sm">
                                <input
                                    type="checkbox"
                                    checked={!!selectedBox.style.shadow}
                                    onChange={(e) => updateStyle({ shadow: e.target.checked })}
                                />
                                Drop shadow
                            </label>
                        </>
                    ) : (
                        <p>Select a text box to edit its styles.</p>
                    )}
                </aside>
            </div>
        </div>
    );
}

function clampN(v, min, max) {
    const n = Number(v);
    return isNaN(n) ? min : Math.max(min, Math.min(max, n));
}

function hexToRgb(hex) {
    if (!hex) return { r: 255, g: 255, b: 255 };
    if (hex.length === 4) {
        const r = parseInt(hex[1] + hex[1], 16);
        const g = parseInt(hex[2] + hex[2], 16);
        const b = parseInt(hex[3] + hex[3], 16);
        return { r, g, b };
    }
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return { r, g, b };
}

function colorToRgba(hexOrRgb, alpha = 0.8) {
    if (!hexOrRgb) return `rgba(255,255,255,${alpha})`;
    if (hexOrRgb.startsWith("rgba"))
        return hexOrRgb.replace(/rgba\(([^)]+)\)/, (m, inner) => {
            return `rgba(${inner.split(",").slice(0, 3).join(",")}, ${alpha})`;
        });
    if (hexOrRgb.startsWith("rgb"))
        return hexOrRgb.replace(/rgb\(([^)]+)\)/, (m, inner) => `rgba(${inner}, ${alpha})`);
    const base = hexOrRgb.slice(0, 7);
    const { r, g, b } = hexToRgb(base);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function edgeShadow(color = "#ffffff", width = 1) {
    const w = Math.max(1, Math.min(6, Number(width) || 1));
    return [
        `${w}px 0 ${color}`,
        `-${w}px 0 ${color}`,
        `0 ${w}px ${color}`,
        `0 -${w}px ${color}`,
        `${w}px ${w}px ${color}`,
        `-${w}px -${w}px ${color}`,
        `${w}px -${w}px ${color}`,
        `-${w}px ${w}px ${color}`,
    ].join(", ");
}

/** Draggable, resizable text box */
function DraggableBox({ box, selected, scaleX = 1, scaleY = 1, contentScale = 1, onSelect, onDrag, onResize, onTextChange }) {
    const ref = useRef(null);
    const dragState = useRef(null);

    // Keep latest callbacks
    const onDragRef = useRef(onDrag);
    const onSelectRef = useRef(onSelect);
    const onResizeRef = useRef(onResize);
    useEffect(() => {
        onDragRef.current = onDrag;
    }, [onDrag]);
    useEffect(() => {
        onSelectRef.current = onSelect;
    }, [onSelect]);
    useEffect(() => {
        onResizeRef.current = onResize;
    }, [onResize]);

    const pointer = (e) => ({ x: e.clientX, y: e.clientY });

    useEffect(() => {
        const el = ref.current;
        if (!el) return;

        function cleanupDrag(pointerId) {
            try {
                if (pointerId != null) {
                    el.releasePointerCapture?.(pointerId);
                }
            } catch { }
            window.removeEventListener("pointermove", move);
            window.removeEventListener("pointerup", up);
            window.removeEventListener("pointercancel", cancel);
            el.removeEventListener("lostpointercapture", lostCapture);
            dragState.current = null;
            el.classList.remove("dragging");
        }

        function down(e) {
            if (e.target.closest?.(".edge-handle, .corner-handle")) return;

            e.stopPropagation();
            e.preventDefault();
            onSelectRef.current?.();

            const p = pointer(e);
            dragState.current = { prevX: p.x, prevY: p.y, started: false };

            try {
                el.setPointerCapture?.(e.pointerId);
            } catch { }
            window.addEventListener("pointermove", move, { passive: false });
            window.addEventListener("pointerup", up, { once: true });
            window.addEventListener("pointercancel", cancel, { once: true });
            el.addEventListener("lostpointercapture", lostCapture, { once: true });
            el.classList.add("dragging");
        }

        function move(e) {
            const s = dragState.current;
            if (!s) return;
            e.preventDefault();

            const p = pointer(e);
            const dx = p.x - s.prevX;
            const dy = p.y - s.prevY;

            if (!s.started) {
                if (Math.abs(dx) + Math.abs(dy) < 3) return; // small threshold
                s.started = true;
            }

            onDragRef.current?.(box.id, dx, dy);
            s.prevX = p.x;
            s.prevY = p.y;
        }

        function up(e) {
            cleanupDrag(e.pointerId);
        }

        function cancel(e) {
            cleanupDrag(e.pointerId);
        }

        function lostCapture() {
            cleanupDrag();
        }

        el.addEventListener("pointerdown", down);
        return () => {
            el.removeEventListener("pointerdown", down);
            cleanupDrag();
        };
    }, [box.id]);

    // --- Resize support ---
    const rs = useRef(null);

    function startResize(e, anchor) {
        e.stopPropagation();
        e.preventDefault();
        onSelectRef.current?.();

        const p = pointer(e);
        rs.current = { prevX: p.x, prevY: p.y, anchor };

        try {
            e.currentTarget.setPointerCapture?.(e.pointerId);
        } catch { }
        window.addEventListener("pointermove", onResizeMove, { passive: false });
        window.addEventListener("pointerup", onResizeUp, { once: true });
        window.addEventListener("pointercancel", onResizeUp, { once: true });
    }

    function onResizeMove(e) {
        const s = rs.current;
        if (!s) return;
        e.preventDefault();
        const p = pointer(e);
        const dx = p.x - s.prevX;
        const dy = p.y - s.prevY;

        let dw = 0,
            dh = 0;
        if (s.anchor.includes("e")) dw = dx;
        if (s.anchor.includes("w")) dw = -dx;
        if (s.anchor.includes("s")) dh = dy;
        if (s.anchor.includes("n")) dh = -dy;

        onResizeRef.current?.(box.id, dw, dh, s.anchor);
        s.prevX = p.x;
        s.prevY = p.y;
    }

    function onResizeUp(e) {
        try {
            e.currentTarget?.releasePointerCapture?.(e.pointerId);
        } catch { }
        window.removeEventListener("pointermove", onResizeMove);
        window.removeEventListener("pointerup", onResizeUp);
        window.removeEventListener("pointercancel", onResizeUp);
        rs.current = null;
    }

    const style = {
        left: box.x * scaleX,
        top: box.y * scaleY,
        width: box.w * scaleX,
        height: box.h * scaleY,
        padding: box.style.padding * contentScale,
        borderRadius: box.style.radius * contentScale,
        background: colorToRgba(box.style.bg, box.style.bgAlpha ?? 0.8),
        boxShadow: box.style.shadow ? "0 6px 24px rgba(0,0,0,.18)" : "none",
        fontFamily: box.style.fontFamily,
        fontSize: box.style.fontSize * contentScale,
        fontWeight: box.style.fontWeight,
        lineHeight: box.style.lineHeight,
        color: box.style.color,
        textAlign: box.style.align,
        outline: selected ? "2px solid #6c8cff" : "1px solid rgba(0,0,0,.08)",
        position: "absolute",
        cursor: "grab",
        touchAction: "none",
        userSelect: "none",
    };

    const contentStyle = {
        textShadow: box?.style?.textEdge
            ? edgeShadow(box?.style?.textEdgeColor || "#ffffff", box?.style?.textEdgeWidth ?? 1)
            : "none",
    };

    return (
        <div
            className={`tbox ${selected ? "selected" : ""}`}
            style={style}
            ref={ref}
            onDoubleClick={onSelect}
        >
            {/* Resize edges */}
            <div className="edge-handle handle-n" onPointerDown={(e) => startResize(e, "n")} />
            <div className="edge-handle handle-s" onPointerDown={(e) => startResize(e, "s")} />
            <div className="edge-handle handle-e" onPointerDown={(e) => startResize(e, "e")} />
            <div className="edge-handle handle-w" onPointerDown={(e) => startResize(e, "w")} />

            {/* Resize corners */}
            <div className="corner-handle handle-nw" onPointerDown={(e) => startResize(e, "nw")} />
            <div className="corner-handle handle-ne" onPointerDown={(e) => startResize(e, "ne")} />
            <div className="corner-handle handle-sw" onPointerDown={(e) => startResize(e, "sw")} />
            <div className="corner-handle handle-se" onPointerDown={(e) => startResize(e, "se")} />

            <div className="content" style={contentStyle}>{box.text}</div>
        </div>
    );
}
