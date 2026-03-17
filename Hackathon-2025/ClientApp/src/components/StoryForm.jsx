"use client"

import { Fragment, useState, useEffect, useRef } from "react"
import { useAuth } from "../context/AuthContext"
import api from "../api"
import "./StoryForm.css"

import themeIcon from "../assets/ui-icons/theme.png"
import lessonIcon from "../assets/ui-icons/lesson.png"
import readingIcon from "../assets/ui-icons/reading.png"
import personIcon from "../assets/ui-icons/person.png"
import sparkleIcon from "../assets/ui-icons/sparkle.png"
//import animalIcon from "../assets/ui-icons/animal.png"

const createEmptyCharacter = (role = "main") => ({
    role,
    roleCustom: "",
    name: "",
    isAnimal: false,
    descriptionFields: {},
})

const cloneCharacter = (character) => JSON.parse(JSON.stringify(character))
const MAX_CHARACTERS_PER_STORY = 1
const MAX_ACCESSORIES_PER_CHARACTER = 5
const ACCESSORY_FIELDS = Array.from({ length: MAX_ACCESSORIES_PER_CHARACTER }, (_, index) =>
    index === 0 ? "accessory" : `accessory${index + 1}`
)

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

const getSavedDescriptionFields = (character = {}) =>
    character.descriptionFields ||
    character.DescriptionFields ||
    {}

const normalizeSavedCharacter = (character = {}) => ({
    ...character,
    role: character.role || character.Role || "main",
    roleCustom: character.roleCustom || character.RoleCustom || "",
    name: character.name || character.Name || "",
    isAnimal: Boolean(
        typeof character.isAnimal === "boolean"
            ? character.isAnimal
            : character.IsAnimal
    ),
    descriptionFields: getSavedDescriptionFields(character),
})

const prepareCharacterForSubmit = (character) => {
    const normalized = normalizeDescFields(getSavedDescriptionFields(character))
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

    return {
        ...normalizeSavedCharacter(character),
        role: character.roleCustom?.trim() ? character.roleCustom.trim() : (character.role || "main"),
        descriptionFields: normalized,
    }
}

const normalizeSavedCharacterList = (items = []) =>
    items
        .map((item) => {
            const parsed = item?.character
            if (!parsed || typeof parsed !== "object") return null
            const normalizedCharacter = normalizeSavedCharacter(parsed)
            return {
                id: item.id,
                name: item.name || normalizedCharacter.name || "Unnamed Character",
                character: normalizedCharacter,
                updatedAtUtc: item.updatedAtUtc || null,
            }
        })
        .filter(Boolean)

const hasValue = (value) => String(value || "").trim().length > 0

const getAccessoryFieldCountForCharacter = (character = {}) => {
    const fields = getSavedDescriptionFields(character)
    let count = 1

    ACCESSORY_FIELDS.forEach((field, index) => {
        if (hasValue(fields[field]) || hasValue(fields[`${field}Custom`])) {
            count = index + 1
        }
    })

    return count
}

const getAccessoryFieldCounts = (characterList = []) =>
    characterList.map((character) => getAccessoryFieldCountForCharacter(character))

