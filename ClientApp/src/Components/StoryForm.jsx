// src/components/StoryForm.jsx

import React, { useState } from 'react';

const StoryForm = ({ onSubmit }) => {
    const [formData, setFormData] = useState({
        characterName: '',
        characterDescription: '',
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
        if (formData.characterName && formData.characterDescription && formData.theme) {
            onSubmit(formData); // calls function from parent (App.jsx)
        }
    };

    return (
        <form onSubmit={handleSubmit} style={styles.form}>
            <h2>Create a Personalized Bedtime Story</h2>
            <input
                type="text"
                name="characterName"
                placeholder="Character's Name"
                value={formData.characterName}
                onChange={handleChange}
                required
                style={styles.input}
            />
            <input
                type="text"
                name="characterDescription"
                placeholder="Description Of Character"
                value={formData.characterDescription}
                onChange={handleChange}
                required
                style={styles.input}
            />
            <input
                type="text"
                name="theme"
                placeholder="Story Theme (e.g. jungle, space)"
                value={formData.theme}
                onChange={handleChange}
                required
                style={styles.input}
            />
            <button type="submit" style={styles.button}>Generate Story</button>
        </form>
    );
};

const styles = {
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1rem',
        maxWidth: '400px',
        margin: '2rem auto'
    },
    input: {
        padding: '0.5rem',
        fontSize: '1rem'
    },
    button: {
        padding: '0.75rem',
        fontSize: '1rem',
        backgroundColor: '#0077cc',
        color: '#fff',
        border: 'none',
        borderRadius: '4px',
        cursor: 'pointer'
    }
};

export default StoryForm;