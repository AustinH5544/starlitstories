"use client"

import { useState } from "react"
import { useAuth } from "../context/AuthContext"
import "./StoryForm.css"

const StoryForm = ({ onSubmit }) => {
    const { user } = useAuth()
    const [theme, setTheme] = useState("")
    const [characters, setCharacters] = useState([
        {
            role: "main",
            roleCustom: "",
            name: "",
            isAnimal: false,
            descriptionFields: {},
        },
    ])

    const defaultOptions = {
        age: Array.from({ length: 17 }, (_, i) => (i + 2).toString()),
        gender: ["boy", "girl", "man", "woman", "non-binary", "gender-fluid", "other"],
        skinTone: ["pale", "light", "tan", "olive", "brown", "dark", "freckled"],
        hairColor: [
            "blonde",
            "dirty blonde",
            "light brown",
            "brown",
            "dark brown",
            "black",
            "red",
            "auburn",
            "gray",
            "white",
            "pink",
            "blue",
            "green",
            "purple",
        ],
        hairstylesByGender: {
            boy: ["buzz cut", "crew cut", "spiky", "messy", "mohawk", "slicked back", "short side part"],
            girl: ["braided", "pigtails", "ponytail", "bun", "double buns", "bob cut", "pixie cut", "crown braid"],
            man: ["buzz cut", "crew cut", "slicked back", "short side part", "undercut", "shaggy", "top knot"],
            woman: ["long and flowing", "curly", "wavy", "layered", "bun", "side shave", "twists", "halo braid"],
            default: ["mohawk", "afro", "dreadlocks", "straight", "tied with ribbon", "wind-swept"],
        },
        eyeColor: ["blue", "green", "hazel", "brown", "amber", "gray", "violet"],
        shirtColor: ["blue", "red", "green", "yellow", "purple", "orange", "pink", "white", "black"],
        pantsColor: ["blue", "black", "gray", "green", "khaki", "white", "brown"],
        shoeColor: ["black", "brown", "white", "red", "blue", "pink", "green", "yellow", "silver", "gold"],
        species: ["dog", "cat", "fox", "rabbit", "turtle", "bird", "dragon", "dinosaur", "horse", "bear", "koala"],
        bodyCovering: ["fur", "scales", "feathers", "skin", "shell"],
        bodyColor: [
            "white",
            "black",
            "gray",
            "brown",
            "golden",
            "red",
            "green",
            "blue",
            "pink",
            "orange",
            "striped",
            "spotted",
            "glowing",
        ],
        humanAccessories: [
            "glasses",
            "hat",
            "scarf",
            "backpack",
            "necklace",
            "bracelet",
            "watch",
            "boots",
            "sneakers",
            "wand",
            "cape",
            "crown",
            "bowtie",
        ],
        animalAccessories: [
            "collar",
            "bandana",
            "tiny hat",
            "bell",
            "bow",
            "armor",
            "wing clips",
            "ribbon",
            "feather accessory",
        ],
    }

    const defaultThemes = [
        "Magical Forest",
        "Outer Space",
        "Under the Sea",
        "Pirate Adventure",
        "Jungle Quest",
        "Dinosaur World",
        "Enchanted Castle",
        "Friendly Monsters",
        "Robot City",
        "Fairy Garden",
        "Winter Wonderland",
        "Desert Treasure Hunt",
        "Time Travel",
        "Circus Mystery",
        "Dreamland",
    ]

    const handleCharacterChange = (index, key, value) => {
        const updated = [...characters]
        updated[index][key] = value
        setCharacters(updated)
    }

    const handleFieldChange = (index, field, value) => {
        const updated = [...characters]
        updated[index].descriptionFields[field] = value
        setCharacters(updated)
    }

    const addCharacter = () => {
        setCharacters([
            ...characters,
            {
                role: "",
                roleCustom: "",
                name: "",
                isAnimal: false,
                descriptionFields: {},
            },
        ])
    }

    const removeCharacter = (index) => {
        const updated = [...characters]
        updated.splice(index, 1)
        setCharacters(updated)
    }

    const handleSubmit = (e) => {
        e.preventDefault()
        const processedCharacters = characters.map((c) => ({
            ...c,
            role: c.roleCustom?.trim() ? c.roleCustom.trim() : c.role,
        }))
        onSubmit({ theme, characters: processedCharacters })
    }

    const renderDropdownWithCustom = (index, field, label, options = []) => {
        const customValue = characters[index].descriptionFields[field + "Custom"] || ""

        return (
            <div className="field-group">
                <label className="field-label">{label}</label>
                <div className="dual-input-container">
                    <select
                        value={characters[index].descriptionFields[field] || ""}
                        onChange={(e) => handleFieldChange(index, field, e.target.value)}
                        className="form-select"
                        disabled={!!customValue}
                    >
                        <option value="">Select {label}</option>
                        {options.map((opt) => (
                            <option key={opt} value={opt}>
                                {opt.charAt(0).toUpperCase() + opt.slice(1)}
                            </option>
                        ))}
                    </select>
                    <input
                        placeholder={`Or enter custom ${label.toLowerCase()}`}
                        value={customValue}
                        onChange={(e) => handleFieldChange(index, field + "Custom", e.target.value)}
                        className="form-input"
                    />
                </div>
            </div>
        )
    }

    // Check if user can add more characters
    const canAddMoreCharacters = user?.membership !== "free" || characters.length < 1
    const isAtCharacterLimit = user?.membership === "free" && characters.length >= 1

    return (
        <form onSubmit={handleSubmit} className="story-form">
            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">🎭</span>
                    Story Theme
                </h3>
                <div className="field-group">
                    <label className="field-label">Choose your adventure</label>
                    <div className="dual-input-container">
                        <select
                            value={defaultThemes.includes(theme) ? theme : ""}
                            onChange={(e) => setTheme(e.target.value)}
                            className="form-select"
                            disabled={theme.trim() !== "" && !defaultThemes.includes(theme)}
                        >
                            <option value="">Select Theme</option>
                            {defaultThemes.map((t) => (
                                <option key={t} value={t}>
                                    {t}
                                </option>
                            ))}
                        </select>
                        <input
                            placeholder="Or create your own theme"
                            value={!defaultThemes.includes(theme) ? theme : ""}
                            onChange={(e) => setTheme(e.target.value)}
                            className="form-input"
                        />
                    </div>
                </div>
            </div>

            {characters.map((char, i) => (
                <div key={i} className="character-card">
                    <div className="character-header">
                        <h3 className="character-title">
                            <span className="character-icon">{char.isAnimal ? "🐾" : "👤"}</span>
                            {char.role === "main" ? "Main Character" : `Character ${i + 1}`}
                        </h3>
                        {i > 0 && (
                            <button type="button" onClick={() => removeCharacter(i)} className="remove-character-btn">
                                <span>✕</span>
                            </button>
                        )}
                    </div>

                    <div className="field-group">
                        <label className="field-label">Character Name</label>
                        <input
                            type="text"
                            placeholder="Enter character name"
                            value={char.name}
                            onChange={(e) => handleCharacterChange(i, "name", e.target.value)}
                            required
                            className="form-input"
                        />
                    </div>

                    {i > 0 && (
                        <div className="field-group">
                            <label className="field-label">Character Role</label>
                            <div className="dual-input-container">
                                <select
                                    value={char.role}
                                    onChange={(e) => handleCharacterChange(i, "role", e.target.value)}
                                    className="form-select"
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
                                    value={char.roleCustom || ""}
                                    onChange={(e) => handleCharacterChange(i, "roleCustom", e.target.value)}
                                    className="form-input"
                                />
                            </div>
                        </div>
                    )}

                    <div className="character-type-toggle">
                        <label className="toggle-label">
                            <input
                                type="checkbox"
                                checked={char.isAnimal}
                                onChange={(e) => handleCharacterChange(i, "isAnimal", e.target.checked)}
                                className="toggle-checkbox"
                            />
                            <span className="toggle-slider"></span>
                            <span className="toggle-text">{char.isAnimal ? "🐾 Animal Character" : "👤 Human Character"}</span>
                        </label>
                    </div>

                    <div className="character-details">
                        {char.isAnimal ? (
                            <>
                                {renderDropdownWithCustom(i, "species", "Species", defaultOptions.species)}
                                {renderDropdownWithCustom(i, "bodyCovering", "Body Covering", defaultOptions.bodyCovering)}
                                {renderDropdownWithCustom(i, "bodyColor", "Body Color", defaultOptions.bodyColor)}
                                {renderDropdownWithCustom(i, "accessory", "Accessory (optional)", defaultOptions.animalAccessories)}
                            </>
                        ) : (
                            <>
                                {renderDropdownWithCustom(i, "age", "Age", defaultOptions.age)}
                                {renderDropdownWithCustom(i, "gender", "Gender", defaultOptions.gender)}
                                {(() => {
                                    const gender = char.descriptionFields.gender || ""
                                    const options = defaultOptions.hairstylesByGender[gender] || defaultOptions.hairstylesByGender.default
                                    return renderDropdownWithCustom(i, "hairStyle", "Hair Style", options)
                                })()}
                                {renderDropdownWithCustom(i, "skinTone", "Skin Tone", defaultOptions.skinTone)}
                                {renderDropdownWithCustom(i, "hairColor", "Hair Color", defaultOptions.hairColor)}
                                {renderDropdownWithCustom(i, "eyeColor", "Eye Color", defaultOptions.eyeColor)}
                                {renderDropdownWithCustom(i, "shirtColor", "Shirt Color", defaultOptions.shirtColor)}
                                {renderDropdownWithCustom(i, "pantsColor", "Pants Color", defaultOptions.pantsColor)}
                                {renderDropdownWithCustom(i, "shoeColor", "Shoe Color", defaultOptions.shoeColor)}
                                {renderDropdownWithCustom(i, "accessory", "Accessory (optional)", defaultOptions.humanAccessories)}
                            </>
                        )}
                    </div>
                </div>
            ))}

            <div className="form-actions">
                {canAddMoreCharacters ? (
                    <button type="button" onClick={addCharacter} className="add-character-btn">
                        <span className="button-icon">➕</span>
                        <span>Add Another Character</span>
                    </button>
                ) : (
                    <div className="character-limit-notice">
                        <div className="limit-icon">🔒</div>
                        <div className="limit-content">
                            <h4>Want to add more characters?</h4>
                            <p>Upgrade to Pro or Premium to include friends, family, and pets in your stories!</p>
                            <div className="limit-benefits">
                                <span className="benefit">✨ Multiple characters</span>
                                <span className="benefit">🎨 Premium illustrations</span>
                                <span className="benefit">📚 More stories per month</span>
                            </div>
                        </div>
                    </div>
                )}

                <button type="submit" className="generate-story-btn">
                    <span className="button-icon">✨</span>
                    <span>Generate My Story</span>
                </button>
            </div>
        </form>
    )
}

export default StoryForm
