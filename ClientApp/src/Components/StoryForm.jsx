// src/components/StoryForm.jsx
import React, { useState } from 'react';

const StoryForm = ({ onSubmit }) => {
    const [formData, setFormData] = useState({
        childName: '',
        favoriteCharacter: '',
        theme: ''
    });

    const handleChange = (e) => {
        setFormData(prev => ({
            ...prev,
            [e.target.name]: e.target.value
        }));
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        onSubmit(formData);
    };

    return (
        <form onSubmit={handleSubmit}>
            <input
                type="text"
                name="childName"
                placeholder="Child's Name"
                value={formData.childName}
                onChange={handleChange}
                required
            />
            <input
                type="text"
                name="favoriteCharacter"
                placeholder="Favorite Character"
                value={formData.favoriteCharacter}
                onChange={handleChange}
                required
            />
            <input
                type="text"
                name="theme"
                placeholder="Story Theme"
                value={formData.theme}
                onChange={handleChange}
                required
            />
            <button type="submit">Generate Story</button>
        </form>
    );
};

export default StoryForm;