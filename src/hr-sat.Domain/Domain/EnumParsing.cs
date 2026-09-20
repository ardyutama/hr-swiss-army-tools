namespace hr_sat.Domain;

public static class EnumParsing
{
    public static bool TryParseDefined<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        parsed = default;
        var normalizedValue = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.TryParse(normalizedValue, ignoreCase: true, out parsed);
            }
        }

        return false;
    }
}