using Hackathon_2025.Models;

namespace Hackathon_2025.Services;

public static class MembershipEntitlements
{
    public const int MaxCharactersPerStory = 1;

    private static readonly HashSet<string> FreeCharacterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "accessory",
        "accessoryCustom",
        "age",
        "ageCustom",
        "bodyColor",
        "bodyColorCustom",
        "bodyCovering",
        "bodyCoveringCustom",
        "eyeColor",
        "eyeColorCustom",
        "gender",
        "genderCustom",
        "hairColor",
        "hairColorCustom",
        "hairStyle",
        "hairStyleCustom",
        "pantsColor",
        "pantsColorCustom",
        "shirtColor",
        "shirtColorCustom",
        "shoeColor",
        "shoeColorCustom",
        "skinTone",
        "skinToneCustom",
        "species",
        "speciesCustom"
    };

    public static int SavedCharacterLimitFor(MembershipPlan membership) => membership switch
    {
        MembershipPlan.Pro => 5,
        MembershipPlan.Premium => 10,
        _ => 1
    };

    public static bool SupportsAdvancedCharacterCreation(MembershipPlan membership) =>
        membership != MembershipPlan.Free;

    public static Dictionary<string, string> SanitizeDescriptionFields(
        MembershipPlan membership,
        IReadOnlyDictionary<string, string>? descriptionFields)
    {
        if (descriptionFields is null || descriptionFields.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (SupportsAdvancedCharacterCreation(membership))
        {
            return descriptionFields
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        return descriptionFields
            .Where(kvp =>
                !string.IsNullOrWhiteSpace(kvp.Key) &&
                !string.IsNullOrWhiteSpace(kvp.Value) &&
                FreeCharacterFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static CharacterSpec SanitizeCharacterForMembership(MembershipPlan membership, CharacterSpec character) =>
        character with
        {
            Role = string.IsNullOrWhiteSpace(character.Role) ? "Main" : character.Role.Trim(),
            DescriptionFields = SanitizeDescriptionFields(membership, character.DescriptionFields)
        };
}
