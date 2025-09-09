"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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

    const [story, setStory] = useState(null);
    const [pageIndex, setPageIndex] = useState(0);
    const [boxesByPage, setBoxesByPage] = useState({});
    const [selectedBoxId, setSelectedBoxId] = useState(null);
    const stageRef = useRef(null);

    // Mobile drawers state: 'pages' | 'inspector' | null
    const [panel, setPanel] = useState(null);
    const openPages = () => setPanel("pages");
    const openInspector = () => setPanel("inspector");
    const closePanels = () => setPanel(null);

    // stable helper
    function createDefaultTextBox(text, page) {
        return {
            id: crypto.randomUUID(),
            page,
            x: 24, y: 24, w: 420, h: 160,
            text,
            style: {
                fontFamily: "Georgia, 'Times New Roman', serif",
                fontSize: 20, fontWeight: 400, lineHeight: 1.35,
                color: "#1b1b1b", bg: "#ffffff", bgAlpha: 0.8,
                align: "left", padding: 16, radius: 12, shadow: true,
            },
        };
    }

    function seedFromStory(s) {
        const init = {};
        (s?.pages || []).forEach((p, i) => {
            init[i] = [createDefaultTextBox(p?.text || "", i)];
        });
        return init;
    }

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
                            next[i] = [createDefaultTextBox(s.pages[i].text, i)];
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
                        next[i] = [createDefaultTextBox(s.pages[i].text, i)];
                    }
                }
                return next;
            }

            const normalized = normalizeLayouts(savedLayouts, s);
            setBoxesByPage(normalized || seedFromStory(s));
            if (normalized) localStorage.setItem(key, JSON.stringify(normalized));
        }
    }, [state]);

    // Persist on change
    useEffect(() => {
        if (!story) return;
        const key = storageKey(story);
        localStorage.setItem(key, JSON.stringify(boxesByPage));

        // store stage size so export can scale coordinates 1:1
        const rect = stageRef.current?.getBoundingClientRect();
        if (rect) {
            localStorage.setItem(`${key}:meta`, JSON.stringify({
                stageW: Math.round(rect.width),
                stageH: Math.round(rect.height),
            }));
        }
    }, [story, boxesByPage]);

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
        setBoxesByPage((prev) => {
            const next = { ...prev };
            const copy = [...(next[pageIndex] || [])];
            copy.push(createDefaultTextBox("New text…", pageIndex));
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
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;
                let x = nextX,
                    y = nextY;
                if (bounds) {
                    const maxX = bounds.width - b.w - 2;
                    const maxY = bounds.height - b.h - 2;
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
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;
                let x = b.x + dx;
                let y = b.y + dy;
                if (bounds) {
                    const maxX = bounds.width - b.w - 2;
                    const maxY = bounds.height - b.h - 2;
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

            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;

                let x = b.x,
                    y = b.y,
                    w = b.w,
                    h = b.h;
                const minW = 120,
                    minH = 80;

                const right = b.x + b.w;
                const bottom = b.y + b.h;

                if (anchor.includes("e")) {
                    w = w + dw;
                }
                if (anchor.includes("w")) {
                    x = b.x - dw;
                    w = right - x;
                }
                if (anchor.includes("s")) {
                    h = h + dh;
                }
                if (anchor.includes("n")) {
                    y = b.y - dh;
                    h = bottom - y;
                }

                w = Math.max(minW, w);
                h = Math.max(minH, h);

                if (bounds) {
                    const pad = 2;
                    const maxX = bounds.width - minW - pad;
                    const maxY = bounds.height - minH - pad;

                    x = Math.max(pad, Math.min(x, maxX));
                    y = Math.max(pad, Math.min(y, maxY));

                    w = Math.min(w, bounds.width - x - pad);
                    h = Math.min(h, bounds.height - y - pad);
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

    function applySelectedStyleToAllPages() {
        if (!selectedBox) return;
        setBoxesByPage((prev) => {
            const next = { ...prev };
            Object.keys(next).forEach((k) => {
                const i = Number(k);
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

    return (
        <div className="customizer">
            {/* Top bar */}
            <header className="topbar">
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
                    Customize: <span>{story.title || "Untitled"}</span>
                </div>

                <div className="actions">
                    {/* Mobile: open inspector drawer */}
                    <button
                        className="btn show-on-mobile"
                        onClick={openInspector}
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
                        onClick={() => setBoxesByPage(seedFromStory(story))}
                        title="Reset layout to default text positions"
                    >
                        Reset layout
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
                        <select onChange={(e) => copyLayoutFrom(Number(e.target.value))} value="">
                            <option value="" disabled>
                                Choose page…
                            </option>
                            {story.pages.map((_, i) => (
                                <option key={i} value={i}>
                                    Page {i + 1}
                                </option>
                            ))}
                        </select>
                        <button
                            className="btn ghost"
                            disabled={!selectedBox}
                            onClick={applySelectedStyleToAllPages}
                        >
                            Apply selected style → all pages
                        </button>
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

                            <div className="grid2">
                                <div>
                                    <label>Font size</label>
                                    <input
                                        type="number"
                                        className="input"
                                        min={12}
                                        max={64}
                                        value={selectedBox.style.fontSize}
                                        onChange={(e) =>
                                            updateStyle({ fontSize: clampN(e.target.value, 12, 64) })
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

/** Draggable, resizable text box */
function DraggableBox({ box, selected, onSelect, onDrag, onResize, onTextChange }) {
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
            try {
                el.releasePointerCapture?.(e.pointerId);
            } catch { }
            window.removeEventListener("pointermove", move);
            dragState.current = null;
            el.classList.remove("dragging");
        }

        el.addEventListener("pointerdown", down);
        return () => {
            el.removeEventListener("pointerdown", down);
            window.removeEventListener("pointermove", move);
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
        rs.current = null;
    }

    const style = {
        left: box.x,
        top: box.y,
        width: box.w,
        height: box.h,
        padding: box.style.padding,
        borderRadius: box.style.radius,
        background: colorToRgba(box.style.bg, box.style.bgAlpha ?? 0.8),
        boxShadow: box.style.shadow ? "0 6px 24px rgba(0,0,0,.18)" : "none",
        fontFamily: box.style.fontFamily,
        fontSize: box.style.fontSize,
        fontWeight: box.style.fontWeight,
        lineHeight: box.style.lineHeight,
        color: box.style.color,
        textAlign: box.style.align,
        outline: selected ? "2px solid #6c8cff" : "1px solid rgba(0,0,0,.08)",
        position: "absolute",
        cursor: "grab",
        userSelect: "none",
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

            <div className="content">{box.text}</div>
        </div>
    );
}
