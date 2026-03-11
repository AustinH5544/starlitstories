import { useEffect, useRef, useState } from "react"
import "./PaintingFlightGame.css"

const clamp = (value, min, max) => Math.max(min, Math.min(max, value))
const circleIntersectsRect = (cx, cy, radius, rx, ry, rw, rh) => {
    const closestX = clamp(cx, rx, rx + rw)
    const closestY = clamp(cy, ry, ry + rh)
    const dx = cx - closestX
    const dy = cy - closestY
    return dx * dx + dy * dy <= radius * radius
}

const obstacleTypes = ["books", "shelf", "open-book", "ink"]

export default function PaintingFlightGame() {
    const containerRef = useRef(null)
    const canvasRef = useRef(null)
    const rafRef = useRef(0)
    const [score, setScore] = useState(0)
    const [bestScore, setBestScore] = useState(0)
    const gameRef = useRef({
        width: 760,
        height: 340,
        dpr: 1,
        lastTs: 0,
        brush: { x: 170, y: 170, vy: 0, hitboxRadius: 9, hitboxOffsetX: -2 },
        trail: [],
        sparkles: [],
        obstacles: [],
        spawnTimer: 0,
        hasStarted: false,
        gameOver: false,
        score: 0,
        bestScore: 0,
        hue: 0,
    })

    const syncScore = () => {
        const g = gameRef.current
        setScore(g.score)
        setBestScore(g.bestScore)
    }

    const resizeCanvas = () => {
        const canvas = canvasRef.current
        const container = containerRef.current
        if (!canvas || !container) return
        const rect = container.getBoundingClientRect()
        const dpr = Math.max(1, window.devicePixelRatio || 1)
        const width = Math.max(360, Math.floor(rect.width))
        const height = Math.max(240, Math.floor(rect.height))
        canvas.width = width * dpr
        canvas.height = height * dpr
        canvas.style.width = `${width}px`
        canvas.style.height = `${height}px`

        const g = gameRef.current
        const prevW = g.width
        const prevH = g.height
        g.width = width
        g.height = height
        g.dpr = dpr
        g.brush.x = Math.round(width * 0.24)
        g.brush.y = clamp((g.brush.y / prevH) * height, 35, height - 35)

        g.obstacles = g.obstacles.map((o) => ({
            ...o,
            x: (o.x / prevW) * width,
            width: Math.max(56, (o.width / prevW) * width),
            gapY: clamp((o.gapY / prevH) * height, 70, height - 70),
            gapSize: clamp((o.gapSize / prevH) * height, 112, 170),
        }))
    }

    const spawnSparkles = (x, y, intensity = 8) => {
        const g = gameRef.current
        for (let i = 0; i < intensity; i++) {
            const angle = Math.random() * Math.PI * 2
            const speed = 24 + Math.random() * 70
            g.sparkles.push({
                x,
                y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                life: 0.5 + Math.random() * 0.45,
                age: 0,
                size: 1.2 + Math.random() * 2.5,
                hue: 190 + Math.random() * 130,
            })
        }
    }

    const getBrushTipPosition = (brush) => {
        const angle = clamp(brush.vy / 500, -0.5, 0.55)
        const tipLocalX = 29.5
        const tipLocalY = 0
        const cosA = Math.cos(angle)
        const sinA = Math.sin(angle)
        return {
            x: brush.x + tipLocalX * cosA - tipLocalY * sinA,
            y: brush.y + tipLocalX * sinA + tipLocalY * cosA,
        }
    }

    const restart = (startImmediately = false) => {
        const g = gameRef.current
        g.brush = { ...g.brush, y: g.height * 0.5, vy: 0 }
        g.trail = []
        g.sparkles = []
        g.obstacles = []
        g.spawnTimer = 0
        g.hasStarted = startImmediately
        g.gameOver = false
        g.score = 0
        g.lastTs = 0
        syncScore()
    }

    const recoverRuntimeState = () => {
        const g = gameRef.current
        const minY = Math.max(10, g.brush.hitboxRadius + 1)
        const maxY = Math.max(minY, g.height - g.brush.hitboxRadius - 1)
        if (!Number.isFinite(g.brush.y)) g.brush.y = g.height * 0.5
        g.brush.y = clamp(g.brush.y, minY, maxY)
        if (!Number.isFinite(g.brush.vy)) g.brush.vy = 0
        g.trail = g.trail.filter((t) => Number.isFinite(t.x) && Number.isFinite(t.y) && Number.isFinite(t.vx))
        g.sparkles = g.sparkles.filter((s) => Number.isFinite(s.x) && Number.isFinite(s.y) && Number.isFinite(s.vx) && Number.isFinite(s.vy))
        g.obstacles = g.obstacles.filter((o) => Number.isFinite(o.x) && Number.isFinite(o.gapY) && Number.isFinite(o.gapSize) && Number.isFinite(o.width))
    }

    const flap = () => {
        const g = gameRef.current
        if (g.gameOver) {
            restart(true)
        } else if (!g.hasStarted) {
            g.hasStarted = true
        }
        g.brush.vy = -260
        const tip = getBrushTipPosition(g.brush)
        spawnSparkles(tip.x, tip.y, 11)
    }

    const spawnObstacle = () => {
        const g = gameRef.current
        const baseGap = clamp(118 - g.score * 0.55, 88, 124)
        const variance = 10 + Math.min(14, g.score * 0.25)
        const gapSize = clamp(baseGap + (Math.random() * 2 - 1) * variance, 82, 132)
        const margin = 52
        g.obstacles.push({
            x: g.width + 45,
            width: 74,
            gapY: margin + Math.random() * (g.height - margin * 2),
            gapSize,
            passed: false,
            type: obstacleTypes[Math.floor(Math.random() * obstacleTypes.length)],
        })
    }

    const collidesWithObstacle = (brush, obstacle, height) => {
        const hitboxX = brush.x + brush.hitboxOffsetX
        const hitboxY = brush.y
        const hitboxRadius = brush.hitboxRadius
        const gapTop = clamp(obstacle.gapY - obstacle.gapSize * 0.5, 0, height)
        const gapBottom = clamp(obstacle.gapY + obstacle.gapSize * 0.5, 0, height)
        return (
            circleIntersectsRect(hitboxX, hitboxY, hitboxRadius, obstacle.x, 0, obstacle.width, gapTop) ||
            circleIntersectsRect(hitboxX, hitboxY, hitboxRadius, obstacle.x, gapBottom, obstacle.width, height - gapBottom)
        )
    }

    const update = (dt) => {
        const g = gameRef.current
        if (g.gameOver || !g.hasStarted) return

        const gravity = 670
        const scrollSpeed = 164 + Math.min(52, g.score * 1.2)
        g.brush.vy += gravity * dt
        g.brush.y += g.brush.vy * dt
        g.hue = (g.hue + dt * 95) % 360
        const tip = getBrushTipPosition(g.brush)

        g.trail.push({
            x: tip.x + (Math.random() - 0.5) * 1.2,
            y: tip.y + (Math.random() - 0.5) * 1.8,
            vx: -(scrollSpeed * (0.62 + Math.random() * 0.28)),
            life: 0.72,
            age: 0,
            width: 5 + Math.random() * 2.4,
            hue: g.hue,
        })
        if (g.trail.length > 180) g.trail.splice(0, g.trail.length - 180)

        g.spawnTimer += dt
        if (g.spawnTimer >= 1.6) {
            g.spawnTimer = 0
            spawnObstacle()
        }

        g.obstacles.forEach((o) => {
            o.x -= scrollSpeed * dt
            if (!o.passed && o.x + o.width < g.brush.x) {
                o.passed = true
                g.score += 1
                g.bestScore = Math.max(g.bestScore, g.score)
                syncScore()
                spawnSparkles(g.brush.x + 18, g.brush.y, 6)
            }
        })
        g.obstacles = g.obstacles.filter((o) => o.x + o.width > -40)

        g.sparkles.forEach((s) => {
            s.age += dt
            s.x += s.vx * dt
            s.y += s.vy * dt
            s.vy += 120 * dt
        })
        g.sparkles = g.sparkles.filter((s) => s.age < s.life)

        g.trail.forEach((t) => {
            t.age += dt
            t.x += t.vx * dt
        })
        g.trail = g.trail.filter((t) => t.age < t.life)

        const hitboxTop = g.brush.y - g.brush.hitboxRadius
        const hitboxBottom = g.brush.y + g.brush.hitboxRadius
        const ceilingHit = hitboxTop <= 0
        const floorHit = hitboxBottom >= g.height
        const obstacleHit = g.obstacles.some((o) => collidesWithObstacle(g.brush, o, g.height))

        if (ceilingHit || floorHit || obstacleHit) {
            g.gameOver = true
            spawnSparkles(g.brush.x, g.brush.y, 18)
        }
    }

    const drawRoundedRect = (ctx, x, y, width, height, radius) => {
        const r = Math.min(radius, width / 2, height / 2)
        ctx.beginPath()
        ctx.moveTo(x + r, y)
        ctx.arcTo(x + width, y, x + width, y + height, r)
        ctx.arcTo(x + width, y + height, x, y + height, r)
        ctx.arcTo(x, y + height, x, y, r)
        ctx.arcTo(x, y, x + width, y, r)
        ctx.closePath()
    }

    const drawRectWithCornerRadii = (ctx, x, y, width, height, radii) => {
        if (width <= 0 || height <= 0) return
        const tl = Math.min(Math.max(0, radii.tl || 0), width / 2, height / 2)
        const tr = Math.min(Math.max(0, radii.tr || 0), width / 2, height / 2)
        const br = Math.min(Math.max(0, radii.br || 0), width / 2, height / 2)
        const bl = Math.min(Math.max(0, radii.bl || 0), width / 2, height / 2)
        ctx.beginPath()
        ctx.moveTo(x + tl, y)
        ctx.lineTo(x + width - tr, y)
        if (tr) ctx.arcTo(x + width, y, x + width, y + tr, tr)
        else ctx.lineTo(x + width, y)
        ctx.lineTo(x + width, y + height - br)
        if (br) ctx.arcTo(x + width, y + height, x + width - br, y + height, br)
        else ctx.lineTo(x + width, y + height)
        ctx.lineTo(x + bl, y + height)
        if (bl) ctx.arcTo(x, y + height, x, y + height - bl, bl)
        else ctx.lineTo(x, y + height)
        ctx.lineTo(x, y + tl)
        if (tl) ctx.arcTo(x, y, x + tl, y, tl)
        else ctx.lineTo(x, y)
        ctx.closePath()
    }

    const drawObstacle = (ctx, obstacle, h) => {
        const gapTop = clamp(obstacle.gapY - obstacle.gapSize * 0.5, 0, h)
        const gapBottom = clamp(obstacle.gapY + obstacle.gapSize * 0.5, 0, h)
        const x = obstacle.x
        const w = obstacle.width

        const drawPair = (fillA, fillB) => {
            drawRectWithCornerRadii(ctx, x, 0, w, gapTop, { tl: 0, tr: 0, br: 12, bl: 12 })
            ctx.fillStyle = fillA
            ctx.fill()
            drawRectWithCornerRadii(ctx, x, gapBottom, w, h - gapBottom, { tl: 12, tr: 12, br: 0, bl: 0 })
            ctx.fillStyle = fillB
            ctx.fill()
        }

        if (obstacle.type === "books") {
            drawPair("#714fcb", "#5f46b9")
            ctx.strokeStyle = "rgba(255,255,255,0.4)"
            ctx.lineWidth = 1.5
            for (let y = 18; y < gapTop - 12; y += 24) {
                ctx.beginPath()
                ctx.moveTo(x + 8, y)
                ctx.lineTo(x + w - 8, y)
                ctx.stroke()
            }
            for (let y = gapBottom + 18; y < h - 12; y += 24) {
                ctx.beginPath()
                ctx.moveTo(x + 8, y)
                ctx.lineTo(x + w - 8, y)
                ctx.stroke()
            }
            return
        }

        if (obstacle.type === "shelf") {
            drawPair("#334155", "#3f4f66")
            ctx.fillStyle = "#f59e0b"
            for (let y = 12; y < gapTop - 15; y += 26) {
                drawRoundedRect(ctx, x + 9, y, w - 18, 14, 4)
                ctx.fill()
            }
            for (let y = gapBottom + 12; y < h - 15; y += 26) {
                drawRoundedRect(ctx, x + 9, y, w - 18, 14, 4)
                ctx.fill()
            }
            return
        }

        if (obstacle.type === "open-book") {
            drawPair("#f4d38c", "#ebbf65")
            ctx.fillStyle = "rgba(79,46,14,0.35)"
            ctx.fillRect(x + w / 2 - 2, 0, 4, gapTop)
            ctx.fillRect(x + w / 2 - 2, gapBottom, 4, h - gapBottom)
            return
        }

        drawPair("#0f766e", "#0a5f58")
        ctx.fillStyle = "rgba(255,255,255,0.8)"
        for (let i = 0; i < 5; i++) {
            const yA = 18 + i * 26
            if (yA < gapTop - 8) {
                ctx.beginPath()
                ctx.arc(x + w * 0.5, yA, 5.5, 0, Math.PI * 2)
                ctx.fill()
            }
            const yB = gapBottom + 18 + i * 26
            if (yB < h - 8) {
                ctx.beginPath()
                ctx.arc(x + w * 0.5, yB, 5.5, 0, Math.PI * 2)
                ctx.fill()
            }
        }
    }

    const draw = () => {
        const canvas = canvasRef.current
        if (!canvas) return
        const ctx = canvas.getContext("2d")
        if (!ctx) return
        const g = gameRef.current
        const { width, height, dpr } = g
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0)

        const bg = ctx.createLinearGradient(0, 0, 0, height)
        bg.addColorStop(0, "#20144a")
        bg.addColorStop(1, "#0f2c52")
        ctx.fillStyle = bg
        ctx.fillRect(0, 0, width, height)

        for (let i = 0; i < 24; i++) {
            const x = ((i * 137) % width) + ((g.lastTs * 0.018 * (i % 4 + 1)) % width)
            const y = (i * 53) % height
            const r = 0.7 + (i % 3) * 0.5
            ctx.fillStyle = `rgba(255,255,255,${0.18 + (i % 5) * 0.08})`
            ctx.beginPath()
            ctx.arc(x % width, y, r, 0, Math.PI * 2)
            ctx.fill()
        }

        for (let i = 1; i < g.trail.length; i++) {
            const a = g.trail[i - 1]
            const b = g.trail[i]
            const alphaA = 1 - a.age / a.life
            const alphaB = 1 - b.age / b.life
            const alpha = Math.max(0, Math.min(1, (alphaA + alphaB) * 0.5))
            if (alpha <= 0) continue
            ctx.strokeStyle = `hsla(${b.hue}, 92%, 62%, ${alpha * 0.65})`
            ctx.lineCap = "round"
            ctx.lineJoin = "round"
            ctx.lineWidth = Math.max(1.2, (a.width + b.width) * 0.5 * alpha)
            ctx.beginPath()
            ctx.moveTo(a.x, a.y)
            ctx.lineTo(b.x, b.y)
            ctx.stroke()
        }

        g.obstacles.forEach((o) => drawObstacle(ctx, o, height))

        g.sparkles.forEach((s) => {
            const alpha = 1 - s.age / s.life
            ctx.fillStyle = `hsla(${s.hue}, 100%, 70%, ${alpha})`
            ctx.beginPath()
            ctx.arc(s.x, s.y, s.size, 0, Math.PI * 2)
            ctx.fill()
        })

        const b = g.brush
        ctx.save()
        ctx.translate(b.x, b.y)
        ctx.rotate(clamp(b.vy / 500, -0.5, 0.55))
        // Wooden handle
        const handleGrad = ctx.createLinearGradient(-30, 0, 8, 0)
        handleGrad.addColorStop(0, "#7c4a24")
        handleGrad.addColorStop(0.55, "#b8793e")
        handleGrad.addColorStop(1, "#d79a58")
        ctx.fillStyle = handleGrad
        drawRoundedRect(ctx, -30, -5, 40, 10, 5)
        ctx.fill()
        ctx.fillStyle = "rgba(255,255,255,0.2)"
        drawRoundedRect(ctx, -26, -3.3, 28, 2.2, 1.1)
        ctx.fill()

        // Ferrule (metal connector)
        const ferruleGrad = ctx.createLinearGradient(8, -7, 16, 7)
        ferruleGrad.addColorStop(0, "#d1d5db")
        ferruleGrad.addColorStop(0.5, "#f3f4f6")
        ferruleGrad.addColorStop(1, "#9ca3af")
        ctx.fillStyle = ferruleGrad
        drawRoundedRect(ctx, 8, -7, 8, 14, 2)
        ctx.fill()
        ctx.strokeStyle = "rgba(17,24,39,0.28)"
        ctx.lineWidth = 1
        ctx.beginPath()
        ctx.moveTo(11, -6)
        ctx.lineTo(11, 6)
        ctx.moveTo(13.8, -6)
        ctx.lineTo(13.8, 6)
        ctx.stroke()

        // Bristle base
        ctx.fillStyle = "#6b3f1f"
        ctx.beginPath()
        ctx.moveTo(16, -6)
        ctx.lineTo(24, -3.8)
        ctx.lineTo(24, 3.8)
        ctx.lineTo(16, 6)
        ctx.closePath()
        ctx.fill()

        // Bristle tip with paint tint
        ctx.fillStyle = `hsla(${(g.hue + 18) % 360}, 92%, 58%, 0.95)`
        ctx.beginPath()
        ctx.moveTo(24, -3.8)
        ctx.lineTo(29.5, 0)
        ctx.lineTo(24, 3.8)
        ctx.closePath()
        ctx.fill()
        ctx.restore()

        if (!g.hasStarted) {
            ctx.fillStyle = "rgba(3,8,25,0.52)"
            ctx.fillRect(0, 0, width, height)
            ctx.fillStyle = "#ffffff"
            ctx.font = "700 30px 'Trebuchet MS', 'Comic Sans MS', sans-serif"
            ctx.textAlign = "center"
            ctx.fillText("Press Space to Start", width / 2, height / 2 - 4)
            ctx.font = "500 15px 'Trebuchet MS', sans-serif"
            ctx.fillStyle = "rgba(255,255,255,0.92)"
            ctx.fillText("Tap or click also works.", width / 2, height / 2 + 24)
            return
        }

        if (g.gameOver) {
            ctx.fillStyle = "rgba(3,8,25,0.55)"
            ctx.fillRect(0, 0, width, height)
            ctx.fillStyle = "#ffffff"
            ctx.font = "700 26px 'Trebuchet MS', 'Comic Sans MS', sans-serif"
            ctx.textAlign = "center"
            ctx.fillText("Splash! Tap to Fly Again", width / 2, height / 2 - 4)
            ctx.font = "500 15px 'Trebuchet MS', sans-serif"
            ctx.fillStyle = "rgba(255,255,255,0.92)"
            ctx.fillText("Your story is still being crafted in the background.", width / 2, height / 2 + 24)
        }
    }

    useEffect(() => {
        resizeCanvas()
        restart(false)

        const canvas = canvasRef.current
        const onResize = () => resizeCanvas()
        const onKeyDown = (event) => {
            if (event.code === "Space" || event.code === "ArrowUp") {
                event.preventDefault()
                flap()
            }
        }
        const onVisibilityChange = () => {
            if (document.visibilityState === "visible") {
                gameRef.current.lastTs = 0
            }
        }
        const onContextLost = (event) => {
            event.preventDefault()
            gameRef.current.lastTs = 0
        }
        const onContextRestored = () => {
            resizeCanvas()
            gameRef.current.lastTs = 0
            recoverRuntimeState()
        }

        window.addEventListener("resize", onResize)
        window.addEventListener("keydown", onKeyDown)
        document.addEventListener("visibilitychange", onVisibilityChange)
        canvas?.addEventListener?.("contextlost", onContextLost)
        canvas?.addEventListener?.("contextrestored", onContextRestored)

        const tick = (ts) => {
            const g = gameRef.current
            try {
                if (!g.lastTs) g.lastTs = ts
                const dt = clamp((ts - g.lastTs) / 1000, 0, 0.033)
                g.lastTs = ts

                if (!Number.isFinite(g.brush.y) || !Number.isFinite(g.brush.vy)) recoverRuntimeState()
                update(dt)

                draw()
            } catch (error) {
                console.error("PaintingFlightGame recovered from frame error:", error)
                g.lastTs = 0
                recoverRuntimeState()
            }
            rafRef.current = requestAnimationFrame(tick)
        }

        rafRef.current = requestAnimationFrame(tick)
        return () => {
            cancelAnimationFrame(rafRef.current)
            window.removeEventListener("resize", onResize)
            window.removeEventListener("keydown", onKeyDown)
            document.removeEventListener("visibilitychange", onVisibilityChange)
            canvas?.removeEventListener?.("contextlost", onContextLost)
            canvas?.removeEventListener?.("contextrestored", onContextRestored)
        }
    }, [])

    return (
        <div className="painting-flight-game">
            <div className="painting-flight-header">
                <p className="painting-flight-title">Painting Flight</p>
                <p className="painting-flight-score">Score: {score} | Best: {bestScore}</p>
            </div>
            <div
                ref={containerRef}
                className="painting-flight-canvas-wrap"
                onPointerDown={flap}
                role="button"
                tabIndex={0}
                onKeyDown={(event) => {
                    if (event.key === " " || event.key === "Enter") {
                        event.preventDefault()
                        flap()
                    }
                }}
                aria-label="Painting flight game area"
            >
                <canvas ref={canvasRef} className="painting-flight-canvas" />
            </div>
            <p className="painting-flight-help">Press Space to start. Then tap, click, or press Space/Up to float the magical paintbrush through storybook obstacles.</p>
        </div>
    )
}