const getCharacterValidationResult = (character, { advanced }) => {
    if (!hasValue(character?.name)) {
        return { field: "name", message: "Please enter a character name." }
    }

    const fields = getSavedDescriptionFields(character)

    if (character?.isAnimal) {
        if (!hasValue(fields.species) && !hasValue(fields.speciesCustom)) {
            return { field: "species", message: "Please choose a species for the character." }
        }
        if (!hasValue(fields.bodyCovering) && !hasValue(fields.bodyCoveringCustom)) {
            return { field: "bodyCovering", message: "Please choose a body covering for the character." }
        }
        if (!hasValue(fields.bodyColor) && !hasValue(fields.bodyColorCustom)) {
            return { field: "bodyColor", message: "Please choose a body color for the character." }
        }

        return null
    }

    if (!hasValue(fields.age) && !hasValue(fields.ageCustom)) {
        return { field: "age", message: "Please choose an age for the character." }
    }
    if (!hasValue(fields.gender) && !hasValue(fields.genderCustom)) {
        return { field: "gender", message: "Please choose a gender for the character." }
    }
    if (!hasValue(fields.skinTone) && !hasValue(fields.skinToneCustom)) {
        return { field: "skinTone", message: "Please choose a skin tone for the character." }
    }
    if (!hasValue(fields.eyeColor) && !hasValue(fields.eyeColorCustom)) {
        return { field: "eyeColor", message: "Please choose an eye color for the character." }
    }

    const hasOnePiece = hasValue(fields.onePieceWear) || hasValue(fields.onePieceWearCustom)
    if (advanced) {
        if (hasOnePiece && !hasValue(fields.onePieceColor) && !hasValue(fields.onePieceColorCustom)) {
            return { field: "onePieceColor", message: "Please choose a one-piece color for the character." }
        }

        const hasTop = hasValue(fields.topWear) || hasValue(fields.topWearCustom)
        const hasBottom = hasValue(fields.bottomWear) || hasValue(fields.bottomWearCustom)
        const allowsTopLayer = onePieceAllowsTopLayer(fields.onePieceWearCustom || fields.onePieceWear || "")

        if (!hasOnePiece && !hasTop) {
            return { field: "topWear", message: "Please choose a top or a one-piece outfit for the character." }
        }
        if (hasTop && !hasValue(fields.topWearColor) && !hasValue(fields.topWearColorCustom)) {
            return { field: "topWearColor", message: "Please choose a top color for the character." }
        }
        if (!hasOnePiece && !hasBottom) {
            return { field: "bottomWear", message: "Please choose a bottom for the character." }
        }
        if (!hasOnePiece && hasBottom && !hasValue(fields.bottomWearColor) && !hasValue(fields.bottomWearColorCustom)) {
            return { field: "bottomWearColor", message: "Please choose a bottom color for the character." }
        }
        if (hasOnePiece && allowsTopLayer && hasTop && !hasValue(fields.topWearColor) && !hasValue(fields.topWearColorCustom)) {
            return { field: "topWearColor", message: "Please choose a top color for the layered outfit." }
        }
        if (!hasValue(fields.shoeColor) && !hasValue(fields.shoeColorCustom)) {
            return { field: "shoeColor", message: "Please choose a shoe color for the character." }
        }
    } else {
        if (!hasValue(fields.shirtColor) && !hasValue(fields.shirtColorCustom)) {
            return { field: "shirtColor", message: "Please choose a shirt color for the character." }
        }
        if (!hasValue(fields.pantsColor) && !hasValue(fields.pantsColorCustom)) {
            return { field: "pantsColor", message: "Please choose a pants color for the character." }
        }
        if (!hasValue(fields.shoeColor) && !hasValue(fields.shoeColorCustom)) {
            return { field: "shoeColor", message: "Please choose a shoe color for the character." }
        }
    }

    return null
}

