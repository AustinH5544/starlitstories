import React, { useState } from 'react';
import { useAuth } from '../context/AuthContext';
import StoryForm from '../components/StoryForm';
import axios from '../api';
import './CreatePage.css';
import { useNavigate } from 'react-router-dom';

const CreatePage = () => {
    const { user } = useAuth();
    const [story, setStory] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [storyReady, setStoryReady] = useState(false);
    const [error, setError] = useState(null);
    const navigate = useNavigate();

    const generateStory = async (formData) => {
        setIsLoading(true);
        setStoryReady(false);
        setError(null);
        setStory(null);

        try {
            // Append user email to form data
            const fullRequest = {
                ...formData,
                email: user?.email || '',
            };

            const res = await axios.post('/story/generate-full', fullRequest);
            setStory(res.data);
            setStoryReady(true);
        } catch (err) {
            console.error('API Error:', err);
            const message =
                err?.response?.data ?? 'Oops! Something went wrong generating your story.';
            setError(message);
        } finally {
            setIsLoading(false);
        }
    };

    const isValidStory =
        story &&
        Array.isArray(story.pages) &&
        story.pages.length > 0 &&
        story.pages[0].text?.toLowerCase().startsWith('oops') === false &&
        !error;

    return (
        <div className="create-page">
            <div className="create-form-wrapper">
                <h2 className="create-header">Create a Personalized Bedtime Story</h2>
                <StoryForm onSubmit={generateStory} />

                {isLoading && <p className="loading-text">✨ Generating your story...</p>}
                {!isLoading && error && <p className="create-error">{error}</p>}

                {!isLoading && storyReady && isValidStory && (
                    <button
                        className="view-story-button"
                        onClick={() => navigate('/view', { state: { story } })}
                    >
                        View Your Story
                    </button>
                )}
            </div>
        </div>
    );
};

export default CreatePage;