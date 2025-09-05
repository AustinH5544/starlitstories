"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

/**
 * StoryCustomizePage
 *
 * A paid-members-only editor that lets users arrange page text like a real picture book:
 *  - Drag text boxes anywhere over the illustration
 *  - Resize text boxes via handles
 *  - Style controls (font, size, color, background, alignment)
 *  - Per‑page layouts with quick copy/paste layout between pages
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

    // Load story from navigation state or localStorage (mirrors StoryViewerPage behavior)
    useEffect(() => {
        let s = state?.story;
        if (!s) {
            const saved = localStorage.getItem("story");
            if (saved) s = JSON.parse(saved);
        }
        if (s && Array.isArray(s.pages)) {
            setStory(s);
            // seed default layout from story text if not present
            const key = storageKey(s);
            const savedLayouts = JSON.parse(localStorage.getItem(key) || "null");
            if (savedLayouts) setBoxesByPage(savedLayouts);
            else {
                const init = {};
                s.pages.forEach((p, i) => {
                    init[i] = [createDefaultTextBox(p?.text || "", i)];
                });
                setBoxesByPage(init);
            }
        }
    }, [state]);

    // Persist on change
    useEffect(() => {
        if (!story) return;
        localStorage.setItem(storageKey(story), JSON.stringify(boxesByPage));
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

    function createDefaultTextBox(text, page) {
        return {
            id: crypto.randomUUID(),
            page,
            x: 24,
            y: 24,
            w: 420,
            h: 160,
            text,
            style: {
                fontFamily: "Georgia, 'Times New Roman', serif",
                fontSize: 20,
                fontWeight: 400,
                lineHeight: 1.35,
                color: "#1b1b1b",
                bg: "#ffffff",
                bgAlpha: 0.8,
                align: "left",
                padding: 16,
                radius: 12,
                shadow: true,
            },
        };
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
                let x = nextX, y = nextY;
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
                // keep inside image area if possible
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

    function onResize(id, dw, dh) {
        setBoxesByPage((prev) => {
            const stage = stageRef.current;
            const bounds = stage ? stage.getBoundingClientRect() : null;
            const next = { ...prev };
            next[pageIndex] = (next[pageIndex] || []).map((b) => {
                if (b.id !== id) return b;
                let w = Math.max(120, b.w + dw);
                let h = Math.max(80, b.h + dh);
                if (bounds) {
                    w = Math.min(w, bounds.width - b.x - 2);
                    h = Math.min(h, bounds.height - b.y - 2);
                }
                return { ...b, w, h };
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
            // deep-ish clone and reassign ids
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
                next[i] = (next[i] || []).map((b) => ({ ...b, style: { ...b.style, ...selectedBox.style } }));
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
                    <button className="btn" onClick={() => navigate("/create")}>Create a Story</button>
                </div>
            </div>
        );
    }

    const page = story.pages[pageIndex];

    return (
        <div className="customizer">
            <style>{styles}</style>

            {/* Top bar */}
            <header className="topbar">
                <button className="btn" onClick={() => navigate(-1)}>← Back</button>
                <div className="title">Customize: <span>{story.title || "Untitled"}</span></div>
                <div className="actions">
                    <button className="btn" onClick={addTextBox}>➕ Add Text Box</button>
                    <button className="btn" disabled={selectedBox == null} onClick={() => duplicateBox(selectedBox?.id)}>⧉ Duplicate</button>
                    <button className="btn danger" disabled={selectedBox == null} onClick={() => removeBox(selectedBox?.id)}>🗑 Remove</button>
                </div>
            </header>

            {/* Main layout */}
            <div className="layout">
                {/* Left: page picker */}
                <aside className="sidebar">
                    <div className="pages">
                        <button
                            className={`thumb ${pageIndex === -1 ? "active" : ""}`}
                            onClick={() => setPageIndex(-1)}
                            title="Cover"
                        >📖 Cover</button>
                        {story.pages.map((_, i) => (
                            <button key={i} className={`thumb ${i === pageIndex ? "active" : ""}`} onClick={() => setPageIndex(i)}>
                                {i + 1}
                            </button>
                        ))}
                    </div>
                    <div className="copybar">
                        <label>Copy layout from:</label>
                        <select onChange={(e) => copyLayoutFrom(Number(e.target.value))} value="">
                            <option value="" disabled>Choose page…</option>
                            {story.pages.map((_, i) => (
                                <option key={i} value={i}>Page {i + 1}</option>
                            ))}
                        </select>
                        <button className="btn ghost" disabled={!selectedBox} onClick={applySelectedStyleToAllPages}>Apply selected style → all pages</button>
                    </div>
                </aside>

                {/* Center: stage */}
                <main className="stage-wrap">
                    <div className="stage" ref={stageRef}>
                        <img src={page?.imageUrl || story.coverImageUrl || "/placeholder.svg"} alt="Page" className="page-img" />
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

                {/* Right: inspector */}
                <aside className="inspector">
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
                                                bgAlpha: Math.max(
                                                    0,
                                                    Math.min(1, Number(e.target.value) / 100)
                                                ),
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