const StoryForm = ({ onSubmit }) => {
    const { user } = useAuth()
    const formRef = useRef(null)

    // Normalize membership to a lowercase string
    const membershipRaw = user?.membership ?? "free"
    const membership = String(membershipRaw).toLowerCase()
    const isFree = membership === "free"
    const canUseAdvancedCharacterCreation = membership !== "free"

    // Feature flag: hide the extra UI for now
    const showCharacterTypeAndExtraButton = false

    const [readingLevel, setReadingLevel] = useState("early") // "pre" | "early" | "independent"
    const [artStyle, setArtStyle] = useState("watercolor")
    const [theme, setTheme] = useState("")
    const [lesson, setLesson] = useState("")
    const [selectedCategory, setSelectedCategory] = useState("")
    const [includeLesson, setIncludeLesson] = useState(false)
    const [showAdvancedOptions, setShowAdvancedOptions] = useState(false)
    const [savedCharacters, setSavedCharacters] = useState([])
    const [selectedSavedCharacterId, setSelectedSavedCharacterId] = useState("")
    const [editingSavedCharacterId, setEditingSavedCharacterId] = useState(null)
    const [savedCharacterLimit, setSavedCharacterLimit] = useState(5)
    const [savedCharacterNotice, setSavedCharacterNotice] = useState("")
    const [isUsingSavedCharacter, setIsUsingSavedCharacter] = useState(false)
    const [isSavingCharacter, setIsSavingCharacter] = useState(false)
    const [isDeletingCharacter, setIsDeletingCharacter] = useState(false)

    const [lengthHintEnabled, setLengthHintEnabled] = useState(false)
    const API_BASE = import.meta.env.VITE_API_BASE ?? ""
    const [storyLength, setStoryLength] = useState("short")

    const clearValidationMessage = (event) => {
        event.target.setCustomValidity("")
    }

    const showValidationPopup = (field, message, { reopenCharacterEditor = true } = {}) => {
        if (reopenCharacterEditor) {
            setIsUsingSavedCharacter(false)
        }

        requestAnimationFrame(() => {
            const form = formRef.current
            if (!form) return

            const target = form.querySelector(`[data-field="${field}"]`)
            if (!target) return

            target.setCustomValidity(message)
            target.reportValidity()
            target.focus()
        })
    }

    useEffect(() => {
        if (!canUseAdvancedCharacterCreation) {
            setShowAdvancedOptions(false)
        }
    }, [canUseAdvancedCharacterCreation])

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

    const [characters, setCharacters] = useState([createEmptyCharacter("main")])
    const [accessoryFieldCounts, setAccessoryFieldCounts] = useState([1])

    const applySavedCharactersResponse = (data, preferredSavedCharacterId = null) => {
        const normalizedItems = normalizeSavedCharacterList(Array.isArray(data?.items) ? data.items : [])
        const preferredId = preferredSavedCharacterId != null ? String(preferredSavedCharacterId) : null

        setSavedCharacters(normalizedItems)
        setSavedCharacterLimit(Number(data?.maxSavedCharacters) || 5)
        setSelectedSavedCharacterId((prev) => {
            if (normalizedItems.length === 0) return ""
            if (preferredId && normalizedItems.some((x) => String(x.id) === preferredId)) {
                return preferredId
            }

            const prevId = String(prev)
            return normalizedItems.some((x) => String(x.id) === prevId)
                ? prevId
                : String(normalizedItems[0].id)
        })
        setSavedCharacterNotice(
            data?.isOverLimit
                ? data?.downgradePolicy || "You are over your current saved character limit. Delete down before saving a new one."
                : ""
        )

        return normalizedItems
    }

    const refreshSavedCharacters = async (preferredSavedCharacterId = null) => {
        const { data } = await api.get("/saved-character/me")
        return applySavedCharactersResponse(data, preferredSavedCharacterId)
    }

    useEffect(() => {
        let isMounted = true

        const loadSavedCharacters = async () => {
            try {
                const { data } = await api.get("/saved-character/me")
                if (!isMounted) return
                applySavedCharactersResponse(data)
                setIsUsingSavedCharacter(false)
                setEditingSavedCharacterId(null)
            } catch (err) {
                if (err?.response?.status !== 404) {
                    console.error("Failed to load saved character:", err)
                }
                if (!isMounted) return
                setSavedCharacters([])
                setSelectedSavedCharacterId("")
                setIsUsingSavedCharacter(false)
                setEditingSavedCharacterId(null)
            }
        }

        loadSavedCharacters()
        return () => {
            isMounted = false
        }
    }, [])

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
            "backpack", "bowtie", "bracelet", "cape", "crown",
            "glasses", "hair bow", "headband", "hat", "necklace",
            "satchel", "scarf", "wand", "watch",
        ]),

        animalAccessories: sortedUnique([
            "bandana", "bell", "bow", "collar", "flower crown",
            "harness", "ribbon", "saddle", "tiny hat",
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
        "Arctic Expedition",
        "Backyard Safari",
        "Cloud Kingdom",
        "Cozy Village Adventure",
        "Crystal Caves",
        "Dragon Mountain",
        "Farmyard Friends",
        "Haunted-but-Friendly Mansion",
        "Hidden Treehouse Club",
        "Island Discovery",
        "Lost-and-Found Mystery",
        "Moonlight Train Journey",
        "Music Festival in the Park",
        "Mythical Creatures Academy",
        "Neighborhood Hero Adventure",
        "Ocean Rescue Mission",
        "Rainy Day Detective",
        "Rainbow Bridge Quest",
        "Silly Science Lab",
        "Sky Pirates",
        "Sports Day Challenge",
        "Superhero Training Camp",
        "Treasure Map in the Attic",
        "Volcano Explorer",
        "Wild West Wagon Trail",
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

    const addAccessoryField = (index) => {
        setAccessoryFieldCounts((current) => {
            const next = [...current]
            const existingCount = next[index] ?? getAccessoryFieldCountForCharacter(characters[index])
            next[index] = Math.min(MAX_ACCESSORIES_PER_CHARACTER, existingCount + 1)
            return next
        })
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
        setAccessoryFieldCounts([
            ...accessoryFieldCounts,
            1,
        ])
    }

    const removeCharacter = (index) => {
        const updated = [...characters]
        updated.splice(index, 1)
        setCharacters(updated)
        const nextAccessoryCounts = [...accessoryFieldCounts]
        nextAccessoryCounts.splice(index, 1)
        setAccessoryFieldCounts(nextAccessoryCounts.length ? nextAccessoryCounts : [1])
    }

    const handleSaveCharacter = async () => {
        const mainCharacter = characters[0]
        const validationResult = getCharacterValidationResult(mainCharacter, {
            advanced: canUseAdvancedCharacterCreation && showAdvancedOptions,
        })
        if (validationResult) {
            showValidationPopup(validationResult.field, validationResult.message)
            return
        }

        const toSave = cloneCharacter(prepareCharacterForSubmit({
            ...mainCharacter,
            role: "main",
            roleCustom: "",
        }))

        const isCreatingNew = !editingSavedCharacterId
        if (isCreatingNew && savedCharacters.length >= savedCharacterLimit) {
            setSavedCharacterNotice(`You can only save up to ${savedCharacterLimit} characters. Delete one to save a new character.`)
            return
        }

        try {
            setIsSavingCharacter(true)
            setSavedCharacterNotice("")

            const response = editingSavedCharacterId
                ? await api.put(`/saved-character/me/${editingSavedCharacterId}`, { character: toSave })
                : await api.post("/saved-character/me", { character: toSave })

            const savedItem = response?.data
                ? {
                    id: response.data.id,
                    name: response.data.name || toSave.name || "Unnamed Character",
                    character: cloneCharacter(normalizeSavedCharacter(response.data.character || toSave)),
                    updatedAtUtc: response.data.updatedAtUtc || null,
                }
                : null

            if (savedItem) {
                const refreshedItems = await refreshSavedCharacters(savedItem.id)
                const refreshedSelected = refreshedItems.find((x) => x.id === savedItem.id) || savedItem
                setSelectedSavedCharacterId(String(refreshedSelected.id))
                setCharacters([cloneCharacter(refreshedSelected.character)])
                setAccessoryFieldCounts(getAccessoryFieldCounts([refreshedSelected.character]))
                setEditingSavedCharacterId(refreshedSelected.id)
            } else {
                setCharacters([cloneCharacter(toSave)])
                setAccessoryFieldCounts(getAccessoryFieldCounts([toSave]))
                setEditingSavedCharacterId(editingSavedCharacterId)
            }
            setIsUsingSavedCharacter(true)
        } catch (err) {
            console.error("Failed to save character:", err)
            const apiMessage = err?.response?.data?.message
            if (apiMessage) {
                setSavedCharacterNotice(apiMessage)
            } else {
                alert("We couldn't save this character right now. Please try again.")
            }
        } finally {
            setIsSavingCharacter(false)
        }
    }

    const loadSelectedSavedCharacter = (savedCharacterId) => {
        const selected = savedCharacters.find((x) => String(x.id) === String(savedCharacterId))
        if (!selected) return
        setCharacters([cloneCharacter(selected.character)])
        setAccessoryFieldCounts(getAccessoryFieldCounts([selected.character]))
        setEditingSavedCharacterId(selected.id)
        setSavedCharacterNotice("")
        setIsUsingSavedCharacter(true)
    }

    const handleLoadSavedCharacter = () => {
        loadSelectedSavedCharacter(selectedSavedCharacterId)
    }

    const handleSelectedSavedCharacterChange = (nextSavedCharacterId) => {
        setSelectedSavedCharacterId(nextSavedCharacterId)

        if (isUsingSavedCharacter) {
            loadSelectedSavedCharacter(nextSavedCharacterId)
        }
    }

    const handleDeleteSavedCharacter = async () => {
        const selected = savedCharacters.find((x) => String(x.id) === String(selectedSavedCharacterId))
        if (!selected) return

        try {
            setIsDeletingCharacter(true)
            setSavedCharacterNotice("")
            await api.delete(`/saved-character/me/${selected.id}`)
        } catch (err) {
            console.error("Failed to delete saved character:", err)
            const apiMessage = err?.response?.data?.message
            if (apiMessage) setSavedCharacterNotice(apiMessage)
        } finally {
            setIsDeletingCharacter(false)
        }

        const nextCharacters = await refreshSavedCharacters()
        setSelectedSavedCharacterId(nextCharacters.length ? String(nextCharacters[0].id) : "")

        if (isUsingSavedCharacter && editingSavedCharacterId === selected.id) {
            setIsUsingSavedCharacter(false)
            setCharacters([createEmptyCharacter("main")])
            setAccessoryFieldCounts([1])
        }
        if (editingSavedCharacterId === selected.id) {
            setEditingSavedCharacterId(null)
        }
    }

    const handleEditSavedCharacter = () => {
        const selected = savedCharacters.find((x) => String(x.id) === String(selectedSavedCharacterId))
        if (!selected) return
        setCharacters([cloneCharacter(selected.character)])
        setAccessoryFieldCounts(getAccessoryFieldCounts([selected.character]))
        setEditingSavedCharacterId(selected.id)
        setSavedCharacterNotice("")
        setIsUsingSavedCharacter(false)
    }

    const handleStartNewCharacter = () => {
        setEditingSavedCharacterId(null)
        setCharacters([createEmptyCharacter("main")])
        setAccessoryFieldCounts([1])
        setSavedCharacterNotice("")
        setIsUsingSavedCharacter(false)
    }

    const handleSubmit = (e) => {
        e.preventDefault()

        if (!theme.trim()) {
            showValidationPopup("theme", "Please choose or enter a story theme.", {
                reopenCharacterEditor: false,
            })
            return
        }

        const validationResult = characters
            .map((character) => getCharacterValidationResult(character, {
                advanced: canUseAdvancedCharacterCreation && showAdvancedOptions,
            }))
            .find(Boolean)

        if (validationResult) {
            showValidationPopup(validationResult.field, validationResult.message)
            return
        }

        const processedCharacters = characters
            .map((c) => prepareCharacterForSubmit(c))
            .slice(0, MAX_CHARACTERS_PER_STORY)

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
        const selectedValue = characters[index].descriptionFields[field] || ""
        const customValue = characters[index].descriptionFields[field + "Custom"] || ""
        const isFieldDisabled = disabled || !!customValue
        const displaySelectValue = customValue ? "" : selectedValue
        const selectClassName = `form-select${displaySelectValue ? " is-filled" : ""}`
        const inputClassName = `form-input${customValue ? " is-filled" : ""}`

        return (
            <div className="field-group">
                <label className="field-label">{label}</label>
                <div className="dual-input-container">
                    <select
                        data-field={field}
                        value={displaySelectValue}
                        onChange={(e) => {
                            clearValidationMessage(e)
                            handleFieldChange(index, field, e.target.value)
                        }}
                        className={selectClassName}
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
                        data-field={`${field}Custom`}
                        placeholder={`Or enter custom ${label.toLowerCase()}`}
                        value={customValue}
                        onChange={(e) => {
                            clearValidationMessage(e)
                            handleFieldChange(index, field + "Custom", e.target.value)
                        }}
                        className={inputClassName}
                        disabled={disabled}
                    />
                </div>
            </div>
        )
    }

    const renderAccessoryFields = (index, options) => {
        const visibleCount = Math.max(
            accessoryFieldCounts[index] ?? 1,
            getAccessoryFieldCountForCharacter(characters[index])
        )
        const fields = characters[index]?.descriptionFields || {}
        const lastVisibleField = ACCESSORY_FIELDS[Math.max(0, visibleCount - 1)]
        const canAddAnotherAccessory =
            visibleCount < MAX_ACCESSORIES_PER_CHARACTER &&
            (hasValue(fields[lastVisibleField]) || hasValue(fields[`${lastVisibleField}Custom`]))

        return (
            <>
                {ACCESSORY_FIELDS.slice(0, visibleCount).map((field, accessoryIndex) => (
                    <Fragment key={field}>
                        {renderDropdownWithCustom(
                            index,
                            field,
                            `Accessory ${accessoryIndex + 1} (optional)`,
                            options
                        )}
                    </Fragment>
                ))}
                {canAddAnotherAccessory ? (
                    <div className="field-group accessory-actions">
                        <button
                            type="button"
                            className="add-accessory-btn"
                            onClick={() => addAccessoryField(index)}
                        >
                            + Add accessory
                        </button>
                    </div>
                ) : null}
            </>
        )
    }

    const hasSavedCharacter = savedCharacters.length > 0
    const selectedSavedCharacter =
        savedCharacters.find((x) => String(x.id) === String(selectedSavedCharacterId)) || null
    const canSaveCharacter = !!characters[0]?.name?.trim()
    const isCreatingNewCharacter = !isUsingSavedCharacter && !editingSavedCharacterId

    return (
        <form
            ref={formRef}
            onSubmit={handleSubmit}
            onInvalidCapture={(e) => {
                const field = e.target?.dataset?.field || ""
                const isCharacterField =
                    field !== "theme" &&
                    field !== "" &&
                    field !== "lesson" &&
                    field !== "selectedCategory"

                if (isUsingSavedCharacter && isCharacterField) {
                    setIsUsingSavedCharacter(false)
                }
            }}
            className="story-form"
        >
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
                            <option value="gouache" disabled={isFree}>
                                {isFree ? "🔒 Gouache (storybook paint)" : "Gouache (storybook paint)"}
                            </option>
                            <option value="pastel" disabled={isFree}>
                                {isFree ? "🔒 Soft pastel (chalky)" : "Soft pastel (chalky)"}
                            </option>
                            <option value="lineart" disabled={isFree}>
                                {isFree ? "🔒 Clean line art (modern)" : "Clean line art (modern)"}
                            </option>
                            <option value="clay" disabled={isFree}>
                                {isFree ? "🔒 Clay animation (handmade)" : "Clay animation (handmade)"}
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
                        {(() => {
                            const selectedTheme = defaultThemes.includes(theme) ? theme : ""
                            const customTheme = !defaultThemes.includes(theme) ? theme : ""

                            return (
                                <>
                        <select
                            data-field="theme"
                            value={selectedTheme}
                            onChange={(e) => {
                                clearValidationMessage(e)
                                setTheme(e.target.value)
                            }}
                            className={`form-select${selectedTheme ? " is-filled" : ""}`}
                            disabled={theme.trim() !== "" && !defaultThemes.includes(theme)}
                        >
                            <option value="">Select Theme</option>
                            {defaultThemes.map((t) => (
                                <option key={t} value={t}>{t}</option>
                            ))}
                        </select>

                        <input
                            data-field="theme"
                            placeholder="Or create your own theme"
                            value={customTheme}
                            onChange={(e) => {
                                clearValidationMessage(e)
                                setTheme(e.target.value)
                            }}
                            className={`form-input${customTheme.trim() ? " is-filled" : ""}`}
                        />
                                </>
                            )
                        })()}
                    </div>
                </div>
            </div>

            <div className="form-section saved-character-section">
                <h3 className="section-title">
                    <span className="section-icon">
                        <img
                            src={personIcon}
                            alt="Saved character controls"
                            className="section-icon-img"
                        />
                    </span>
                    Saved Character
                </h3>
                {!hasSavedCharacter ? (
                    <p className="field-hint">
                        Save characters here. Your {membership === "premium" ? "Premium" : membership === "pro" ? "Pro" : "Free"} plan includes up to {savedCharacterLimit} saved character{savedCharacterLimit === 1 ? "" : "s"}.
                    </p>
                ) : (
                    <>
                        <div className="field-group">
                            <label className="field-label">Select saved character</label>
                            <select
                                className="form-select"
                                value={selectedSavedCharacterId}
                                onChange={(e) => handleSelectedSavedCharacterChange(e.target.value)}
                                disabled={isSavingCharacter || isDeletingCharacter}
                            >
                                {savedCharacters.map((item) => (
                                    <option key={item.id} value={String(item.id)}>
                                        {item.name}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <p className="saved-character-current-name">
                            Saved {savedCharacters.length} / {savedCharacterLimit}
                        </p>
                        <div className="saved-character-controls">
                            {!isUsingSavedCharacter ? (
                                <button
                                    type="button"
                                    className="load-character-btn"
                                    onClick={handleLoadSavedCharacter}
                                    disabled={isSavingCharacter || isDeletingCharacter}
                                >
                                    Load Saved Character
                                </button>
                            ) : (
                                <button
                                    type="button"
                                    className="edit-character-btn"
                                    onClick={handleEditSavedCharacter}
                                    disabled={isSavingCharacter || isDeletingCharacter}
                                >
                                    Edit Character
                                </button>
                            )}
                            {!isCreatingNewCharacter && (
                                <button
                                    type="button"
                                    className="load-character-btn"
                                    onClick={handleStartNewCharacter}
                                    disabled={isSavingCharacter || isDeletingCharacter}
                                >
                                    New Character
                                </button>
                            )}
                            <button
                                type="button"
                                className="delete-character-btn"
                                onClick={handleDeleteSavedCharacter}
                                disabled={isSavingCharacter || isDeletingCharacter}
                            >
                                {isDeletingCharacter ? "Deleting..." : "Delete Saved Character"}
                            </button>
                        </div>
                    </>
                )}
                {!!savedCharacterNotice && (
                    <p className="saved-character-notice">{savedCharacterNotice}</p>
                )}
            </div>

            <div className={`character-editor-panel ${isUsingSavedCharacter ? "collapsed" : ""}`}>
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
                            data-field="name"
                            placeholder="Enter character name"
                            value={char.name}
                            onChange={(e) => {
                                clearValidationMessage(e)
                                handleCharacterChange(i, "name", e.target.value)
                            }}
                            required
                            className={`form-input${char.name?.trim() ? " is-filled" : ""}`}
                        />
                    </div>

                    {i > 0 && (
                        <div className="field-group">
                            <label className="field-label">Character Role</label>
                            <div className="dual-input-container">
                                <select
                                    value={char.role}
                                    onChange={(e) => handleCharacterChange(i, "role", e.target.value)}
                                    className={`form-select${char.role ? " is-filled" : ""}`}
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
                                    className={`form-input${char.roleCustom?.trim() ? " is-filled" : ""}`}
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

                    {i === 0 && canUseAdvancedCharacterCreation && (
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
                    {i === 0 && !canUseAdvancedCharacterCreation && (
                        <div className="field-group" style={{ marginTop: "1rem" }}>
                            <p className="field-hint">
                                Advanced character creation is available on Pro and Premium. Free includes the standard character creator.
                            </p>
                        </div>
                    )}

                    <div className="character-details">
                        {char.isAnimal ? (
                            <>
                                {renderDropdownWithCustom(i, "species", "Species", defaultOptions.species)}
                                {renderDropdownWithCustom(i, "bodyCovering", "Body Covering", defaultOptions.bodyCovering)}
                                {renderDropdownWithCustom(i, "bodyColor", "Body Color", defaultOptions.bodyColor)}
                                {renderAccessoryFields(i, defaultOptions.animalAccessories)}
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
                                                    {hasOnePieceOutfit
                                                        ? renderDropdownWithCustom(i, "onePieceColor", "One-Piece Color", defaultOptions.onePieceColor)
                                                        : null}
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
                                {renderAccessoryFields(i, defaultOptions.humanAccessories)}
                            </>
                        )}
                    </div>
                </div>
                ))}
            </div>

            <div className={`character-card loaded-character-card ${isUsingSavedCharacter ? "visible" : ""}`}>
                <div className="field-group">
                    <label className="field-label">Loaded Character</label>
                    <p className="loaded-character-name">{characters[0]?.name || selectedSavedCharacter?.name || "Saved character"}</p>
                    <p className="field-hint">
                        Character creation is hidden while using this saved character. Click Edit Character to update and save again.
                    </p>
                </div>
            </div>

            <div className="form-actions">
                {!isUsingSavedCharacter && (
                    <button
                        type="button"
                        onClick={handleSaveCharacter}
                        className="save-character-btn"
                        disabled={!canSaveCharacter || isSavingCharacter || isDeletingCharacter}
                    >
                        <span>
                            {isSavingCharacter
                                ? "Saving..."
                                : editingSavedCharacterId
                                    ? "Save Changes"
                                    : "Save Character"}
                        </span>
                    </button>
                )}

                {!isUsingSavedCharacter && showCharacterTypeAndExtraButton && (
                    characters.length < MAX_CHARACTERS_PER_STORY ? (
                        <button type="button" onClick={addCharacter} className="add-character-btn">
                            <span className="button-icon">➕</span>
                            <span>Add Another Character</span>
                        </button>
                    ) : (
                        characters.length >= MAX_CHARACTERS_PER_STORY && (
                            <div className="character-limit-notice">
                                <div className="limit-icon">🔒</div>
                                <div className="limit-content">
                                    <h4>Single-character stories only</h4>
                                    <p>Multiple characters are currently disabled, so each story uses one main character.</p>
                                    <div className="limit-benefits">
                                        <span className="benefit">👤 One main character</span>
                                        <span className="benefit">💾 Saved character slots depend on plan</span>
                                        <span className="benefit">🎨 Advanced creator on paid plans</span>
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
