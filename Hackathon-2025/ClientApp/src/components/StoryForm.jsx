"use client"

import { useState, useEffect } from "react"
import { useAuth } from "../context/AuthContext"
import "./StoryForm.css"

import themeIcon from "../assets/ui-icons/theme.png"
import lessonIcon from "../assets/ui-icons/lesson.png"
import readingIcon from "../assets/ui-icons/reading.png"
import personIcon from "../assets/ui-icons/person.png"
import sparkleIcon from "../assets/ui-icons/sparkle.png"
//import animalIcon from "../assets/ui-icons/animal.png"

const sortedUnique = (values = []) =>
    [...new Set(values.map((v) => String(v).trim()).filter(Boolean))]
        .sort((a, b) => a.localeCompare(b))

const onePieceAllowsTopLayer = (onePieceValue = "") => {
    const normalized = onePieceValue.trim().toLowerCase()
    return normalized === "overalls" || normalized.includes("overall")
}

const normalizeDescFields = (df = {}) => {
    const out = { ...df }
    for (const k of Object.keys(df)) {
        if (k.endsWith("Custom")) {
            const base = k.slice(0, -6) // strip "Custom"
            const val = (df[k] || "").trim()
            if (val) out[base] = val    // custom takes precedence
            delete out[k]               // remove the *Custom key
        }
    }
    return out
}

