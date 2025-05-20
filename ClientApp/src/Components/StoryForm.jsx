import React, { useState } from 'react';

const StoryForm = ({ onSubmit }) => {
    const [theme, setTheme] = useState('');
    const [characters, setCharacters] = useState([
        {
            role: 'main',
            roleCustom: '',
            name: '',
            isAnimal: false,
            descriptionFields: {}
        }
    ]);

    const defaultOptions = {
        age: Array.from({ length: 17 }, (_, i) => (i + 2).toString()),
        gender: ['boy', 'girl', 'non-binary', 'gender-fluid', 'other'],
        skinTone: ['pale', 'light', 'tan', 'olive', 'brown', 'dark', 'freckled'],
        hairColor: [
            'blonde', 'dirty blonde', 'light brown', 'brown', 'dark brown',
            'black', 'red', 'auburn', 'gray', 'white', 'pink', 'blue', 'green', 'purple'
        ],
        eyeColor: ['blue', 'green', 'hazel', 'brown', 'amber', 'gray', 'violet'],
        shirtColor: ['blue', 'red', 'green', 'yellow', 'purple', 'orange', 'pink', 'white', 'black'],
        pantsColor: ['blue', 'black', 'gray', 'green', 'khaki', 'white', 'brown'],
        species: ['dog', 'cat', 'fox', 'rabbit', 'turtle', 'bird', 'dragon', 'dinosaur', 'horse', 'bear', 'koala'],
        bodyCovering: ['fur', 'scales', 'feathers', 'skin', 'shell'],
        bodyColor: ['white', 'black', 'gray', 'brown', 'golden', 'red', 'green', 'blue', 'pink', 'orange', 'striped', 'spotted', 'glowing'],
        humanAccessories: [
            'glasses', 'hat', 'scarf', 'backpack', 'necklace', 'bracelet',
            'watch', 'boots', 'sneakers', 'wand', 'cape', 'crown', 'bowtie'
        ],
        animalAccessories: [
            'collar', 'bandana', 'tiny hat', 'bell', 'bow', 'armor', 'wing clips', 'ribbon', 'feather accessory'
        ]
    };

    const defaultThemes = [
        'Magical Forest',
        'Outer Space',
        'Under the Sea',
        'Pirate Adventure',
        'Jungle Quest',
        'Dinosaur World',
        'Enchanted Castle',
        'Friendly Monsters',
        'Robot City',
        'Fairy Garden',
        'Winter Wonderland',
        'Desert Treasure Hunt',
        'Time Travel',
        'Circus Mystery',
        'Dreamland'
    ];


    const handleCharacterChange = (index, key, value) => {
        const updated = [...characters];
        updated[index][key] = value;
        setCharacters(updated);
    };

    const handleFieldChange = (index, field, value) => {
        const updated = [...characters];
        updated[index].descriptionFields[field] = value;
        setCharacters(updated);
    };

    const addCharacter = () => {
        setCharacters([...characters, {
            role: 'friend',
            roleCustom: '',
            name: '',
            isAnimal: false,
            descriptionFields: {}
        }]);
    };

    const removeCharacter = (index) => {
        const updated = [...characters];
        updated.splice(index, 1);
        setCharacters(updated);
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        const processedCharacters = characters.map(c => ({
            ...c,
            role: c.roleCustom?.trim() ? c.roleCustom.trim() : c.role
        }));
        onSubmit({ theme, characters: processedCharacters });
    };

    const renderDropdownWithCustom = (index, field, label, options = []) => (
        <div style={styles.fieldGroup}>
            <label style={styles.label}>{label}</label>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
                <select
                    value={characters[index].descriptionFields[field] || ''}
                    onChange={(e) => handleFieldChange(index, field, e.target.value)}
                    style={{ ...styles.input, fontSize: '1.1rem', height: '2.5rem' }}
                >
                    <option value="">Select {label}</option>
                    {options.map(opt => (
                        <option key={opt} value={opt}>
                            {opt.charAt(0).toUpperCase() + opt.slice(1)}
                        </option>
                    ))}
                </select>
                <input
                    placeholder={`Or enter custom ${label}`}
                    value={characters[index].descriptionFields[field + 'Custom'] || ''}
                    onChange={(e) => handleFieldChange(index, field + 'Custom', e.target.value)}
                    style={styles.input}
                />
            </div>
        </div>
    );


    return (
        <form onSubmit={handleSubmit} style={styles.form}>
            <div style={styles.fieldGroup}>
                <label style={styles.label}>Story Theme</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem' }}>
                    <select
                        value={defaultThemes.includes(theme) ? theme : ''}
                        onChange={(e) => setTheme(e.target.value)}
                        style={styles.input}
                    >
                        <option value="">Select Theme</option>
                        {defaultThemes.map(t => (
                            <option key={t} value={t}>{t}</option>
                        ))}
                    </select>
                    <input
                        placeholder="Or enter custom theme"
                        value={!defaultThemes.includes(theme) ? theme : ''}
                        onChange={(e) => setTheme(e.target.value)}
                        style={styles.input}
                    />
                </div>
            </div>

            {characters.map((char, i) => (
                <div key={i} style={styles.characterBox}>
                    <h4>{char.role === 'main' ? 'Main Character' : `Character ${i + 1}`}</h4>
                    <input
                        type="text"
                        placeholder="Name"
                        value={char.name}
                        onChange={(e) => handleCharacterChange(i, 'name', e.target.value)}
                        required
                        style={styles.input}
                    />

                    {i > 0 && (
                        <div style={styles.roleContainer}>
                            <select
                                value={char.role}
                                onChange={(e) => handleCharacterChange(i, 'role', e.target.value)}
                                style={styles.input}
                            >
                                <option value="">Select Role</option>
                                <option value="friend">Friend</option>
                                <option value="mom">Mom</option>
                                <option value="dad">Dad</option>
                                <option value="pet">Pet</option>
                                <option value="sibling">Sibling</option>
                                <option value="teacher">Teacher</option>
                            </select>
                            <input
                                type="text"
                                placeholder="Or enter custom role"
                                value={char.roleCustom || ''}
                                onChange={(e) => handleCharacterChange(i, 'roleCustom', e.target.value)}
                                style={styles.input}
                            />
                        </div>
                    )}

                    <label style={styles.checkboxLabel}>
                        <input
                            type="checkbox"
                            checked={char.isAnimal}
                            onChange={(e) => handleCharacterChange(i, 'isAnimal', e.target.checked)}
                        />
                        Is Animal?
                    </label>

                    {char.isAnimal ? (
                        <>
                            {renderDropdownWithCustom(i, 'species', 'Species', defaultOptions.species)}
                            {renderDropdownWithCustom(i, 'bodyCovering', 'Body Covering Type', defaultOptions.bodyCovering)}
                            {renderDropdownWithCustom(i, 'bodyColor', 'Body Covering Color', defaultOptions.bodyColor)}
                            {renderDropdownWithCustom(
                                i,
                                'accessory',
                                'Accessory (optional)',
                                char.isAnimal ? defaultOptions.animalAccessories : defaultOptions.humanAccessories
                            )}
                        </>
                    ) : (
                        <>
                            {renderDropdownWithCustom(i, 'age', 'Age', defaultOptions.age)}
                            {renderDropdownWithCustom(i, 'gender', 'Gender', defaultOptions.gender)}
                            {renderDropdownWithCustom(i, 'skinTone', 'Skin Tone', defaultOptions.skinTone)}
                            {renderDropdownWithCustom(i, 'hairColor', 'Hair Color', defaultOptions.hairColor)}
                            {renderDropdownWithCustom(i, 'eyeColor', 'Eye Color', defaultOptions.eyeColor)}
                            {renderDropdownWithCustom(i, 'shirtColor', 'Shirt Color', defaultOptions.shirtColor)}
                            {renderDropdownWithCustom(i, 'pantsColor', 'Pants Color', defaultOptions.pantsColor)}
                            {renderDropdownWithCustom(
                                i,
                                'accessory',
                                'Accessory (optional)',
                                char.isAnimal ? defaultOptions.animalAccessories : defaultOptions.humanAccessories
                            )}
                        </>
                    )}

                    {i > 0 && (
                        <button type="button" onClick={() => removeCharacter(i)} style={styles.removeBtn}>
                            Remove Character
                        </button>
                    )}
                </div>
            ))}

            <button type="button" onClick={addCharacter} style={styles.addBtn}>
                + Add Character
            </button>
            <button type="submit" style={styles.button}>Generate Story</button>
        </form>
    );
};