function rgbToHex(v) {
    if (!v) return "#ffffff";
    if (v.startsWith("#")) return v.slice(0, 7);
    const m = v.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
    if (!m) return "#ffffff";
    const r = Number(m[1]).toString(16).padStart(2, "0");
    const g = Number(m[2]).toString(16).padStart(2, "0");
    const b = Number(m[3]).toString(16).padStart(2, "0");
    return `#${r}${g}${b}`;
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
    if (hexOrRgb.startsWith("rgba")) return hexOrRgb.replace(/rgba\(([^)]+)\)/, (m, inner) => `rgba(${inner.split(',').slice(0, 3).join(',')}, ${alpha})`);
    if (hexOrRgb.startsWith("rgb")) return hexOrRgb.replace(/rgb\(([^)]+)\)/, (m, inner) => `rgba(${inner}, ${alpha})`);
    const base = hexOrRgb.slice(0, 7);
    const { r, g, b } = hexToRgb(base);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

/** Draggable, resizable text box */
function DraggableBox({ box, selected, onSelect, onDrag, onResize, onTextChange }) {
    const ref = useRef(null);
    const handleRef = useRef(null);
    const dragState = useRef(null);

    // Always point to the latest callbacks without retriggering the effect
    const onDragRef = useRef(onDrag);
    const onSelectRef = useRef(onSelect);
    useEffect(() => { onDragRef.current = onDrag; }, [onDrag]);
    useEffect(() => { onSelectRef.current = onSelect; }, [onSelect]);

    useEffect(() => {
        const handle = handleRef.current;
        if (!handle) return;

        function pointer(e) { return { x: e.clientX, y: e.clientY }; }

        function down(e) {
            e.stopPropagation();
            e.preventDefault();
            onSelectRef.current?.();

            const p = pointer(e);
            dragState.current = { prevX: p.x, prevY: p.y };

            try { handle.setPointerCapture?.(e.pointerId); } catch { }
            window.addEventListener("pointermove", move, { passive: false });
            window.addEventListener("pointerup", up, { once: true });
        }

        function move(e) {
            const s = dragState.current; if (!s) return;
            e.preventDefault();
            const p = { x: e.clientX, y: e.clientY };
            const dx = p.x - s.prevX;
            const dy = p.y - s.prevY;
            onDragRef.current?.(box.id, dx, dy);
            s.prevX = p.x; s.prevY = p.y;
        }

        function up(e) {
            dragState.current = null;
            try { handle.releasePointerCapture?.(e.pointerId); } catch { }
            window.removeEventListener("pointermove", move);
        }

        handle.addEventListener("pointerdown", down);
        return () => {
            handle.removeEventListener("pointerdown", down);
            window.removeEventListener("pointermove", move);
        };
    }, [box.id]);

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
    };

    return (
        <div className={`tbox ${selected ? "selected" : ""}`} style={style} ref={ref} onDoubleClick={onSelect}>
              <button className="drag-handle" ref={handleRef} aria-label="Drag text box" />
              <div className="content">{box.text}</div>
            </div>
    );
}

