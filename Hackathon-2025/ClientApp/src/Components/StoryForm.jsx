"use client"

import { useState } from "react"
import { useAuth } from "../context/AuthContext"
import "./StoryForm.css"

const StoryForm = ({ onSubmit }) => {
    const { user } = useAuth()
    const isFree = user?.membership === "free"
    const [readingLevel, setReadingLevel] = useState("early") // "pre" | "early" | "independent"
    const [artStyle, setArtStyle] = useState("watercolor")
    const [theme, setTheme] = useState("")
    const [lesson, setLesson] = useState("");
    const membership = (user?.membership || "free").toLowerCase();
    const [storyLength, setStoryLength] = useState("short");

    const lengthOptionsByMembership = {
        free: [{ value: "short", label: "Short (about 4 pages)" }],
        pro: [{ value: "short", label: "Short (about 4 pages)" },
        { value: "medium", label: "Medium (about 8 pages)" }],
        premium: [{ value: "short", label: "Short (about 4 pages)" },
        { value: "medium", label: "Medium (about 8 pages)" },
        { value: "long", label: "Long (about 12 pages)" }],
    };
    const availableLengths = lengthOptionsByMembership[membership] || lengthOptionsByMembership.free;
    const isLengthAllowed = (v) => availableLengths.some(o => o.value === v);
    const defaultLessons = [
        // Kindness & Social
        "Always be kind to others",
        "Sharing makes everyone happier",
        "Honesty is the best policy",
        "Friends help each other in good times and bad",
        "Listening is as important as speaking",
        "Being different makes you special",
        "Teamwork makes us stronger",
        "Helping others makes you feel good too",
        "Generosity brings joy",
        "Kind words can brighten someone’s day",

        // Courage & Perseverance
        "Bravery means doing the right thing even when it’s hard",
        "It’s okay to make mistakes and learn from them",
        "Perseverance helps you reach your goals",
        "Courage comes in small steps",
        "Believe in yourself",
        "Always try your best",
        "Patience brings good things",

        // Respect & Gratitude
        "Respect the world around you",
        "Be grateful for what you have",
        "Take care of animals and nature",

        // Daily Habits & Routines
        "Brush your teeth every morning and night",
        "Wash your hands before eating and after playing",
        "Always flush and wash after using the bathroom",
        "Keeping your room tidy helps you find things",
        "Washing your face keeps you fresh and healthy",
        "Getting good sleep makes you strong and happy",
        "Eating vegetables helps you grow healthy and strong",
        "Drinking water keeps your body happy",
        "Exercise and play keep you strong",
        "Putting toys away keeps your space safe",

        // Safety
        "Look both ways before crossing the street",
        "Stay close to a trusted adult in public",
        "Always wear your seatbelt",
        "Wear a helmet when riding a bike or scooter",
        "Ask before talking to strangers",
    ];
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
        e.preventDefault();
        const processedCharacters = characters.map((c) => ({
            ...c,
            role: c.roleCustom?.trim() ? c.roleCustom.trim() : c.role,
        }));

        const finalArtStyle = isFree ? "watercolor" : artStyle;

        onSubmit({
            theme,
            readingLevel,
            artStyle: finalArtStyle,
            characters: processedCharacters,
            lessonLearned: lesson || null,
            storyLength,
        });
    };

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
            {/* Reading/Art section */}
            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">📚</span>
                    Reading Level & Art Style
                </h3>

                <div className="dual-input-container">
                    <div className="field-group">
                        <label className="field-label">Reading Level</label>
                        <select
                            className="form-select"
                            value={readingLevel}
                            onChange={(e) => setReadingLevel(e.target.value)}
                        >
                            <option value="pre">Pre-reader</option>
                            <option value="early">Early reader</option>
                            <option value="independent">Independent</option>
                        </select>
                    </div>

                    <div className="field-group">
                        <label className="field-label">
                            Art Style{" "}
                            {isFree && (
                                <span className="badge-locked" aria-hidden="true">
                                    🔒 Members only
                                </span>
                            )}
                        </label>

                        <select
                            className={`form-select ${isFree ? "art-style-locked" : ""}`}
                            value={artStyle}
                            onChange={(e) => {
                                const v = e.target.value
                                if (isFree && v !== "watercolor") {
                                    alert("Upgrade to unlock this style!")
                                    setArtStyle("watercolor")
                                } else {
                                    setArtStyle(v)
                                }
                            }}
                            title={isFree ? "Upgrade to unlock more art styles" : undefined}
                        >
                            <option value="watercolor">
                                Watercolor {isFree ? "(included)" : ""}
                            </option>

                            <option value="comic" disabled={isFree}>
                                {isFree ? "🔒 Comic (bold lines)" : "Comic (bold lines)"}
                            </option>
                            <option value="crayon" disabled={isFree}>
                                {isFree ? "🔒 Crayon (kid-like)" : "Crayon (kid-like)"}
                            </option>
                            <option value="papercut" disabled={isFree}>
                                {isFree ? "🔒 Paper cutout (flat)" : "Paper cutout (flat)"}
                            </option>
                            <option value="toy3d" disabled={isFree}>
                                {isFree ? "🔒 3D toy render" : "3D toy render"}
                            </option>
                            <option value="pixel" disabled={isFree}>
                                {isFree ? "🔒 Pixel art (retro)" : "Pixel art (retro)"}
                            </option>
                            <option value="inkwash" disabled={isFree}>
                                {isFree ? "🔒 Ink & wash (minimal)" : "Ink & wash (minimal)"}
                            </option>
                        </select>

                        {isFree && (
                            <>
                                <p className="style-lock-hint">
                                    Watercolor is included on the Free plan.{" "}
                                    <a href="/upgrade">Upgrade</a> to unlock every style.
                                </p>
                            </>
                        )}
                    </div>
                </div>
            </div>

            <div className="field-group">
                <label className="field-label">Story Length</label>
                <select
                    className="form-select"
                    value={isLengthAllowed(storyLength) ? storyLength : "short"}
                    onChange={(e) => setStoryLength(e.target.value)}
                >
                    {/* always show all tiers, lock ones above plan */}
                    <option value="short">Short (about 4 pages)</option>
                    <option value="medium" disabled={membership === "free"}>{membership === "free" ? "🔒 Medium (upgrade for 8 pages)" : "Medium (about 8 pages)"}</option>
                    <option value="long" disabled={membership !== "premium"}>{membership === "premium" ? "Long (about 12 pages)" : "🔒 Long (premium only)"}</option>
                </select>
                {membership !== "premium" && (
                    <p className="style-lock-hint">
                        Choose longer stories by upgrading your plan.
                    </p>
                )}
            </div>

            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">🌟</span>
                    Lesson Learned (Optional)
                </h3>
                <div className="field-group">
                    <label className="field-label">What lesson should the story teach?</label>
                    <div className="dual-input-container">
                        <select
                            value={defaultLessons.includes(lesson) ? lesson : ""}
                            onChange={(e) => setLesson(e.target.value)}
                            className="form-select"
                            disabled={lesson.trim() !== "" && !defaultLessons.includes(lesson)}
                        >
                            <option value="">Select a Lesson</option>

                            <optgroup label="🌸 Kindness & Social">
                                <option value="Always be kind to others">Always be kind to others</option>
                                <option value="Sharing makes everyone happier">Sharing makes everyone happier</option>
                                <option value="Honesty is the best policy">Honesty is the best policy</option>
                                <option value="Friends help each other in good times and bad">Friends help each other in good times and bad</option>
                                <option value="Listening is as important as speaking">Listening is as important as speaking</option>
                                <option value="Being different makes you special">Being different makes you special</option>
                                <option value="Teamwork makes us stronger">Teamwork makes us stronger</option>
                                <option value="Helping others makes you feel good too">Helping others makes you feel good too</option>
                                <option value="Generosity brings joy">Generosity brings joy</option>
                                <option value="Kind words can brighten someone’s day">Kind words can brighten someone’s day</option>
                            </optgroup>

                            <optgroup label="⚡ Courage & Perseverance">
                                <option value="Bravery means doing the right thing even when it’s hard">Bravery means doing the right thing even when it’s hard</option>
                                <option value="It’s okay to make mistakes and learn from them">It’s okay to make mistakes and learn from them</option>
                                <option value="Perseverance helps you reach your goals">Perseverance helps you reach your goals</option>
                                <option value="Courage comes in small steps">Courage comes in small steps</option>
                                <option value="Believe in yourself">Believe in yourself</option>
                                <option value="Always try your best">Always try your best</option>
                                <option value="Patience brings good things">Patience brings good things</option>
                            </optgroup>

                            <optgroup label="🌍 Respect & Gratitude">
                                <option value="Respect the world around you">Respect the world around you</option>
                                <option value="Be grateful for what you have">Be grateful for what you have</option>
                                <option value="Take care of animals and nature">Take care of animals and nature</option>
                            </optgroup>

                            <optgroup label="🛁 Daily Habits & Routines">
                                <option value="Brush your teeth every morning and night">Brush your teeth every morning and night</option>
                                <option value="Wash your hands before eating and after playing">Wash your hands before eating and after playing</option>
                                <option value="Always flush and wash after using the bathroom">Always flush and wash after using the bathroom</option>
                                <option value="Keeping your room tidy helps you find things">Keeping your room tidy helps you find things</option>
                                <option value="Washing your face keeps you fresh and healthy">Washing your face keeps you fresh and healthy</option>
                                <option value="Getting good sleep makes you strong and happy">Getting good sleep makes you strong and happy</option>
                                <option value="Eating vegetables helps you grow healthy and strong">Eating vegetables helps you grow healthy and strong</option>
                                <option value="Drinking water keeps your body happy">Drinking water keeps your body happy</option>
                                <option value="Exercise and play keep you strong">Exercise and play keep you strong</option>
                                <option value="Putting toys away keeps your space safe">Putting toys away keeps your space safe</option>
                            </optgroup>

                            <optgroup label="🚦 Safety">
                                <option value="Look both ways before crossing the street">Look both ways before crossing the street</option>
                                <option value="Stay close to a trusted adult in public">Stay close to a trusted adult in public</option>
                                <option value="Always wear your seatbelt">Always wear your seatbelt</option>
                                <option value="Wear a helmet when riding a bike or scooter">Wear a helmet when riding a bike or scooter</option>
                                <option value="Ask before talking to strangers">Ask before talking to strangers</option>
                            </optgroup>
                        </select>

                        <input
                            placeholder="Or enter your own lesson"
                            value={!defaultLessons.includes(lesson) ? lesson : ""}
                            onChange={(e) => setLesson(e.target.value)}
                            className="form-input"
                        />
                    </div>
                </div>
            </div>

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
                                <option key={t} value={t}>{t}</option>
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
