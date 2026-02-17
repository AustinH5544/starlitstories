// You can tweak defaults here or pass overrides per-call.
export const defaultRuleSet = {
    minLength: 8,
    requireUpper: true,
    requireLower: true,
    requireNumber: false,
    requireSpecial: true,
    disallowSpaces: true,
};

// Returns { requirements: { length, upper, ... }, allMet }
export function checkPassword(password, ruleSet = defaultRuleSet) {
    const {
        minLength,
        requireUpper,
        requireLower,
        requireNumber,
        requireSpecial,
        disallowSpaces,
    } = { ...defaultRuleSet, ...ruleSet };

    const requirements = {
        length: password.length >= minLength,
        upper: !requireUpper || /[A-Z]/.test(password),
        lower: !requireLower || /[a-z]/.test(password),
        number: !requireNumber || /\d/.test(password),
        special: !requireSpecial || /[^A-Za-z0-9]/.test(password),
        nospace: !disallowSpaces || !/\s/.test(password),
    };

    const allMet = Object.values(requirements).every(Boolean);
    return { requirements, allMet };
}

// Friendly labels you can reuse in checklists.
// You can localize/adjust these in one place.
export function requirementLabels(ruleSet = defaultRuleSet) {
    const rs = { ...defaultRuleSet, ...ruleSet };
    const labels = {};

    if (rs.minLength) labels.length = `At least ${rs.minLength} characters`;
    if (rs.requireUpper) labels.upper = "Contains an uppercase letter";
    if (rs.requireLower) labels.lower = "Contains a lowercase letter";
    if (rs.requireNumber) labels.number = "Contains a number";
    if (rs.requireSpecial) labels.special = "Contains a special character";
    if (rs.disallowSpaces) labels.nospace = "No spaces";

    return labels;
}