const styles = `
:root {
  --bg: #0b1020;
  --surface: #0e152a;
  --text: #e7ecf1;
  --muted: #9aa6b2;
  --primary: #6c8cff;
  --border: #23314f;
}
* { box-sizing: border-box; }
.customizer { padding-top: var(--navbar-height, 64px); min-height: 100vh; background: radial-gradient(1200px 800px at 10% -10%, #1a2652 0%, transparent 55%),
             radial-gradient(900px 700px at 110% 10%, #361f59 0%, transparent 40%), var(--bg); color: var(--text); }
.topbar { position: sticky; top: var(--navbar-height, 64px); height: 56px; display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 12px; padding: 8px 16px; border-bottom: 1px solid var(--border); background: linear-gradient(0deg, rgba(14,21,42,.8), rgba(14,21,42,.8)); backdrop-filter: blur(8px); }
.topbar .title { text-align: center; font-weight: 600; }
.topbar .actions { justify-self: end; display: flex; gap: 8px; }
.btn { background: #1a2344; color: #dbe6ff; border: 1px solid var(--border); padding: 8px 12px; border-radius: 10px; cursor: pointer; }
.btn:hover { border-color: #3a4f8f; }
.btn.primary { background: var(--primary); color: #0b0f1c; border-color: #6c8cff; }
.btn.ghost { background: transparent; }
.btn.danger { background: #3a1f2a; border-color: #5a2a3a; color: #ffd7df; }
.layout { display: grid; grid-template-columns: 200px 1fr 320px; gap: 16px; padding: 16px; }
.sidebar { display: grid; grid-template-rows: auto 1fr; gap: 16px; }
.pages { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; align-content: start; }
.thumb { aspect-ratio: 1 / 1; background: #121a35; border: 1px solid var(--border); border-radius: 10px; color: #b6c5ff; cursor: pointer; }
.thumb.active { outline: 2px solid var(--primary); }
.copybar { display: grid; gap: 8px; padding: 12px; border: 1px dashed var(--border); border-radius: 12px; background: rgba(20,30,60,.35); }
.stage-wrap { display: grid; place-items: center; }
.stage { touch-action: none; position: relative; width: min(840px, 90vw); aspect-ratio: 4/3; background: #0b0f1c; border-radius: 16px; overflow: hidden; border: 1px solid var(--border); }
.page-img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; filter: saturate(1.02) contrast(1.02); }
.tbox { user-select: none; }
.tbox .content {
  width: 100%;
  height: 100%;
  overflow: auto;
  white-space: pre-wrap;
  pointer-events: auto;
  -webkit-user-select: none;
  user-select: none;
}

.tbox .drag-handle {
  position: absolute;
  top: 8px;
  left: 8px;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: white;
  mix-blend-mode: difference;
  cursor: grab;
  z-index: 3;
}
.tbox .drag-handle:active { cursor: grabbing; }
.tbox.selected .drag-handle { outline: 1px dashed rgba(108,140,255,.35); outline-offset: 2px; }
.tbox.selected .drag-bar { outline: 1px dashed rgba(108,140,255,.5); }
.tbox .handle { position: absolute; right: -10px; bottom: -10px; width: 20px; height: 20px; border-radius: 50%; border: 2px solid #6c8cff; background: #0b0f1c; box-shadow: 0 2px 10px rgba(0,0,0,.4); cursor: nwse-resize; }
.inspector { padding: 8px 12px; border: 1px solid var(--border); border-radius: 12px; background: rgba(14,21,42,.6); height: fit-content; position: sticky; top: 72px; }
.inspector .input, .inspector select, .inspector textarea { width: 100%; background: #0f1630; color: #dbe6ff; border: 1px solid var(--border); border-radius: 10px; padding: 8px; }
.grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-block: 8px; }
.row.gap { display: flex; gap: 8px; }
.row.gap.sm { gap: 6px; align-items: center; }
.pane.empty { max-width: 680px; margin: 96px auto; background: rgba(14,21,42,.6); padding: 24px; border-radius: 16px; border: 1px solid var(--border); text-align: center; }
.tip { color: var(--muted); font-size: 12px; }
`;
