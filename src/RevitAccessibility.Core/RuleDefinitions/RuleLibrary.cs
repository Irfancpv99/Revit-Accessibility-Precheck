using RevitAccessibility.Core.Register;

namespace RevitAccessibility.Core.RuleDefinitions;

/// <summary>
/// Loads every rule source and builds one rule set: the shipped register (rules/rule-register.csv) plus the
/// architect's own JSON rule files in a user folder. Nothing is dropped silently: an unreadable file, a malformed
/// definition, a duplicate id or an unsupported method is reported in <see cref="RuleSetBuildResult.NotRun"/>.
/// The shipped register is loaded first, so a user file can never replace a shipped rule that has the same id.
/// </summary>
public static class RuleLibrary
{
    public const string UserRuleFilePattern = "*.json";

    /// <param name="registerCsvPath">The shipped register. A missing or unreadable register is an error, never an empty rule set.</param>
    /// <param name="userRulesDirectory">Folder of architect-managed rule files; null or non-existent means none.</param>
    public static RuleSetBuildResult Load(string registerCsvPath, string? userRulesDirectory, RuleCapabilities? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerCsvPath);

        var adapted = LegacyRegisterAdapter.Adapt(RuleRegisterLoader.LoadFile(registerCsvPath));
        var definitions = new List<RuleDefinition>(adapted.Definitions);
        var notRun = new List<RuleNotRun>(adapted.NotRun);

        if (!string.IsNullOrWhiteSpace(userRulesDirectory) && Directory.Exists(userRulesDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(userRulesDirectory, UserRuleFilePattern, SearchOption.TopDirectoryOnly)
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file);
                try
                {
                    definitions.AddRange(RuleDefinitionJson.Parse(File.ReadAllText(file)));
                }
                catch (RuleDefinitionFormatException ex)
                {
                    notRun.Add(new RuleNotRun($"(file {name})", ex.Message));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    notRun.Add(new RuleNotRun($"(file {name})", $"Could not read the file: {ex.Message}"));
                }
            }
        }

        return RuleSetBuilder.Build(definitions, capabilities, notRun);
    }

    /// <summary>
    /// Banner text for the results window, or null when every rule that ran is Verified.
    /// Legal verification does not stop a rule running; it must stop its results being read as approved.
    /// </summary>
    public static string? UnverifiedBanner(RuleSetBuildResult set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var ids = set.Reports
            .Where(r => r.IsExecutable && r.GovernanceWarnings.Any(w => w.Code == "GOV-UNVERIFIED"))
            .Select(r => r.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return ids.Count == 0
            ? null
            : $"UNVERIFIED: {ids.Count} rule(s) that ran are not legally verified ({string.Join(", ", ids)}). Not verified regulation values.";
    }
}
