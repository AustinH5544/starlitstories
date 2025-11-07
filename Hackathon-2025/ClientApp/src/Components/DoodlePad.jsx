import { useEffect, useRef, useState } from "react";

export default function DoodlePad({
    className = "",
    height = 280,
    lineWidth = 4,
    strokeStyle = "#222",
    background = "transparent",
    ariaLabel = "Doodle pad",
}) {
    const containerRef = useRef(null);
    const canvasRef = useRef(null);
    const ctxRef = useRef(null);
    const drawingRef = useRef(false);
    const lastPointRef = useRef(null);
    const [isReady, setIsReady] = useState(false);

    const setupSize = () => {
        const canvas = canvasRef.current;
        const container = containerRef.current;
        if (!canvas || !container) return;

        const rect = container.getBoundingClientRect();
        const cssW = Math.max(1, Math.floor(rect.width));
        const cssH = Math.max(1, Math.floor(rect.height));
        const dpr = Math.max(1, window.devicePixelRatio || 1);

        canvas.width = cssW * dpr;
        canvas.height = cssH * dpr;
        canvas.style.width = `${cssW}px`;
        canvas.style.height = `${cssH}px`;

        const ctx = canvas.getContext("2d");
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.lineWidth = lineWidth;
        ctx.strokeStyle = strokeStyle;

        if (background !== "transparent") {
            ctx.fillStyle = background;
            ctx.fillRect(0, 0, cssW, cssH);
            ctx.fillStyle = strokeStyle;
        }

        ctxRef.current = ctx;
    };

    useEffect(() => {
        setupSize();
        setIsReady(true);

        const ro = new ResizeObserver(() => setupSize());
        if (containerRef.current) ro.observe(containerRef.current);

        const media = window.matchMedia?.(`(resolution: ${window.devicePixelRatio}dppx)`);
        const onChangeDPR = () => setupSize();
        media?.addEventListener?.("change", onChangeDPR);

        return () => {
            ro.disconnect();
            media?.removeEventListener?.("change", onChangeDPR);
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [lineWidth, strokeStyle, background]);

    const getPos = (nativeEvent) => {
        const canvas = canvasRef.current;
        const rect = canvas.getBoundingClientRect();
        return { x: nativeEvent.clientX - rect.left, y: nativeEvent.clientY - rect.top };
    };

    const begin = (e) => {
        e.preventDefault();
        const canvas = canvasRef.current;
        canvas.setPointerCapture?.(e.pointerId);
        drawingRef.current = true;
        lastPointRef.current = getPos(e.nativeEvent);
    };

    const move = (e) => {
        if (!drawingRef.current || !ctxRef.current) return;
        e.preventDefault();
        const ctx = ctxRef.current;
        const p = getPos(e.nativeEvent);
        const last = lastPointRef.current || p;

        ctx.beginPath();
        ctx.moveTo(last.x, last.y);
        ctx.lineTo(p.x, p.y);
        ctx.stroke();

        lastPointRef.current = p;
    };

    const end = (e) => {
        const canvas = canvasRef.current;
        try { canvas.releasePointerCapture?.(e.pointerId); } catch { }
        drawingRef.current = false;
        lastPointRef.current = null;
    };

    const clear = () => {
        if (!ctxRef.current || !canvasRef.current) return;
        const ctx = ctxRef.current;
        const rect = canvasRef.current.getBoundingClientRect();
        if (background === "transparent") {
            ctx.clearRect(0, 0, rect.width, rect.height);
        } else {
            ctx.fillStyle = background;
            ctx.fillRect(0, 0, rect.width, rect.height);
            ctx.fillStyle = strokeStyle;
        }
    };

    const containerStyle = {
        position: "relative",
        width: "100%",
        height: `${height}px`,
        border: "1px solid rgba(0,0,0,0.15)",
        borderRadius: "16px",
        overflow: "hidden",
        background: background === "transparent" ? "#fff" : background, // ✅ fixed
    };

    const headerStyle = {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        marginBottom: "8px",
    };

    const clearBtnStyle = {
        border: "1px solid rgba(0,0,0,0.2)",
        borderRadius: "12px",
        padding: "6px 12px",
        fontWeight: 600,
        background: "transparent",
        cursor: "pointer",
    };

    return (
        <div className={className} aria-label={ariaLabel}>
            <div style={headerStyle}>
                <div style={{ fontSize: 14, opacity: 0.7 }}>doodle while we create your story…</div>
                <button type="button" style={clearBtnStyle} onClick={clear} aria-label="Clear drawing">
                    Clear
                </button>
            </div>

            <div ref={containerRef} style={containerStyle}>
                <canvas
                    ref={canvasRef}
                    role="img"
                    aria-label={ariaLabel}
                    style={{
                        display: "block",
                        width: "100%",
                        height: "100%",
                        cursor: "crosshair",
                        touchAction: "none",
                    }}
                    onPointerDown={begin}
                    onPointerMove={move}
                    onPointerUp={end}
                    onPointerCancel={end}
                    onPointerLeave={end}
                />
                {!isReady && (
                    <div
                        style={{
                            position: "absolute",
                            inset: 0,
                            display: "grid",
                            placeItems: "center",
                            fontSize: 14,
                            opacity: 0.7,
                            background: "rgba(255,255,255,0.5)",
                            backdropFilter: "blur(2px)",
                        }}
                    >
                        loading…
                    </div>
                )}
            </div>
        </div>
    );
}