using System.Text.RegularExpressions;

namespace QuizHelper.AI;

/// <summary>
/// Result from parsing an AI response. Either a multiple-choice set of letters
/// or a direct (open-ended) text answer.
/// </summary>
public class AnswerResult
{
    /// <summary>True when the AI returned uppercase letters (A, B, C...) indicating multiple-choice.</summary>
    public bool IsMultipleChoice { get; init; }

    /// <summary>For multiple-choice: sorted, deduplicated list of answer letters.</summary>
    public List<char> Letters { get; init; } = [];

    /// <summary>For open-ended: the direct text answer from the AI.</summary>
    public string DirectText { get; init; } = string.Empty;

    public string Display => IsMultipleChoice
        ? string.Join(", ", Letters)
        : DirectText;
}

/// <summary>
/// Extracts valid answer letters (A-Z dynamically) from an AI response.
/// Falls back to treating the entire response as a direct text answer when
/// no recognized option letters are present.
/// </summary>
public static class AnswerParser
{
    // Marker the AI uses to signal a direct answer (no multiple choice detected)
    private const string DirectAnswerMarker = "RESPUESTA:";

    /// <summary>
    /// Parses the AI response given the set of valid option letters from the current color map.
    /// Returns an <see cref="AnswerResult"/> with either Letters or DirectText populated.
    /// </summary>
    public static AnswerResult Parse(string aiResponse, IEnumerable<string>? validLetters = null)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return new AnswerResult { IsMultipleChoice = true, Letters = [] };

        var trimmed = aiResponse.Trim();

        // If the AI prefixed with RESPUESTA: marker → direct answer
        if (trimmed.StartsWith(DirectAnswerMarker, StringComparison.OrdinalIgnoreCase))
        {
            var text = trimmed[DirectAnswerMarker.Length..].Trim();
            return new AnswerResult { IsMultipleChoice = false, DirectText = text };
        }

        // Build the letter set from the config color map (default A-F if not supplied)
        var letters = validLetters?.ToHashSet() ?? new HashSet<string> { "A","B","C","D","E","F" };
        var pattern  = string.Join("|", letters.Select(Regex.Escape));
        var regex    = new Regex($@"\b({pattern})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var matches = regex.Matches(trimmed)
            .Select(m => char.ToUpper(m.Value[0]))
            .Where(c => letters.Contains(c.ToString()))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (matches.Count > 0)
            return new AnswerResult { IsMultipleChoice = true, Letters = matches };

        // No option letter found → treat whole response as a direct text answer
        return new AnswerResult { IsMultipleChoice = false, DirectText = trimmed };
    }

    /// <summary>
    /// Convenience overload accepting existing List&lt;char&gt; callers (multiple-choice path).
    /// Returns true if the result carries at least 1 valid letter.
    /// </summary>
    public static bool IsValid(AnswerResult result) =>
        result.IsMultipleChoice
            ? result.Letters.Count >= 1 && result.Letters.Count <= 26
            : !string.IsNullOrWhiteSpace(result.DirectText);

    /// <summary>Back-compat: parse expecting only A-E letters (legacy call sites).</summary>
    public static List<char> Parse(string aiResponse) =>
        Parse(aiResponse, null).Letters;

    /// <summary>Back-compat: validate letter list directly.</summary>
    public static bool IsValid(List<char> answers) =>
        answers.Count >= 1 && answers.Count <= 26;
}
