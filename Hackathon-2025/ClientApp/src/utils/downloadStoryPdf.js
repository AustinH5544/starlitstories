import html2canvas from "html2canvas";
import { jsPDF } from "jspdf";

/** Must match the key used in StoryCustomizePage */
function storageKey(story) {
    return `layout:${story.id ?? story.title ?? "untitled"}`; // same as editor
}

/** Convert hex/rgb + alpha to rgba() like your editor does */
function colorToRgba(hexOrRgb, alpha = 0.8) {
    if (!hexOrRgb) return `rgba(255,255,255,${alpha})`;
    if (hexOrRgb.startsWith("rgba"))
        return hexOrRgb.replace(/rgba\(([^)]+)\)/, (m, inner) =>
            `rgba(${inner.split(",").slice(0, 3).join(",")}, ${alpha})`
        );
    if (hexOrRgb.startsWith("rgb"))
        return hexOrRgb.replace(/rgb\(([^)]+)\)/, (m, inner) => `rgba(${inner}, ${alpha})`);
    const hex = hexOrRgb.slice(0, 7);
    const to = (h) => parseInt(h, 16);
    const r = hex.length === 4 ? to(hex[1] + hex[1]) : to(hex.slice(1, 3));
    const g = hex.length === 4 ? to(hex[2] + hex[2]) : to(hex.slice(3, 5));
    const b = hex.length === 4 ? to(hex[3] + hex[3]) : to(hex.slice(5, 7));
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

/** Wait for an <img> to load (with crossOrigin) */
function loadImage(url) {
    return new Promise((resolve, reject) => {
        const img = new Image();
        img.crossOrigin = "anonymous"; // must have Azure Blob CORS set (see below)
        img.referrerPolicy = "no-referrer";
        img.onload = () => resolve(img);
        img.onerror = () => reject(new Error(`Image failed to load (CORS or 404): ${url}`));
        img.src = url || "/placeholder.svg";
    });
}

/** Slugify filename */
function slug(s) {
    return (s || "story")
        .toString()
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "");
}

/**
 * Render one page as DOM (image background + absolutely positioned text boxes)
 * and snapshot to canvas for a pixel-perfect PDF page.
 */
async function renderPageCanvas({ page, boxes = [], stageW, stageH }) {
    // Offscreen container
    const host = document.createElement("div");
    host.style.position = "fixed";
    host.style.left = "-10000px";
    host.style.top = "0";
    host.style.width = stageW + "px";
    host.style.height = stageH + "px";
    host.style.background = "#ffffff"; // PDF white
    host.style.isolation = "isolate";

    // Background image sized to cover width while preserving aspect
    const bgImg = await loadImage(page?.imageUrl || "/placeholder.svg");
    const ratio = bgImg.naturalWidth / bgImg.naturalHeight;
    const targetW = stageW;
    const targetH = Math.round(targetW / ratio);
    const imgEl = document.createElement("img");
    imgEl.src = bgImg.src;
    imgEl.style.position = "absolute";
    imgEl.style.left = "0";
    imgEl.style.top = "0";
    imgEl.style.width = `${targetW}px`;
    imgEl.style.height = `${targetH}px`;
    imgEl.style.objectFit = "cover";
    imgEl.style.userSelect = "none";
    imgEl.crossOrigin = "anonymous";
    host.appendChild(imgEl);

    // Add each text box exactly like the editor
    for (const b of boxes) {
        const box = document.createElement("div");
        box.style.position = "absolute";
        box.style.left = b.x + "px";
        box.style.top = b.y + "px";
        box.style.width = b.w + "px";
        box.style.height = b.h + "px";
        box.style.padding = (b.style.padding ?? 16) + "px";
        box.style.borderRadius = (b.style.radius ?? 12) + "px";
        box.style.background = colorToRgba(b.style.bg || "#ffffff", b.style.bgAlpha ?? 0.8);
        box.style.boxShadow = b.style.shadow ? "0 6px 24px rgba(0,0,0,.18)" : "none";
        box.style.fontFamily = b.style.fontFamily || "Georgia, 'Times New Roman', serif";
        box.style.fontSize = (b.style.fontSize ?? 20) + "px";
        box.style.fontWeight = (b.style.fontWeight ?? 400).toString();
        box.style.lineHeight = (b.style.lineHeight ?? 1.35).toString();
        box.style.color = b.style.color || "#1b1b1b";
        box.style.textAlign = b.style.align || "left";
        box.style.whiteSpace = "pre-wrap";
        box.style.overflow = "hidden";
        box.textContent = b.text ?? "";
        host.appendChild(box);
    }

    document.body.appendChild(host);

    // Ensure fonts are ready before snapshot
    if (document.fonts && document.fonts.ready) {
        try { await document.fonts.ready; } catch { }
    }

    // Snapshot
    const canvas = await html2canvas(host, {
        backgroundColor: "#ffffff",
        scale: 2,          // nice sharpness
        useCORS: true,     // allow crossOrigin images (CORS must be enabled in Azure)
        allowTaint: false,
        imageTimeout: 15000,
        logging: false,
    });

    // Cleanup
    document.body.removeChild(host);
    return canvas;
}