const StoryForm = ({ onSubmit }) => {
    const { user } = useAuth()

    // Normalize membership to a lowercase string
    const membershipRaw = user?.membership ?? "free"
    const membership = String(membershipRaw).toLowerCase()
    const isFree = membership === "free"

    // Feature flag: hide the extra UI for now
    const showCharacterTypeAndExtraButton = false

    const [readingLevel, setReadingLevel] = useState("early") // "pre" | "early" | "independent"
    const [artStyle, setArtStyle] = useState("watercolor")
    const [theme, setTheme] = useState("")
    const [lesson, setLesson] = useState("")
    const [selectedCategory, setSelectedCategory] = useState("")
    const [includeLesson, setIncludeLesson] = useState(false)
    const [showAdvancedOptions, setShowAdvancedOptions] = useState(false)

    const [lengthHintEnabled, setLengthHintEnabled] = useState(false)
    const API_BASE = import.meta.env.VITE_API_BASE ?? ""
    const [storyLength, setStoryLength] = useState("short")

    const lengthOptionsByMembership = {
        free: [{ value: "short", label: "Short (about 4 pages)" }],
        pro: [
            { value: "short", label: "Short (about 4 pages)" },
            { value: "medium", label: "Medium (about 8 pages)" },
        ],
        premium: [
            { value: "short", label: "Short (about 4 pages)" },
            { value: "medium", label: "Medium (about 8 pages)" },
            { value: "long", label: "Long (about 12 pages)" },
        ],
    }

    useEffect(() => {
        const url = `${API_BASE}/api/config`
        fetch(url, { credentials: "omit" })
            .then(res => {
                if (!res.ok) throw new Error(`HTTP ${res.status}`)
                return res.json()
            })
            .then(data => setLengthHintEnabled(!!data.lengthHintEnabled))
            .catch(err => {
                console.error("Failed to load /api/config:", err)
                setLengthHintEnabled(false)
            })
    }, [API_BASE])

    const availableLengths = lengthOptionsByMembership[membership] || lengthOptionsByMembership.free
    const isLengthAllowed = (v) => availableLengths.some(o => o.value === v)

    // --- Lessons split into categories (two-step UI) ---
    const lessonsByCategory = {
        "🌸 Kindness & Social": [
            "Always be kind to others",
            "Being different makes you special",
            "Friends help each other in good times and bad",
            "Generosity brings joy",
            "Helping others makes you feel good too",
            "Honesty is the best policy",
            "Kind words can brighten someone’s day",
            "Listening is as important as speaking",
            "Sharing makes everyone happier",
            "Teamwork makes us stronger",
        ],
        "⚡ Courage & Perseverance": [
            "Always try your best",
            "Believe in yourself",
            "Bravery means doing the right thing even when it’s hard",
            "Courage comes in small steps",
            "It’s okay to make mistakes and learn from them",
            "Patience brings good things",
            "Perseverance helps you reach your goals",
        ],
        "🌍 Respect & Gratitude": [
            "Be grateful for what you have",
            "Respect the world around you",
            "Take care of animals and nature",
        ],
        "🛁 Daily Habits & Routines": [
            "Always flush and wash after using the bathroom",
            "Brush your teeth every morning and night",
            "Drinking water keeps your body happy",
            "Eating vegetables helps you grow healthy and strong",
            "Exercise and play keep you strong",
            "Getting good sleep makes you strong and happy",
            "Keeping your room tidy helps you find things",
            "Putting toys away keeps your space safe",
            "Wash your hands before eating and after playing",
            "Washing your face keeps you fresh and healthy",
        ],
        "🚦 Safety": [
            "Always wear your seatbelt",
            "Ask before talking to strangers",
            "Look both ways before crossing the street",
            "Stay close to a trusted adult in public",
            "Wear a helmet when riding a bike or scooter",
        ],
    }
    const lessonsByCategorySorted = Object.fromEntries(
        Object.keys(lessonsByCategory)
            .sort((a, b) => a.localeCompare(b))
            .map((category) => [category, sortedUnique(lessonsByCategory[category])])
    )

    const allPresetLessons = Object.values(lessonsByCategorySorted).flat()
    const isPresetLesson = (val) => allPresetLessons.includes(val)

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
        gender: sortedUnique(["boy", "girl"]), // keep just boy/girl

        // Alphabetically sorted style options
        ethnicity: sortedUnique([
            "Asian",
            "Black",
            "Hispanic",
            "Middle Eastern",
            "Multiracial",
            "Native American",
            "Pacific Islander",
            "White",
        ]),
        skinTonesByEthnicity: {
            default: sortedUnique(["deep", "fair", "light", "medium", "olive", "tan", "warm"]),
            asian: sortedUnique(["fair", "golden", "light", "medium", "olive", "tan", "warm"]),
            black: sortedUnique(["deep", "ebony", "espresso", "medium", "rich brown", "warm brown"]),
            hispanic: sortedUnique(["golden", "light", "medium", "olive", "tan", "warm", "warm brown"]),
            "middle eastern": sortedUnique(["golden", "light", "medium", "olive", "tan", "warm", "warm brown"]),
            multiracial: sortedUnique(["deep", "fair", "golden", "light", "medium", "olive", "tan", "warm"]),
            "native american": sortedUnique(["golden", "medium", "olive", "sun-kissed", "tan", "warm brown"]),
            "pacific islander": sortedUnique(["golden", "medium", "olive", "sun-kissed", "tan", "warm brown"]),
            white: sortedUnique(["fair", "freckled", "light", "medium", "olive", "pink", "tan"]),
        },
        hairColor: sortedUnique([
            "auburn", "black", "blonde", "blue", "brown", "burgundy", "copper",
            "dark brown", "dirty blonde", "gray", "green", "lavender", "light brown", "pink",
            "platinum", "purple", "red", "silver", "teal", "white",
        ]),
        eyeColor: sortedUnique(["amber", "blue", "brown", "gray", "green", "hazel", "silver", "violet"]),

        topWear: sortedUnique(["blouse", "hoodie", "long-sleeve shirt", "sweater", "tank top", "t-shirt", "tunic", "vest"]),
        bottomWear: sortedUnique(["capris", "jeans", "joggers", "leggings", "pants", "shorts", "skirt", "slacks"]),
        onePieceWear: sortedUnique(["dress", "jumpsuit", "overalls", "robe", "romper"]),
        topWearColor: sortedUnique(["black", "blue", "brown", "cream", "cyan", "gold", "gray", "green", "lavender", "maroon", "navy", "orange", "pink", "purple", "red", "silver", "teal", "white", "yellow"]),
        bottomWearColor: sortedUnique(["black", "blue", "brown", "charcoal", "cream", "gray", "green", "khaki", "navy", "olive", "orange", "pink", "purple", "red", "tan", "teal", "white", "yellow"]),
        onePieceColor: sortedUnique(["black", "blue", "blush", "brown", "cream", "gold", "gray", "green", "lavender", "navy", "orange", "peach", "pink", "purple", "red", "silver", "teal", "white", "yellow"]),
        shoeStyle: sortedUnique(["boots", "dress shoes", "flats", "high-tops", "loafers", "sandals", "slippers", "sneakers"]),
        shoeColor: sortedUnique(["black", "blue", "brown", "cream", "gold", "gray", "green", "navy", "orange", "pink", "purple", "red", "silver", "tan", "teal", "white", "yellow"]),

        hairstylesByGender: {
            boy: sortedUnique(["buzz cut", "crew cut", "messy", "mohawk", "short side part", "slicked back", "spiky"]),
            girl: sortedUnique(["bob cut", "braided", "bun", "crown braid", "double buns", "pigtails", "pixie cut", "ponytail"]),
            default: sortedUnique(["afro", "dreadlocks", "mohawk", "straight", "tied with ribbon", "wind-swept"]),
        },

        species: sortedUnique(["bear", "bird", "cat", "dinosaur", "dog", "dragon", "fox", "horse", "koala", "rabbit", "turtle"]),
        bodyCovering: sortedUnique(["feathers", "fur", "scales", "shell", "skin"]),
        bodyColor: sortedUnique([
            "black", "blue", "brown", "glowing", "golden", "gray",
            "green", "orange", "pink", "red", "spotted", "striped", "white",
        ]),

        humanAccessories: sortedUnique([
            "backpack", "boots", "bowtie", "bracelet", "cape", "crown",
            "glasses", "hat", "necklace", "scarf", "sneakers", "wand", "watch",
        ]),

        animalAccessories: sortedUnique([
            "armor", "bandana", "bell", "bow", "collar", "feather accessory",
            "ribbon", "tiny hat", "wing clips",
        ]),
    }

    const basicOptions = {
        eyeColor: sortedUnique(["amber", "blue", "brown", "gray", "green", "hazel", "violet"]),
        hairColor: sortedUnique([
            "auburn", "black", "blonde", "blue", "brown", "dark brown",
            "dirty blonde", "gray", "green", "light brown", "pink",
            "purple", "red", "white",
        ]),
        pantsColor: sortedUnique(["black", "blue", "brown", "gray", "green", "khaki", "white"]),
        shirtColor: sortedUnique(["black", "blue", "green", "orange", "pink", "purple", "red", "white", "yellow"]),
        shoeColor: sortedUnique(["black", "blue", "brown", "gold", "green", "pink", "red", "silver", "white", "yellow"]),
        skinTone: sortedUnique(["brown", "dark", "freckled", "light", "olive", "pale", "tan"]),
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
    ].sort()

    const handleCharacterChange = (index, key, value) => {
        const updated = [...characters]
        updated[index][key] = value
        setCharacters(updated)
    }

    const getSkinToneOptionsForEthnicity = (ethnicityValue) => {
        const ethnicityKey = (ethnicityValue || "").trim().toLowerCase()
        return defaultOptions.skinTonesByEthnicity[ethnicityKey] || defaultOptions.skinTonesByEthnicity.default
    }

    const handleFieldChange = (index, field, value) => {
        const updated = [...characters]
        updated[index].descriptionFields[field] = value

        if (field === "ethnicity") {
            const nextSkinTones = getSkinToneOptionsForEthnicity(value)
            const selectedSkinTone = (updated[index].descriptionFields.skinTone || "").trim()
            const hasCustomSkinTone = ((updated[index].descriptionFields.skinToneCustom || "").trim()).length > 0
            if (selectedSkinTone && !hasCustomSkinTone && !nextSkinTones.includes(selectedSkinTone)) {
                updated[index].descriptionFields.skinTone = ""
            }
        }

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
            descriptionFields: (() => {
                const normalized = normalizeDescFields(c.descriptionFields)
                const onePieceSelected = (normalized.onePieceWear || "").trim().length > 0
                const keepTopWithOnePiece = onePieceAllowsTopLayer(normalized.onePieceWear || "")

                if (onePieceSelected) {
                    delete normalized.bottomWear
                    delete normalized.bottomWearColor

                    if (!keepTopWithOnePiece) {
                        delete normalized.topWear
                        delete normalized.topWearColor
                    }
                }

                if (!normalized.shirtColor && normalized.topWearColor) {
                    normalized.shirtColor = normalized.topWearColor
                }
                if (!normalized.pantsColor && normalized.bottomWearColor) {
                    normalized.pantsColor = normalized.bottomWearColor
                }

                return normalized
            })(),
        }))

        const finalArtStyle = isFree ? "watercolor" : artStyle

        const trimmedLesson = (lesson ?? "").trim()
        const lessonForRequest =
            includeLesson && trimmedLesson ? trimmedLesson : null

        // Shape expected by backend StoryRequest
        const request = {
            theme,
            readingLevel,
            artStyle: finalArtStyle,
            characters: processedCharacters,
            lessonLearned: lessonForRequest,
            lengthHintEnabled,
        }

        if (lengthHintEnabled) {
            request.storyLength = storyLength
        }

        onSubmit(request)
    }

    const renderDropdownWithCustom = (
        index,
        field,
        label,
        options = [],
        disabled = false
    ) => {
        const customValue = characters[index].descriptionFields[field + "Custom"] || ""
        const isFieldDisabled = disabled || !!customValue

        return (
            <div className="field-group">
                <label className="field-label">{label}</label>
                <div className="dual-input-container">
                    <select
                        value={characters[index].descriptionFields[field] || ""}
                        onChange={(e) => handleFieldChange(index, field, e.target.value)}
                        className="form-select"
                        disabled={isFieldDisabled}
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
                        disabled={disabled}
                    />
                </div>
            </div>
        )
    }

    // Check if user can add more characters
    const canAddMoreCharacters = membership !== "free" || characters.length < 1
    const isAtCharacterLimit = membership === "free" && characters.length >= 1

    return (
        <form onSubmit={handleSubmit} className="story-form">
            {/* Reading/Art section */}
            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">
                        <img
                            src={readingIcon}
                            alt="Reading level and art style"
                            className="section-icon-img"
                        />
                    </span>
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
                            <p className="style-lock-hint">
                                Watercolor is included on the Free plan.{" "}
                                <a href="/upgrade">Upgrade</a> to unlock every style.
                            </p>
                        )}
                    </div>
                </div>
            </div>

            {lengthHintEnabled && (
                <div className="field-group">
                    <label className="field-label">Story Length</label>
                    <select
                        className="form-select"
                        value={isLengthAllowed(storyLength) ? storyLength : "short"}
                        onChange={(e) => setStoryLength(e.target.value)}
                    >
                        <option value="short">Short (about 4 pages)</option>
                        <option
                            value="medium"
                            disabled={membership === "free"}
                        >
                            {membership === "free"
                                ? "🔒 Medium (upgrade for 8 pages)"
                                : "Medium (about 8 pages)"}
                        </option>
                        <option
                            value="long"
                            disabled={membership !== "premium"}
                        >
                            {membership === "premium"
                                ? "Long (about 12 pages)"
                                : "🔒 Long (premium only)"}
                        </option>
                    </select>
                    {membership !== "premium" && (
                        <p className="style-lock-hint">
                            Choose longer stories by upgrading your plan.
                        </p>
                    )}
                </div>
            )}

            {/* Lesson Learned (two-step) */}
            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">
                        <img
                            src={lessonIcon}
                            alt="Lesson learned"
                            className="section-icon-img"
                        />
                    </span>
                    Lesson Learned (Optional)
                </h3>

                {/* Toggle to include/exclude lesson */}
                <div className="field-group">
                    <label className="toggle-label">
                        <input
                            type="checkbox"
                            className="toggle-checkbox"
                            checked={includeLesson}
                            onChange={(e) => {
                                const checked = e.target.checked
                                setIncludeLesson(checked)
                                if (!checked) {
                                    setSelectedCategory("")
                                    setLesson("")
                                }
                            }}
                        />
                        <span className="toggle-slider" />
                        <span className="toggle-text">
                            Include a life lesson in this story
                        </span>
                    </label>
                    <p className="field-hint">
                        Turn this on if you want the story to teach something specific. Leave it off for a purely fun adventure.
                    </p>
                </div>

                <div className="field-group">
                    <label className="field-label">What lesson should the story teach?</label>

                    <div className="dual-input-container">
                        <select
                            className="form-select"
                            value={selectedCategory}
                            onChange={(e) => {
                                setSelectedCategory(e.target.value)
                                setLesson("") // reset when switching category
                            }}
                            disabled={!includeLesson}
                        >
                            <option value="" disabled>
                                Select Category
                            </option>
                            {Object.keys(lessonsByCategorySorted).map((cat) => (
                                <option key={cat} value={cat}>{cat}</option>
                            ))}
                        </select>

                        <input
                            placeholder="Or enter your own lesson"
                            value={!isPresetLesson(lesson) ? lesson : ""}
                            onChange={(e) => setLesson(e.target.value)}
                            className="form-input"
                            disabled={!includeLesson}
                        />
                    </div>

                    {selectedCategory && (
                        <div className="dual-input-container" style={{ marginTop: "0.5rem" }}>
                            <select
                                className="form-select"
                                value={isPresetLesson(lesson) ? lesson : ""}
                                onChange={(e) => setLesson(e.target.value)}
                                disabled={
                                    !includeLesson ||
                                    (!!(!isPresetLesson(lesson) && lesson.trim() !== ""))
                                }
                            >
                                <option value="" disabled>
                                    Select Category
                                </option>
                                {lessonsByCategorySorted[selectedCategory].map((l) => (
                                    <option key={l} value={l}>{l}</option>
                                ))}
                            </select>
                        </div>
                    )}
                </div>
            </div>

            <div className="form-section">
                <h3 className="section-title">
                    <span className="section-icon">
                        <img
                            src={themeIcon}
                            alt="Story theme"
                            className="section-icon-img"
                        />
                    </span>
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
                            <span className="character-icon">
                                <img
                                    src={char.isAnimal ? animalIcon : personIcon}
                                    alt={char.isAnimal ? "Animal character" : "Human character"}
                                    className="character-icon-img"
                                />
                            </span>
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
                                    <option value="dad">Dad</option>
                                    <option value="friend">Friend</option>
                                    <option value="mom">Mom</option>
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

                    {showCharacterTypeAndExtraButton && (
                        <div className="character-type-toggle">
                            <label className="toggle-label">
                                <input
                                    type="checkbox"
                                    checked={char.isAnimal}
                                    onChange={(e) => handleCharacterChange(i, "isAnimal", e.target.checked)}
                                    className="toggle-checkbox"
                                />
                                <span className="toggle-slider"></span>
                                <span className="toggle-text">
                                    {char.isAnimal ? "🐾 Animal Character" : "👤 Human Character"}
                                </span>
                            </label>
                        </div>
                    )}

                    {i === 0 && (
                        <div className="field-group" style={{ marginTop: "1rem" }}>
                            <label className="toggle-label">
                                <input
                                    type="checkbox"
                                    className="toggle-checkbox"
                                    checked={showAdvancedOptions}
                                    onChange={(e) => setShowAdvancedOptions(e.target.checked)}
                                />
                                <span className="toggle-slider" />
                                <span className="toggle-text">
                                    Show advanced character options
                                </span>
                            </label>
                        </div>
                    )}

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
                                    const genderDropdown = (char.descriptionFields.gender || "").trim()
                                    const genderCustom = (char.descriptionFields.genderCustom || "").trim()
                                    const isStandard = genderDropdown === "boy" || genderDropdown === "girl"

                                    let hairOptions = []
                                    if (isStandard) {
                                        hairOptions = defaultOptions.hairstylesByGender[genderDropdown]
                                    } else if (genderCustom) {
                                        // Custom gender → use neutral/default hairstyles
                                        hairOptions = defaultOptions.hairstylesByGender.default
                                    } else {
                                        hairOptions = []
                                    }

                                    return hairOptions.length > 0
                                        ? renderDropdownWithCustom(i, "hairStyle", "Hair Style", hairOptions)
                                        : null
                                })()}

                                {(() => {
                                    const hasGenderSelection =
                                        ((char.descriptionFields.gender || "").trim()).length > 0 ||
                                        ((char.descriptionFields.genderCustom || "").trim()).length > 0
                                    const hasOnePieceOutfit =
                                        ((char.descriptionFields.onePieceWear || "").trim()).length > 0 ||
                                        ((char.descriptionFields.onePieceWearCustom || "").trim()).length > 0
                                    const onePieceValue =
                                        (char.descriptionFields.onePieceWearCustom || "").trim() ||
                                        (char.descriptionFields.onePieceWear || "").trim()
                                    const allowsTopLayer = onePieceAllowsTopLayer(onePieceValue)
                                    const disableTopFields = hasOnePieceOutfit && !allowsTopLayer
                                    const disableBottomFields = hasOnePieceOutfit

                                    return (
                                        <>
                                            {showAdvancedOptions ? (
                                                <>
                                                    {renderDropdownWithCustom(i, "ethnicity", "Ethnicity (optional)", defaultOptions.ethnicity)}
                                                    {hasGenderSelection
                                                        ? renderDropdownWithCustom(i, "hairColor", "Hair Color", defaultOptions.hairColor)
                                                        : null}
                                                    {renderDropdownWithCustom(
                                                        i,
                                                        "skinTone",
                                                        "Skin Tone",
                                                        getSkinToneOptionsForEthnicity(char.descriptionFields.ethnicity)
                                                    )}
                                                    {renderDropdownWithCustom(i, "eyeColor", "Eye Color", defaultOptions.eyeColor)}
                                                    {renderDropdownWithCustom(i, "onePieceWear", "One-Piece Outfit (optional)", defaultOptions.onePieceWear)}
                                                    {renderDropdownWithCustom(i, "onePieceColor", "One-Piece Color", defaultOptions.onePieceColor)}
                                                    {renderDropdownWithCustom(i, "topWear", "Top", defaultOptions.topWear, disableTopFields)}
                                                    {renderDropdownWithCustom(i, "topWearColor", "Top Color", defaultOptions.topWearColor, disableTopFields)}
                                                    {renderDropdownWithCustom(i, "bottomWear", "Bottom", defaultOptions.bottomWear, disableBottomFields)}
                                                    {renderDropdownWithCustom(i, "bottomWearColor", "Bottom Color", defaultOptions.bottomWearColor, disableBottomFields)}
                                                    {renderDropdownWithCustom(i, "shoeStyle", "Shoe Style", defaultOptions.shoeStyle)}
                                                    {renderDropdownWithCustom(i, "shoeColor", "Shoe Color", defaultOptions.shoeColor)}
                                                </>
                                            ) : (
                                                <>
                                                    {hasGenderSelection
                                                        ? renderDropdownWithCustom(i, "hairColor", "Hair Color", basicOptions.hairColor)
                                                        : null}
                                                    {renderDropdownWithCustom(i, "skinTone", "Skin Tone", basicOptions.skinTone)}
                                                    {renderDropdownWithCustom(i, "eyeColor", "Eye Color", basicOptions.eyeColor)}
                                                    {renderDropdownWithCustom(i, "shirtColor", "Shirt Color", basicOptions.shirtColor)}
                                                    {renderDropdownWithCustom(i, "pantsColor", "Pants Color", basicOptions.pantsColor)}
                                                    {renderDropdownWithCustom(i, "shoeColor", "Shoe Color", basicOptions.shoeColor)}
                                                </>
                                            )}
                                        </>
                                    )
                                })()}
                                {renderDropdownWithCustom(i, "accessory", "Accessory (optional)", defaultOptions.humanAccessories)}
                            </>
                        )}
                    </div>
                </div>
            ))}

            <div className="form-actions">
                {showCharacterTypeAndExtraButton && (
                    canAddMoreCharacters ? (
                        <button type="button" onClick={addCharacter} className="add-character-btn">
                            <span className="button-icon">➕</span>
                            <span>Add Another Character</span>
                        </button>
                    ) : (
                        isAtCharacterLimit && (
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
                        )
                    )
                )}

                <button type="submit" className="generate-story-btn">
                    <span className="button-icon">
                        <img
                            src={sparkleIcon}
                            alt=""
                            aria-hidden="true"
                            className="button-icon-img"
                        />
                    </span>
                    <span>Generate My Story</span>
                </button>
            </div>
        </form>
    )
}

export default StoryForm
