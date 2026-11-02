using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitAccessibility.Core.RuleDefinitions;

public sealed class RuleDefinitionFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Strict JSON reader for rule definitions: unknown fields, numeric enum values and trailing garbage are rejected,
/// so a typo can never silently weaken a rule. Reading never executes anything.
/// </summary>
public static class RuleDefinitionJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private sealed record Envelope(IReadOnlyList<RuleDefinition>? Rules);

    public static IReadOnlyList<RuleDefinition> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var env = JsonSerializer.Deserialize<Envelope>(json, Options)
                      ?? throw new RuleDefinitionFormatException("Rule file is empty.");
            if (env.Rules is null) throw new RuleDefinitionFormatException("Rule file has no 'rules' array.");
            if (env.Rules.Any(r => r is null)) throw new RuleDefinitionFormatException("Rule file contains an empty rule entry.");
            return env.Rules;
        }
        catch (JsonException ex)
        {
            throw new RuleDefinitionFormatException($"Rule file is not valid: {ex.Message}", ex);
        }
    }

    public static string Serialize(IReadOnlyList<RuleDefinition> rules) =>
        JsonSerializer.Serialize(new Envelope(rules), new JsonSerializerOptions(Options) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
}
