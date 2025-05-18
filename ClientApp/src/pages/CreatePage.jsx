import React, { useState } from 'react';
import NavBar from '../components/NavBar';
import StoryForm from '../components/StoryForm';
import axios from '../api';
import './CreatePage.css';

const CreatePage = () => {
    const [storyPages, setStoryPages] = useState([]);
    const [isLoading, setIsLoading] = useState(false);
    const [storyReady, setStoryReady] = useState(false);
    const [error, setError] = useState(null);

    const generateStory = async (formData) => {
        setIsLoading(true);
        setStoryReady(false);
        setError(null);
        setStoryPages([]);

        try {
            const res = await axios.post('/story/generate', formData);
            setStoryPages(res.data.pages);
            setStoryReady(true);
        } catch (err) {
            console.error('API Error:', err);
            setError("Oops! Something went wrong generating your story.");
        } finally {
            setIsLoading(false);
        }
    };

    const isValidStory =
        storyPages.length > 0 &&
        !storyPages[0].toLowerCase().startsWith('oops') &&
        !error;

    return (
        <>
            <NavBar />
            <div className="create-page">
                <div className="create-form-wrapper">
                    <h2 className="create-header">Create a Personalized Bedtime Story</h2>
                    <StoryForm onSubmit={generateStory} />

                    {isLoading && <p className="loading-text">✨ Generating your story...</p>}

                    {!isLoading && error && <p className="create-error">{error}</p>}

                    {!isLoading && storyReady && isValidStory && (
                        <button
                            className="view-story-button"
                            onClick={() => alert("This will navigate to the story page soon!")}
                        >
                            View Your Story
                        </button>
                    )}
                </div>
            </div>
        </>
    );
};

export default CreatePage;