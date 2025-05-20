// src/components/StoryForm.jsx

import React, { useState } from 'react';

const StoryForm = ({ onSubmit }) => {
    const [formData, setFormData] = useState({
        characterName: '',
        gender: '',
        skinTone: '',
        hairColor: '',
        eyeColor: '',
        age: '',
        shirtColor: '',
        pantsColor: '',
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

        const {
            characterName,
            age,
            gender,
            skinTone,
            hairColor,
            eyeColor,
            shirtColor,
            pantsColor,
            theme
        } = formData;

        const normalized = (value) => value.trim().toLowerCase();

        const characterDescription = `${normalized(age)}-year-old ${normalized(gender)} child with ${normalized(skinTone)} skin, ${normalized(hairColor)} hair, and ${normalized(eyeColor)} eyes, wearing a ${normalized(shirtColor)} shirt and ${normalized(pantsColor)} pants`;

        onSubmit({
            characterName: normalized(characterName),
            characterDescription,
            theme: normalized(theme)
        });
    };

    return (
        <form onSubmit={handleSubmit} style={styles.form}>
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
                name="gender"
                placeholder="Gender"
                value={formData.gender}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="skinTone"
                placeholder="Skin Tone"
                value={formData.skinTone}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="hairColor"
                placeholder="Hair Color"
                value={formData.hairColor}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="eyeColor"
                placeholder="Eye Color"
                value={formData.eyeColor}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="age"
                placeholder="Age"
                value={formData.age}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="shirtColor"
                placeholder="Shirt Color"
                value={formData.shirtColor}
                onChange={handleChange}
                required
                style={styles.input}
            />

            <input
                name="pantsColor"
                placeholder="Pants Color"
                value={formData.pantsColor}
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