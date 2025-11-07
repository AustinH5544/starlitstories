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
    const [currentColor, setCurrentColor] = useState("#000000")

    const colors = [
        { name: "Black", value: "#000000" },
        { name: "Blue", value: "#3B82F6" },
        { name: "Purple", value: "#A855F7" },
        { name: "Pink", value: "#EC4899" },
        { name: "Red", value: "#EF4444" },
        { name: "Orange", value: "#F97316" },
        { name: "Green", value: "#10B981" },
        { name: "Teal", value: "#14B8A6" },
    ]

    const setupSize = () => {
        const canvas = canvasRef.current
        const container = containerRef.current
        if (!canvas || !container) return

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
        ctx.lineCap = "round"
        ctx.lineJoin = "round"
        ctx.lineWidth = lineWidth
        ctx.strokeStyle = currentColor

        if (background !== "transparent") {
            ctx.fillStyle = background
            ctx.fillRect(0, 0, cssW, cssH)
            ctx.fillStyle = currentColor
        }

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
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [lineWidth, strokeStyle, background, currentColor])

    const getPos = (nativeEvent) => {
        const canvas = canvasRef.current
        const rect = canvas.getBoundingClientRect()
        return { x: nativeEvent.clientX - rect.left, y: nativeEvent.clientY - rect.top }
    }

    const begin = (e) => {
        e.preventDefault()
        const canvas = canvasRef.current
        canvas.setPointerCapture?.(e.pointerId)
        drawingRef.current = true
        lastPointRef.current = getPos(e.nativeEvent)
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
        if (ctxRef.current) {
            ctxRef.current.strokeStyle = newColor
        }
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

    return (
        <div className={className} aria-label={ariaLabel}>
            <div style={{ fontSize: 14, opacity: 0.7, fontWeight: 500, marginBottom: "12px" }}>
                Doodle while we create your story…
            </div>

            <div style={headerStyle}>
                <select
                    value={currentColor}
                    onChange={(e) => changeColor(e.target.value)}
                    style={dropdownStyle}
                    aria-label="Select pen color"
                    onMouseEnter={(e) => {
                        e.currentTarget.style.borderColor = "rgba(0,0,0,0.2)"
                        e.currentTarget.style.boxShadow = "0 2px 6px rgba(0,0,0,0.15)"
                    }}
                    onMouseLeave={(e) => {
                        e.currentTarget.style.borderColor = "rgba(0,0,0,0.1)"
                        e.currentTarget.style.boxShadow = "0 1px 3px rgba(0,0,0,0.1)"
                    }}
                >
                    {colors.map((color) => (
                        <option key={color.value} value={color.value}>
                            {color.name}
                        </option>
                    ))}
                </select>
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