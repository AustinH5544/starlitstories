import React, { useRef, useState, useEffect } from "react";

export default function ScrollableTextPane({ text, showFinish, finishLayout = "section", onFinish }) {
    const textRef = useRef(null);
    const [scrollInfo, setScrollInfo] = useState({
        canScroll: false,
        thumbTop: 0,
        thumbHeight: 100,
    });

    useEffect(() => {
        const el = textRef.current;
        if (!el) return;

        const update = () => {
            const scrollHeight = el.scrollHeight;
            const clientHeight = el.clientHeight;

            // If there is no overflow, hide the fake scrollbar
            if (scrollHeight <= clientHeight + 1) {
                setScrollInfo((prev) =>
                    prev.canScroll
                        ? { canScroll: false, thumbTop: 0, thumbHeight: 100 }
                        : prev
                );
                return;
            }

            const maxScrollTop = scrollHeight - clientHeight;
            const scrollTop = el.scrollTop;
            const ratio = clientHeight / scrollHeight;
            const thumbHeight = Math.max(ratio * 100, 10); // don't get too tiny
            const top =
                maxScrollTop > 0
                    ? (scrollTop / maxScrollTop) * (100 - thumbHeight)
                    : 0;

            setScrollInfo({
                canScroll: true,
                thumbTop: top,
                thumbHeight,
            });
        };

        update();
        el.addEventListener("scroll", update);
        window.addEventListener("resize", update);

        return () => {
            el.removeEventListener("scroll", update);
            window.removeEventListener("resize", update);
        };
    }, [text]);

    const handleFinishClick = (e) => {
        e.preventDefault();
        e.stopPropagation();
        onFinish && onFinish();
    };

    return (
        <div className="page-text-container">
            {/* inner scrollable area */}
            <div className="page-text-inner" ref={textRef}>
                <p className="page-text">{text}</p>

                {showFinish && (
                    finishLayout === "inline" ? (
                        <div className="finish-inline">
                            <button
                                onClick={handleFinishClick}
                                className="finish-story-btn"
                            >
                                <span className="button-icon">🌟</span>
                                Finish Story
                            </button>
                        </div>
                    ) : (
                        <div className="finish-story-section">
                            <button
                                onClick={handleFinishClick}
                                className="finish-story-btn"
                            >
                                <span className="button-icon">🌟</span>
                                Finish Story
                            </button>
                        </div>
                    )
                )}
            </div>

            {/* fake static scrollbar on the side */}
            {scrollInfo.canScroll && (
                <div className="fake-scrollbar">
                    <div
                        className="fake-scrollbar-thumb"
                        style={{
                            height: `${scrollInfo.thumbHeight}%`,
                            top: `${scrollInfo.thumbTop}%`,
                        }}
                    />
                </div>
            )}
        </div>
    );
}
