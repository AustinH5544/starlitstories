// src/pages/CreatePage.jsx
import React, { useState } from 'react';
import StoryForm from '../components/StoryForm';
import axios from '../api';

const CreatePage = () => {
    const [storyPages, setStoryPages] = useState([]);
    const [isLoading, setIsLoading] = useState(false);

    const generateStory = async (formData) => {
        setIsLoading(true);
        try {
            const res = await axios.post('/story/generate', formData);
            setStoryPages(res.data.pages);
        } catch (err) {
            console.error('API Error:', err);
            setStoryPages(["Oops! Something went wrong generating your story."]);
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div style={{ padding: '2rem' }}>
            <h2>Create Your Custom Story</h2>
            <StoryForm onSubmit={generateStory} />

            {isLoading && <p>Generating your story...</p>}

            {!isLoading && storyPages.length > 0 && (
                <div style={{ marginTop: '2rem' }}>
                    <h3>Your Story:</h3>
                    {storyPages.map((page, idx) => (
                        <div key={idx} style={{ marginBottom: '1.5rem' }}>
                            <strong>Page {idx + 1}:</strong>
                            <p>{page}</p>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default CreatePage;