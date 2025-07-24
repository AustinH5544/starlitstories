"use client"

import { useState, useEffect } from "react"
import { useLocation, useNavigate } from "react-router-dom"
import "./StoryViewerPage.css"
import { useAuth } from "../context/AuthContext"

const StoryViewerPage = () => {
    const { state } = useLocation()
    const navigate = useNavigate()

    const [story, setStory] = useState(null)
    const [currentPage, setCurrentPage] = useState(-1) // -1 = cover
    const [isReading, setIsReading] = useState(false)
    const [showControls, setShowControls] = useState(true)
    const [enlargedImage, setEnlargedImage] = useState(null)
    const [showCompletion, setShowCompletion] = useState(false)

    const { user } = useAuth()
    const [showShareModal, setShowShareModal] = useState(false)
    const [shareUrl, setShareUrl] = useState("")
    const [downloadFormat, setDownloadFormat] = useState("pdf") // pdf, images

    // Load story from state or localStorage
    useEffect(() => {
        if (state?.story) {
            setStory(state.story)
            localStorage.setItem("story", JSON.stringify(state.story))
        } else {
            const savedStory = localStorage.getItem("story")
            if (savedStory) {
                setStory(JSON.parse(savedStory))
            }
        }
    }, [state])

    // Auto-hide controls after 10 seconds of inactivity (only when reading)
    useEffect(() => {
        if (!isReading || !showControls) return

        const timer = setTimeout(() => {
            setShowControls(false)
        }, 10000)

        return () => clearTimeout(timer)
    }, [isReading, showControls])

    const nextPage = () => {
        if (story && currentPage < story.pages.length - 1) {
            setCurrentPage(currentPage + 1)
            setShowControls(true)
        } else if (currentPage === story.pages.length - 1) {
            // They're trying to go past the last page, show completion
            setShowCompletion(true)
        }
    }

    const prevPage = () => {
        if (currentPage > -1) {
            setCurrentPage(currentPage - 1)
            setShowControls(true)
            setShowCompletion(false) // Hide completion if going back
        }
    }

    const startReading = () => {
        setCurrentPage(0)
        setIsReading(true)
        setShowControls(true)
        setShowCompletion(false)
    }

    const goToPage = (pageIndex) => {
        setCurrentPage(pageIndex)
        setShowControls(true)
        setShowCompletion(false)
    }

    const finishStory = () => {
        setShowCompletion(true)
    }

    const readAgain = () => {
        setCurrentPage(-1)
        setIsReading(false)
        setShowCompletion(false)
        setShowControls(true)
    }

    const enlargeImage = (imageUrl, e) => {
        e.stopPropagation() // Prevent triggering the content click handler
        setEnlargedImage(imageUrl)
    }

    const closeEnlargedImage = () => {
        setEnlargedImage(null)
    }

    // Only toggle controls when clicking in the main content area
    const handleContentClick = (e) => {
        // Don't toggle if clicking on buttons or controls
        if (
            e.target.closest(".story-header") ||
            e.target.closest(".story-navigation") ||
            e.target.closest(".completion-overlay") ||
            e.target.closest("button") ||
            e.target.closest(".page-image-container")
        ) {
            return
        }

        // Only toggle controls when actually reading (not on cover)
        if (isReading && currentPage >= 0) {
            setShowControls(!showControls)
        }
    }

    const canDownload = user?.membership === "pro" || user?.membership === "premium"

    const handleDownload = async () => {
        if (!canDownload) {
            alert("Download feature is available for Pro and Premium users only!")
            return
        }

        try {
            if (downloadFormat === "pdf") {
                await downloadAsPDF()
            } else {
                await downloadAsImages()
            }
        } catch (error) {
            console.error("Download failed:", error)
            alert("Download failed. Please try again.")
        }
    }

    const downloadAsPDF = async () => {
        // Import jsPDF dynamically to avoid bundle size issues
        const { jsPDF } = await import("jspdf")
        const pdf = new jsPDF()

        // Add title page
        pdf.setFontSize(24)
        pdf.text(story.title, 20, 30)
        pdf.setFontSize(12)
        pdf.text(`Created on ${new Date(story.createdAt).toLocaleDateString()}`, 20, 50)

        // Add cover image if available
        if (story.coverImageUrl && !story.coverImageUrl.includes("placeholder")) {
            try {
                const coverImg = await loadImageAsBase64(story.coverImageUrl)
                pdf.addImage(coverImg, "PNG", 20, 70, 170, 120)
            } catch (err) {
                console.warn("Could not load cover image for PDF")
            }
        }

        // Add story pages
        for (let i = 0; i < story.pages.length; i++) {
            pdf.addPage()
            const page = story.pages[i]

            // Add page image
            if (page.imageUrl && !page.imageUrl.includes("placeholder")) {
                try {
                    const pageImg = await loadImageAsBase64(page.imageUrl)
                    pdf.addImage(pageImg, "PNG", 20, 20, 170, 120)
                } catch (err) {
                    console.warn(`Could not load image for page ${i + 1}`)
                }
            }

            // Add page text
            pdf.setFontSize(12)
            const splitText = pdf.splitTextToSize(page.text, 170)
            pdf.text(splitText, 20, 160)

            // Add page number
            pdf.setFontSize(10)
            pdf.text(`Page ${i + 1}`, 180, 280)
        }

        pdf.save(`${story.title}.pdf`)
    }

    const downloadAsImages = async () => {
        const zip = await import("jszip")
        const JSZip = zip.default
        const zipFile = new JSZip()

        // Add cover image
        if (story.coverImageUrl && !story.coverImageUrl.includes("placeholder")) {
            try {
                const coverBlob = await fetch(story.coverImageUrl).then((r) => r.blob())
                zipFile.file("00-cover.png", coverBlob)
            } catch (err) {
                console.warn("Could not download cover image")
            }
        }

        // Add page images
        for (let i = 0; i < story.pages.length; i++) {
            const page = story.pages[i]
            if (page.imageUrl && !page.imageUrl.includes("placeholder")) {
                try {
                    const pageBlob = await fetch(page.imageUrl).then((r) => r.blob())
                    zipFile.file(`${String(i + 1).padStart(2, "0")}-page-${i + 1}.png`, pageBlob)
                } catch (err) {
                    console.warn(`Could not download image for page ${i + 1}`)
                }
            }
        }

        // Add story text file
        const storyText = `${story.title}\n\n${story.pages.map((page, i) => `Page ${i + 1}:\n${page.text}`).join("\n\n")}`
        zipFile.file("story-text.txt", storyText)

        const content = await zipFile.generateAsync({ type: "blob" })
        const url = window.URL.createObjectURL(content)
        const a = document.createElement("a")
        a.href = url
        a.download = `${story.title}-images.zip`
        a.click()
        window.URL.revokeObjectURL(url)
    }

    const loadImageAsBase64 = (url) => {
        return new Promise((resolve, reject) => {
            const img = new Image()
            img.crossOrigin = "anonymous"
            img.onload = () => {
                const canvas = document.createElement("canvas")
                canvas.width = img.width
                canvas.height = img.height
                const ctx = canvas.getContext("2d")
                ctx.drawImage(img, 0, 0)
                resolve(canvas.toDataURL("image/png"))
            }
            img.onerror = reject
            img.src = url
        })
    }

    const handleShare = () => {
        // Generate a shareable URL (in a real app, you'd create a public story link)
        const currentUrl = window.location.origin + window.location.pathname
        setShareUrl(currentUrl)
        setShowShareModal(true)
    }

    const copyToClipboard = async (text) => {
        try {
            await navigator.clipboard.writeText(text)
            alert("Link copied to clipboard!")
        } catch (err) {
            // Fallback for older browsers
            const textArea = document.createElement("textarea")
            textArea.value = text
            document.body.appendChild(textArea)
            textArea.select()
            document.execCommand("copy")
            document.body.removeChild(textArea)
            alert("Link copied to clipboard!")
        }
    }

    const shareViaEmail = () => {
        const subject = encodeURIComponent(`Check out this magical story: ${story.title}`)
        const body = encodeURIComponent(
            `I created this personalized story on CozyPages and wanted to share it with you!\n\nStory: ${story.title}\nView it here: ${shareUrl}\n\nCreate your own magical stories at CozyPages!`,
        )
        window.open(`mailto:?subject=${subject}&body=${body}`)
    }

    const shareViaSocial = (platform) => {
        const text = encodeURIComponent(`Check out this magical story I created: ${story.title}`)
        const url = encodeURIComponent(shareUrl)

        switch (platform) {
            case "twitter":
                window.open(`https://twitter.com/intent/tweet?text=${text}&url=${url}`)
                break
            case "facebook":
                window.open(`https://www.facebook.com/sharer/sharer.php?u=${url}`)
                break
            case "whatsapp":
                window.open(`https://wa.me/?text=${text}%20${url}`)
                break
        }
    }

    if (!story || !Array.isArray(story.pages)) {
        return (
            <div className="story-viewer">
                <div className="stars"></div>
                <div className="twinkling"></div>
                <div className="clouds"></div>

                <div className="error-container">
                    <div className="error-content">
                        <div className="error-icon">📚</div>
                        <h2>Oops! No story found.</h2>
                        <p>It looks like your magical story got lost in the clouds!</p>
                        <button onClick={() => navigate("/create")} className="create-new-btn">
                            <span className="button-icon">✨</span>
                            Create New Story
                        </button>
                        <button onClick={() => navigate("/profile")} className="back-to-profile-btn">
                            <span className="button-icon">👤</span>
                            Back to Profile
                        </button>
                    </div>
                </div>
            </div>
        )
    }

    const isCover = currentPage === -1
    const page = story.pages[currentPage]
    const isLastPage = currentPage === story.pages.length - 1

    return (
        <div className="story-viewer" onClick={handleContentClick}>
            <div className="stars"></div>
            <div className="twinkling"></div>
            <div className="clouds"></div>

            {/* Header Controls */}
            <div className={`story-header ${showControls ? "visible" : "hidden"}`}>
                <div className="header-left">
                    <button onClick={() => navigate("/profile")} className="back-button">
                        <span>←</span> Back to Profile
                    </button>
                </div>
                <div className="story-progress">
                    <span className="progress-text">
                        {isCover ? "Cover" : `Page ${currentPage + 1} of ${story.pages.length}`}
                    </span>
                    <div className="progress-bar">
                        <div
                            className="progress-fill"
                            style={{
                                width: isCover ? "0%" : `${((currentPage + 1) / story.pages.length) * 100}%`,
                            }}
                        ></div>
                    </div>
                </div>
                <div className="header-right">
                    <button onClick={handleShare} className="share-button">
                        <span className="button-icon">📤</span>
                        <span className="button-text">Share</span>
                    </button>
                    {canDownload && (
                        <div className="download-dropdown">
                            <button onClick={handleDownload} className="download-button">
                                <span className="button-icon">📥</span>
                                <span className="button-text">Download</span>
                            </button>
                            <select
                                value={downloadFormat}
                                onChange={(e) => setDownloadFormat(e.target.value)}
                                className="format-select"
                            >
                                <option value="pdf">PDF</option>
                                <option value="images">Images (ZIP)</option>
                            </select>
                        </div>
                    )}
                </div>
            </div>

            {/* Main Content */}
            <div className="story-content">
                {isCover ? (
                    <div className="cover-page">
                        <div className="cover-container">
                            <h1 className="story-title">{story.title}</h1>
                            <div className="cover-image-container">
                                <img
                                    src={story.coverImageUrl || "/placeholder.svg"}
                                    alt="Story Cover"
                                    className="cover-image"
                                    onClick={(e) => enlargeImage(story.coverImageUrl || "/placeholder.svg", e)}
                                />
                                <div className="cover-overlay">
                                    <button onClick={startReading} className="start-reading-btn">
                                        <span className="button-icon">📖</span>
                                        Start Reading
                                    </button>
                                </div>
                            </div>
                            <div className="cover-details">
                                <p className="story-info">A magical adventure awaits!</p>
                                <div className="story-stats">
                                    <span className="stat">
                                        <span className="stat-icon">📄</span>
                                        {story.pages.length} pages
                                    </span>
                                    <span className="stat">
                                        <span className="stat-icon">⏱️</span>~{Math.ceil(story.pages.length * 1.5)} min read
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className="story-page">
                        <div className="page-container">
                            <div className="page-image-container">
                                <img
                                    src={page.imageUrl || "/placeholder.svg"}
                                    alt={`Page ${currentPage + 1}`}
                                    className="page-image"
                                    onClick={(e) => enlargeImage(page.imageUrl || "/placeholder.svg", e)}
                                />
                                <div className="image-enlarge-hint">
                                    <span className="enlarge-icon">🔍</span>
                                    <span className="enlarge-text">Click to enlarge</span>
                                </div>
                            </div>
                            <div className="page-text-container">
                                <p className="page-text">{page.text}</p>
                                {isLastPage && (
                                    <div className="finish-story-section">
                                        <button onClick={finishStory} className="finish-story-btn">
                                            <span className="button-icon">🌟</span>
                                            Finish Story
                                        </button>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                )}
            </div>

            {/* Navigation Controls */}
            <div className={`story-navigation ${showControls ? "visible" : "hidden"}`}>
                <button onClick={prevPage} disabled={currentPage === -1} className="nav-button prev-button">
                    <span className="nav-icon">←</span>
                    <span className="nav-text">Previous</span>
                </button>

                <div className="page-indicators">
                    <button onClick={() => goToPage(-1)} className={`page-dot ${isCover ? "active" : ""}`} title="Cover">
                        <span className="dot-icon">📖</span>
                    </button>
                    {story.pages.map((_, index) => (
                        <button
                            key={index}
                            onClick={() => goToPage(index)}
                            className={`page-dot ${currentPage === index ? "active" : ""}`}
                            title={`Page ${index + 1}`}
                        >
                            {index + 1}
                        </button>
                    ))}
                </div>

                <button onClick={nextPage} className="nav-button next-button">
                    <span className="nav-text">{isLastPage ? "Finish" : "Next"}</span>
                    <span className="nav-icon">{isLastPage ? "🌟" : "→"}</span>
                </button>
            </div>

            {/* Image Enlargement Modal */}
            {enlargedImage && (
                <div className="image-modal" onClick={closeEnlargedImage}>
                    <div className="image-modal-content">
                        <button className="close-modal-btn" onClick={closeEnlargedImage}>
                            <span>✕</span>
                        </button>
                        <img src={enlargedImage || "/placeholder.svg"} alt="Enlarged view" className="enlarged-image" />
                        <div className="modal-hint">
                            <p>Click anywhere to close</p>
                        </div>
                    </div>
                </div>
            )}

            {/* Completion Screen */}
            {showCompletion && (
                <div className="completion-overlay visible">
                    <div className="completion-content">
                        <div className="completion-icon">🌟</div>
                        <h2>The End!</h2>
                        <p>What a magical adventure! Did you enjoy the story?</p>
                        <div className="completion-actions">
                            <button onClick={readAgain} className="read-again-btn">
                                <span className="button-icon">🔄</span>
                                Read Again
                            </button>
                            <button onClick={() => navigate("/create")} className="create-another-btn">
                                <span className="button-icon">✨</span>
                                Create Another Story
                            </button>
                            <button onClick={() => navigate("/profile")} className="back-profile-btn">
                                <span className="button-icon">👤</span>
                                Back to Profile
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Reading Instructions */}
            {!isReading && !isCover && (
                <div className="reading-hint">
                    <p>💡 Tap anywhere to show/hide controls</p>
                </div>
            )}

            {/* Share Modal */}
            {showShareModal && (
                <div className="share-modal" onClick={() => setShowShareModal(false)}>
                    <div className="share-modal-content" onClick={(e) => e.stopPropagation()}>
                        <div className="share-modal-header">
                            <h3>Share Your Story</h3>
                            <button className="close-modal-btn" onClick={() => setShowShareModal(false)}>
                                <span>✕</span>
                            </button>
                        </div>

                        <div className="share-modal-body">
                            <div className="story-preview">
                                <img src={story.coverImageUrl || "/placeholder.svg"} alt="Story cover" className="share-story-image" />
                                <div className="share-story-info">
                                    <h4>{story.title}</h4>
                                    <p>A magical story created on CozyPages</p>
                                </div>
                            </div>

                            <div className="share-url-section">
                                <label>Share Link:</label>
                                <div className="url-input-container">
                                    <input type="text" value={shareUrl} readOnly className="share-url-input" />
                                    <button onClick={() => copyToClipboard(shareUrl)} className="copy-button">
                                        <span className="button-icon">📋</span>
                                        Copy
                                    </button>
                                </div>
                            </div>

                            <div className="share-options">
                                <h4>Share via:</h4>
                                <div className="share-buttons">
                                    <button onClick={shareViaEmail} className="share-option-btn email">
                                        <span className="share-icon">📧</span>
                                        Email
                                    </button>
                                    <button onClick={() => shareViaSocial("twitter")} className="share-option-btn twitter">
                                        <span className="share-icon">🐦</span>
                                        Twitter
                                    </button>
                                    <button onClick={() => shareViaSocial("facebook")} className="share-option-btn facebook">
                                        <span className="share-icon">📘</span>
                                        Facebook
                                    </button>
                                    <button onClick={() => shareViaSocial("whatsapp")} className="share-option-btn whatsapp">
                                        <span className="share-icon">💬</span>
                                        WhatsApp
                                    </button>
                                </div>
                            </div>

                            {!canDownload && (
                                <div className="upgrade-prompt-small">
                                    <div className="upgrade-icon-small">⭐</div>
                                    <div className="upgrade-text-small">
                                        <strong>Want to download your stories?</strong>
                                        <p>Upgrade to Pro or Premium to download as PDF or image files!</p>
                                        <button onClick={() => navigate("/upgrade")} className="mini-upgrade-btn">
                                            Upgrade Now
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    )
}

export default StoryViewerPage