const styles = {
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: '1.5rem',
        maxWidth: '600px',
        margin: '2rem auto',
        padding: '1rem',
        backgroundColor: '#fff',
        borderRadius: '10px',
        boxShadow: '0 0 10px rgba(0,0,0,0.1)'
    },
    input: {
        padding: '0.5rem',
        fontSize: '1rem',
        borderRadius: '6px',
        border: '1px solid #ccc',
        width: '100%',
        boxSizing: 'border-box',
        minWidth: 0
    },
    characterBox: {
        padding: '1rem',
        borderRadius: '8px',
        backgroundColor: '#f9f9f9',
        border: '1px solid #ddd',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem'
    },
    label: {
        fontWeight: 'bold',
        marginBottom: '0.25rem'
    },
    checkboxLabel: {
        display: 'flex',
        alignItems: 'center',
        gap: '0.5rem',
        marginTop: '0.5rem'
    },
    roleContainer: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '0.75rem'
    },
    fieldGroup: {
        display: 'flex',
        flexDirection: 'column',
        gap: '0.25rem'
    },
    button: {
        padding: '0.75rem',
        fontSize: '1rem',
        backgroundColor: '#0077cc',
        color: '#fff',
        border: 'none',
        borderRadius: '6px',
        cursor: 'pointer',
        marginTop: '1rem'
    },
    addBtn: {
        backgroundColor: '#28a745',
        color: '#fff',
        padding: '0.5rem',
        border: 'none',
        borderRadius: '6px',
        cursor: 'pointer',
        width: 'fit-content'
    },
    removeBtn: {
        backgroundColor: '#dc3545',
        color: '#fff',
        padding: '0.5rem',
        border: 'none',
        borderRadius: '6px',
        cursor: 'pointer',
        width: 'fit-content'
    }
};


export default StoryForm;