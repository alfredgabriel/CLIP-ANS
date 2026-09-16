using System.Text.RegularExpressions;

namespace QuizHelper.AI;

/// <summary>
/// Extracts valid answer letters (A-E) from an AI response string.
/// Defensive: ignores any surrounding text the model may add.
/// </summary>
public static class AnswerParser
{
    // Matches single uppercase letters A-E, case-insensitive
    private static readonly Regex _letterRegex = new(@"\b([A-Ea-e])\b", RegexOptions.Compiled);

    /// <summary>
    /// Parses the AI response and returns a sorted, deduplicated list of answer letters.
    /// Returns an empty list if no valid letters are found.
    /// </summary>
    public static List<char> Parse(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return [];

        var matches = _letterRegex.Matches(aiResponse);
        return matches
            .Select(m => char.ToUpper(m.Value[0]))
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// Returns true if the response looks like a valid answer (1–5 letters).
    /// </summary>
    public static bool IsValid(List<char> answers) =>
        answers.Count >= 1 && answers.Count <= 5;
}