/** Public: build & download the PDF using your saved layout */
export async function downloadStoryPdf(story) {
    if (!story || !Array.isArray(story.pages) || story.pages.length === 0) {
      throw new Error("This story has no pages to export.");
    }

    const key = storageKey(story);
    const layouts = JSON.parse(localStorage.getItem(key) || "{}");
    const meta = JSON.parse(localStorage.getItem(`${key}:meta`) || "null");

    // Fallback: if no meta, assume 1024px wide stage and scale by image aspect
    const defaultStageW = 1024;
    const pages = story.pages || [];
    const stageW = meta?.stageW || defaultStageW;

    const canvases = [];
    const coverBoxesStored =
        JSON.parse(localStorage.getItem(`${key}:cover`) || "null") ??
        (Array.isArray(layouts["-1"]) ? layouts["-1"] : null);

    if (story.coverImageUrl) {
        let coverStageH = meta?.coverStageH;
        if (!coverStageH) {
            try {
                const img = await loadImage(story.coverImageUrl);
                coverStageH = Math.round(stageW * (img.naturalHeight / img.naturalWidth));
            } catch {
                coverStageH = Math.round(stageW * 0.75);
            }
        }

        const defaultCoverBoxes = story.title
            ? [{
                x: Math.round(stageW * 0.08),
                y: Math.round(coverStageH * 0.06),
                w: Math.round(stageW * 0.84),
                h: Math.round(coverStageH * 0.16),
                text: story.title,
                style: {
                    fontFamily: "Georgia, 'Times New Roman', serif",
                    fontSize: 36, fontWeight: 700, lineHeight: 1.2,
                    color: "#1b1b1b", bg: "#ffffff", bgAlpha: 0.0,
                    align: "center", padding: 8, radius: 12, shadow: false
                }
            }]
            : [];

        const coverBoxes = Array.isArray(coverBoxesStored) ? coverBoxesStored : defaultCoverBoxes;

        try {
            const coverCanvas = await renderPageCanvas({
                page: { imageUrl: story.coverImageUrl },
                boxes: coverBoxes,
                stageW,
                stageH: coverStageH
            });
            canvases.push(coverCanvas);
        } catch (e) {
            console.warn("[PDF] Skipping cover due to error:", e?.message || e);
        }
    }
    for (let i = 0; i < pages.length; i++) {
        const page = pages[i];
        // If editor saved stageH, use it; else infer from image aspect
        let stageH = meta?.stageH;
        if (!stageH) {
            try {
                const img = await loadImage(page?.imageUrl || "/placeholder.svg");
                stageH = Math.round(stageW * (img.naturalHeight / img.naturalWidth));
            } catch {
                stageH = Math.round(stageW * 0.75);
            }
        }

        const boxes = Array.isArray(layouts[i]) ? layouts[i] : [
            // conservative fallback: one default box with the page text
            {
                x: 24, y: 24, w: Math.round(stageW * 0.6), h: 160,
                text: page?.text ?? "",
                style: {
                    fontFamily: "Georgia, 'Times New Roman', serif",
                    fontSize: 20, fontWeight: 400, lineHeight: 1.35,
                    color: "#1b1b1b", bg: "#ffffff", bgAlpha: 0.8,
                    align: "left", padding: 16, radius: 12, shadow: true,
                }
            }
        ];

        let canvas;
        try {
              canvas = await renderPageCanvas({ page, boxes, stageW, stageH });
        } catch (e) {
          // Surface which page / URL caused it
              console.error("[PDF] Failed on page", i + 1, page?.imageUrl, e);
          // Common case: tainted canvas (CORS)
              const msg = (e?.message || "").toLowerCase();
          if (msg.includes("tainted") || msg.includes("securityerror")) {
                throw new Error("Images are blocked by CORS. Enable CORS on your Azure Blob for your site origin and try again.");
              }
          throw e;
        }
        canvases.push(canvas);
    }

    // Build PDF: pick orientation per page aspect; we’ll normalize to first page
    const first = canvases[0];
    const w = first.width;
    const h = first.height;
    const isLandscape = w > h;
    const doc = new jsPDF({
        orientation: isLandscape ? "landscape" : "portrait",
        unit: "px",
        format: [w, h],   // match canvas size for pixel-perfect output
        compress: true,
        putOnlyUsedFonts: true,
    });

    canvases.forEach((cv, idx) => {
        if (idx > 0) doc.addPage([cv.width, cv.height], cv.width > cv.height ? "landscape" : "portrait");
        const img = cv.toDataURL("image/jpeg", 0.95);
        doc.addImage(img, "JPEG", 0, 0, cv.width, cv.height);
    });

    doc.save(`${slug(story.title)}.pdf`);
}
