"use client"

import { useEffect, useRef, useState } from "react"

export default function DoodlePad({
    className = "",
    height = 280,
    lineWidth = 4,
    strokeStyle = "#222",
    background = "transparent",
    ariaLabel = "Doodle pad",
}) {
    const containerRef = useRef(null)
    const canvasRef = useRef(null)
    const ctxRef = useRef(null)
    const drawingRef = useRef(false)
    const lastPointRef = useRef(null)
    const [isReady, setIsReady] = useState(false)
    const [currentColor, setCurrentColor] = useState(strokeStyle)
    const [brushSize, setBrushSize] = useState(lineWidth)
    const [brushType, setBrushType] = useState("pen")
    const [canUndo, setCanUndo] = useState(false)
    const imageDataRef = useRef(null)
    const historyRef = useRef([])

    const MAX_HISTORY = 50

    const colors = [
        { name: "Black", value: "#000000" },
        { name: "Gray", value: "#6B7280" },
        { name: "Slate", value: "#334155" },
        { name: "White", value: "#FFFFFF" },
        { name: "Blue", value: "#3B82F6" },
        { name: "Navy", value: "#1D4ED8" },
        { name: "Sky", value: "#0EA5E9" },
        { name: "Purple", value: "#A855F7" },
        { name: "Violet", value: "#8B5CF6" },
        { name: "Pink", value: "#EC4899" },
        { name: "Rose", value: "#F43F5E" },
        { name: "Red", value: "#EF4444" },
        { name: "Orange", value: "#F97316" },
        { name: "Amber", value: "#F59E0B" },
        { name: "Yellow", value: "#EAB308" },
        { name: "Green", value: "#10B981" },
        { name: "Emerald", value: "#059669" },
        { name: "Lime", value: "#84CC16" },
        { name: "Teal", value: "#14B8A6" },
        { name: "Cyan", value: "#06B6D4" },
    ]

    const brushTypes = [
        { name: "Pen", value: "pen" },
        { name: "Marker", value: "marker" },
        { name: "Eraser", value: "eraser" },
    ]

    const applyBrushSettings = (ctx) => {
        if (!ctx) return

        const isEraser = brushType === "eraser"
        const isMarker = brushType === "marker"
        ctx.globalCompositeOperation = isEraser ? "destination-out" : "source-over"
        ctx.globalAlpha = isMarker ? 0.45 : 1
        ctx.lineCap = "round"
        ctx.lineJoin = "round"
        ctx.lineWidth = brushSize
        ctx.strokeStyle = currentColor
    }

    const pushSnapshot = () => {
        const ctx = ctxRef.current
        const canvas = canvasRef.current
        if (!ctx || !canvas) return
        const snapshot = ctx.getImageData(0, 0, canvas.width, canvas.height)
        historyRef.current.push(snapshot)
        if (historyRef.current.length > MAX_HISTORY) historyRef.current.shift()
        setCanUndo(historyRef.current.length > 0)
    }

    const undo = () => {
        const ctx = ctxRef.current
        const canvas = canvasRef.current
        if (!ctx || !canvas || historyRef.current.length === 0) return
        const previous = historyRef.current.pop()
        ctx.putImageData(previous, 0, 0)
        setCanUndo(historyRef.current.length > 0)
    }

    const setupSize = () => {
        const canvas = canvasRef.current
        const container = containerRef.current
        if (!canvas || !container) return

        if (ctxRef.current) {
            imageDataRef.current = ctxRef.current.getImageData(0, 0, canvas.width, canvas.height)
        }

        const rect = container.getBoundingClientRect()
        const cssW = Math.max(1, Math.floor(rect.width))
        const cssH = Math.max(1, Math.floor(rect.height))
        const dpr = Math.max(1, window.devicePixelRatio || 1)

        canvas.width = cssW * dpr
        canvas.height = cssH * dpr
        canvas.style.width = `${cssW}px`
        canvas.style.height = `${cssH}px`

        const ctx = canvas.getContext("2d")
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0)

        if (background !== "transparent") {
            ctx.fillStyle = background
            ctx.fillRect(0, 0, cssW, cssH)
            ctx.fillStyle = currentColor
        }

        if (imageDataRef.current) {
            ctx.putImageData(imageDataRef.current, 0, 0)
        }

        applyBrushSettings(ctx)
        ctxRef.current = ctx
    }

    useEffect(() => {
        setupSize()
        setIsReady(true)

        const ro = new ResizeObserver(() => setupSize())
        if (containerRef.current) ro.observe(containerRef.current)

        const media = window.matchMedia?.(`(resolution: ${window.devicePixelRatio}dppx)`)
        const onChangeDPR = () => setupSize()
        media?.addEventListener?.("change", onChangeDPR)

        return () => {
            ro.disconnect()
            media?.removeEventListener?.("change", onChangeDPR)
        }
    }, [lineWidth, strokeStyle, background, brushSize, brushType, currentColor])

    useEffect(() => {
        setCurrentColor(strokeStyle)
    }, [strokeStyle])

    useEffect(() => {
        setBrushSize(lineWidth)
    }, [lineWidth])

    useEffect(() => {
        if (!ctxRef.current) return
        applyBrushSettings(ctxRef.current)
    }, [brushSize, brushType, currentColor])

    useEffect(() => {
        const onUndoShortcut = (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
                e.preventDefault()
                undo()
            }
        }
        window.addEventListener("keydown", onUndoShortcut)
        return () => window.removeEventListener("keydown", onUndoShortcut)
    }, [])

    const getPos = (nativeEvent) => {
        const canvas = canvasRef.current
        const rect = canvas.getBoundingClientRect()
        return { x: nativeEvent.clientX - rect.left, y: nativeEvent.clientY - rect.top }
    }

    const begin = (e) => {
        e.preventDefault()
        const canvas = canvasRef.current
        canvas.setPointerCapture?.(e.pointerId)
        pushSnapshot()
        drawingRef.current = true
        const startPoint = getPos(e.nativeEvent)
        lastPointRef.current = startPoint

        const ctx = ctxRef.current
        if (ctx) {
            ctx.beginPath()
            ctx.arc(startPoint.x, startPoint.y, Math.max(1, brushSize / 2), 0, Math.PI * 2)
            ctx.fillStyle = brushType === "eraser" ? "#000000" : currentColor
            ctx.fill()
        }
    }

    const move = (e) => {
        if (!drawingRef.current || !ctxRef.current) return
        e.preventDefault()
        const ctx = ctxRef.current
        const p = getPos(e.nativeEvent)
        const last = lastPointRef.current || p

        ctx.beginPath()
        ctx.moveTo(last.x, last.y)
        ctx.lineTo(p.x, p.y)
        ctx.stroke()

        lastPointRef.current = p
    }

    const end = (e) => {
        const canvas = canvasRef.current
        try {
            canvas.releasePointerCapture?.(e.pointerId)
        } catch { }
        drawingRef.current = false
        lastPointRef.current = null
    }

    const clear = () => {
        if (!ctxRef.current || !canvasRef.current) return
        pushSnapshot()
        const ctx = ctxRef.current
        const rect = canvasRef.current.getBoundingClientRect()
        if (background === "transparent") {
            ctx.clearRect(0, 0, rect.width, rect.height)
        } else {
            ctx.fillStyle = background
            ctx.fillRect(0, 0, rect.width, rect.height)
            ctx.fillStyle = currentColor
        }
    }

    const changeColor = (newColor) => {
        setCurrentColor(newColor)
    }

    const changeBrushSize = (newSize) => {
        setBrushSize(Number(newSize))
    }

    const changeBrushType = (newBrushType) => {
        setBrushType(newBrushType)
    }

    const containerStyle = {
        position: "relative",
        width: "100%",
        height: `${height}px`,
        border: "1px solid rgba(0,0,0,0.1)",
        borderRadius: "16px",
        overflow: "hidden",
        background: background === "transparent" ? "#fff" : background,
        boxShadow: "0 2px 8px rgba(0,0,0,0.08)",
    }

    const headerStyle = {
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        marginBottom: "12px",
        gap: "12px",
    }

    const clearBtnStyle = {
        border: "none",
        borderRadius: "8px",
        padding: "8px 16px",
        fontWeight: 600,
        background: "linear-gradient(135deg, #ef4444 0%, #dc2626 100%)",
        color: "white",
        cursor: "pointer",
        fontSize: "13px",
        transition: "all 0.2s ease",
        boxShadow: "0 2px 4px rgba(239, 68, 68, 0.2)",
    }

    const dropdownStyle = {
        padding: "8px 12px",
        borderRadius: "8px",
        border: "2px solid rgba(0,0,0,0.1)",
        background: "white",
        fontSize: "13px",
        fontWeight: 500,
        cursor: "pointer",
        transition: "all 0.2s ease",
        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
        outline: "none",
    }

    const colorGridStyle = {
        display: "grid",
        gridTemplateColumns: "repeat(10, minmax(0, 1fr))",
        gap: "6px",
        flex: 1,
        minWidth: "260px",
        maxWidth: "420px",
    }

    const colorSwatchBaseStyle = {
        width: "100%",
        aspectRatio: "1 / 1",
        borderRadius: "8px",
        border: "2px solid rgba(0,0,0,0.15)",
        cursor: "pointer",
        padding: 0,
    }

    const actionBtnStyle = {
        ...clearBtnStyle,
        background: "linear-gradient(135deg, #0ea5e9 0%, #0284c7 100%)",
        boxShadow: "0 2px 4px rgba(14, 165, 233, 0.2)",
    }

    return (
        <div className={className} aria-label={ariaLabel}>
            <div style={{ fontSize: 14, opacity: 0.7, fontWeight: 500, marginBottom: "12px" }}>
                Doodle while we create your story!
            </div>

            <div style={headerStyle}>
                <div style={colorGridStyle} aria-label="Select pen color">
                    {colors.map((color) => {
                        const isActive = color.value === currentColor
                        return (
                            <button
                                key={color.value}
                                type="button"
                                onClick={() => changeColor(color.value)}
                                title={color.name}
                                aria-label={color.name}
                                aria-pressed={isActive}
                                style={{
                                    ...colorSwatchBaseStyle,
                                    background: color.value,
                                    borderColor: isActive ? "#111827" : "rgba(0,0,0,0.15)",
                                    boxShadow: isActive ? "0 0 0 2px rgba(17,24,39,0.25)" : "none",
                                }}
                            />
                        )
                    })}
                </div>
                <select
                    value={brushSize}
                    onChange={(e) => changeBrushSize(e.target.value)}
                    style={dropdownStyle}
                    aria-label="Select brush size"
                >
                    <option value={2}>Thin</option>
                    <option value={4}>Small</option>
                    <option value={6}>Medium</option>
                    <option value={10}>Large</option>
                    <option value={14}>XL</option>
                </select>
                <select
                    value={brushType}
                    onChange={(e) => changeBrushType(e.target.value)}
                    style={dropdownStyle}
                    aria-label="Select brush type"
                >
                    {brushTypes.map((brush) => (
                        <option key={brush.value} value={brush.value}>
                            {brush.name}
                        </option>
                    ))}
                </select>
                <button
                    type="button"
                    style={{
                        ...actionBtnStyle,
                        opacity: canUndo ? 1 : 0.55,
                        cursor: canUndo ? "pointer" : "not-allowed",
                    }}
                    onClick={undo}
                    disabled={!canUndo}
                    aria-label="Undo last stroke"
                >
                    Undo
                </button>
                <button
                    type="button"
                    style={clearBtnStyle}
                    onClick={clear}
                    aria-label="Clear drawing"
                    onMouseEnter={(e) => {
                        e.currentTarget.style.transform = "translateY(-1px)"
                        e.currentTarget.style.boxShadow = "0 4px 8px rgba(239, 68, 68, 0.3)"
                    }}
                    onMouseLeave={(e) => {
                        e.currentTarget.style.transform = "translateY(0)"
                        e.currentTarget.style.boxShadow = "0 2px 4px rgba(239, 68, 68, 0.2)"
                    }}
                >
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
    )
}
