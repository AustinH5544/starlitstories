// Defaults are easy to tweak or override per page
export const defaultUsernameRuleSet = {
    minLength: 3,
    maxLength: 24,
    // Allowed characters: letters, numbers, dot, underscore, hyphen
    charsetRegex: /^[A-Za-z0-9._-]+$/,
    // Disallow username starting/ending with ., _, or -
    disallowEdgePunct: true,
    // Disallow consecutive punctuation like "..", "__", "--", "._", "-."
    disallowConsecutivePunct: true,
    // Require at least one letter or digit (prevents usernames like "....")
    requireAlphaNum: true,
};

// Returns { requirements: { length, charset, edge, consecutive, alnum }, allMet }
export function checkUsername(username, ruleSet = defaultUsernameRuleSet) {
    const rs = { ...defaultUsernameRuleSet, ...ruleSet };
    const u = (username ?? "").trim();

    const length = u.length >= rs.minLength && u.length <= rs.maxLength;
    const charset = u.length === 0 ? false : rs.charsetRegex.test(u);
    const edge = !rs.disallowEdgePunct || (!/^[._-]/.test(u) && !/[._-]$/.test(u));
    const consecutive = !rs.disallowConsecutivePunct || !/[._-]{2,}/.test(u);
    const alnum = !rs.requireAlphaNum || /[A-Za-z0-9]/.test(u);

    const requirements = { length, charset, edge, consecutive, alnum };
    const allMet = Object.values(requirements).every(Boolean);
    return { requirements, allMet };
}

// Labels for the checklist
export function usernameRequirementLabels(ruleSet = defaultUsernameRuleSet) {
    const rs = { ...defaultUsernameRuleSet, ...ruleSet };
    return {
        length: `Between ${rs.minLength}–${rs.maxLength} characters`,
        charset: "Only letters, numbers, dot (.), underscore (_), or hyphen (-)",
        edge: "Doesn't start or end with ., _, or -",
        consecutive: "No consecutive punctuation (.., __, --, ._, -.)",
        alnum: "Contains at least one letter or number",
    };
}
