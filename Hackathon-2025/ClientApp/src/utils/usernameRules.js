export const defaultUsernameRuleSet = {
    minLength: 3,
    maxLength: 24,
    // Allow letters, numbers, dot, underscore, and hyphen
    charsetRegex: /^[A-Za-z0-9._-]+$/,
    // Don’t start or end with punctuation
    disallowEdgePunct: true,
    // Don’t allow obvious repeats of punctuation (like ".." or "__")
    disallowConsecutivePunct: true,
    // Must contain at least one letter
    requireLetter: true,
};

// Returns { requirements: { length, charset, edge, consecutive, letter }, allMet }
export function checkUsername(username, ruleSet = defaultUsernameRuleSet) {
    const rs = { ...defaultUsernameRuleSet, ...ruleSet };
    const u = (username ?? "").trim();

    const length = u.length >= rs.minLength && u.length <= rs.maxLength;
    const charset = u.length === 0 ? false : rs.charsetRegex.test(u);
    const edge = !rs.disallowEdgePunct || (!/^[._-]/.test(u) && !/[._-]$/.test(u));
    const consecutive = !rs.disallowConsecutivePunct || !/([._-])\1/.test(u); // simpler check
    const letter = !rs.requireLetter || /[A-Za-z]/.test(u);

    const requirements = { length, charset, edge, consecutive, letter };
    const allMet = Object.values(requirements).every(Boolean);
    return { requirements, allMet };
}

// Human-readable checklist labels
export function usernameRequirementLabels(ruleSet = defaultUsernameRuleSet) {
    const rs = { ...defaultUsernameRuleSet, ...ruleSet };
    return {
        length: `Between ${rs.minLength}–${rs.maxLength} characters`,
        charset: "Only letters, numbers, dot (.), underscore (_), or hyphen (-)",
        edge: "Doesn't start or end with punctuation",
        consecutive: "No repeating punctuation (.., __, --)",
        letter: "Contains at least one letter",
    };
}
