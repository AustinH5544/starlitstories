import React, { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import './StoryViewerPage.css';

const StoryViewerPage = () => {
    const { state } = useLocation();
    const navigate = useNavigate();

    const [story, setStory] = useState(null);
    const [currentPage, setCurrentPage] = useState(-1); // -1 = cover

    // Load story from state or localStorage
    useEffect(() => {
        if (state?.story) {
            setStory(state.story);
            localStorage.setItem('story', JSON.stringify(state.story));
        } else {
            const savedStory = localStorage.getItem('story');
            if (savedStory) {
                setStory(JSON.parse(savedStory));
            }
        }
    }, [state]);

    const nextPage = () => {
        if (story && currentPage < story.pages.length - 1) {
            setCurrentPage(currentPage + 1);
        }
    };

    const prevPage = () => {
        if (currentPage > -1) {
            setCurrentPage(currentPage - 1);
        }
    };

    if (!story || !Array.isArray(story.pages)) {
        return (
            <>
                <div style={{ textAlign: 'center', padding: '2rem' }}>
                    <h2>Oops! No story found.</h2>
                    <p>Please return to the create page to generate one.</p>
                    <button
                        onClick={() => navigate('/create')}
                        style={{
                            marginTop: '1rem',
                            padding: '0.75rem 1.5rem',
                            fontSize: '1rem',
                            backgroundColor: '#0077cc',
                            color: '#fff',
                            border: 'none',
                            borderRadius: '6px',
                            cursor: 'pointer'
                        }}
                    >
                        Back to Create Page
                    </button>
                </div>
            </>
        );
    }

    const isCover = currentPage === -1;
    const page = story.pages[currentPage];

    return (
        <>
            <div className="story-viewer">
                {isCover ? (
                    <div className="cover">
                        <h1 className="story-title">{story.title}</h1>
                        <img src={story.coverImageUrl} alt="Cover" className="cover-image" />
                    </div>
                ) : (
                    <div className="page">
                        <img src={page.imageUrl} alt={`Page ${currentPage + 1}`} className="page-image" />
                        <p className="page-text">{page.text}</p>
                    </div>
                )}

                <div className="nav-buttons">
                    <button onClick={prevPage} disabled={currentPage === -1}>⬅</button>
                    <button onClick={nextPage} disabled={currentPage === story.pages.length - 1}>➡</button>
                </div>
            </div>
        </>
    );
};

export default StoryViewerPage;