// src/pages/CreatePage.jsx
import React, { useState } from 'react';
import StoryForm from '../components/StoryForm';
import axios from '../api';

const CreatePage = () => {
    const [storyPages, setStoryPages] = useState([]);

    const generateStory = async (formData) => {
        try {
            const res = await axios.post('/api/story/generate', formData);
            setStoryPages(res.data.pages);
        } catch (err) {
            console.error('API Error:', err);
        }
    };

    return (
        <div style={{ padding: '2rem' }}>
            <h2>Create Your Custom Story</h2>
            <StoryForm onSubmit={generateStory} />

            {storyPages.length > 0 && (
                <div style={{ marginTop: '2rem' }}>
                    {storyPages.map((page, idx) => (
                        <p key={idx}><strong>Page {idx + 1}:</strong> {page}</p>
                    ))}
                </div>
            )}
        </div>
    );
};

export default CreatePage;