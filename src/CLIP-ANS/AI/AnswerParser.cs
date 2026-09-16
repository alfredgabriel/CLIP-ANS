using System.Text.RegularExpressions;

namespace QuizHelper.AI;

/// <summary>
/// Result from parsing an AI response. Either a multiple-choice set of letters
/// or a direct (open-ended) text answer.
/// </summary>
public class AnswerResult
{
    /// <summary>True when the AI returned option letters (A, B, C...) indicating multiple-choice.</summary>
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
/// Safely distinguishes between multiple-choice letter codes (A, B, C...)
/// and open-ended text answers without false-triggering on Spanish prepositions/conjunctions ('a', 'e', 'd').
/// </summary>
public static class AnswerParser
{
    // Matches and removes <think>...</think> blocks from reasoning models (e.g. DeepSeek R1)
    private static readonly Regex _thinkTagRegex = new(@"<think>[\s\S]*?</think>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches leading prefixes like "RESPUESTA:", "**RESPUESTA:**", "### Respuesta: ", "R:"
    private static readonly Regex _prefixRegex = new(
        @"^(\*{1,3}|#{1,4}\s*)?(RESPUESTA|ANSWER|SOLUCI[ÓO]N|R)\s*(\*{1,3})?\s*[:\-\.]?\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses the AI response given the set of valid option letters from the current color map.
    /// Returns an <see cref="AnswerResult"/> with either Letters or DirectText populated.
    /// </summary>
    public static AnswerResult Parse(string aiResponse, IEnumerable<string>? validLetters = null)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return new AnswerResult { IsMultipleChoice = true, Letters = [] };

        // 1. Remove reasoning thought tags if present
        var cleaned = _thinkTagRegex.Replace(aiResponse, string.Empty).Trim();

        // 2. Strip standard answer prefix if present (e.g. "RESPUESTA: ", "**Respuesta:** ")
        var prefixMatch = _prefixRegex.Match(cleaned);
        if (prefixMatch.Success)
        {
            cleaned = cleaned[prefixMatch.Length..].Trim();
        }

        // Clean any residual markdown bolding or backticks wrapping the whole string
        if (cleaned.StartsWith("**") && cleaned.EndsWith("**") && cleaned.Length >= 4)
            cleaned = cleaned[2..^2].Trim();
        if (cleaned.StartsWith('`') && cleaned.EndsWith('`') && cleaned.Length >= 2)
            cleaned = cleaned[1..^1].Trim();

        var validSet = validLetters?.ToHashSet() ?? new HashSet<string> { "A", "B", "C", "D", "E", "F" };

        // 3. Check if the response is purely a multiple-choice response (letters + separators only)
        // Examples: "A", "B", "A, C", "A, B y D", "Opción A", "A)", "B."
        var candidateLetters = ExtractStrictMultipleChoice(cleaned, validSet);
        if (candidateLetters.Count > 0)
        {
            return new AnswerResult
            {
                IsMultipleChoice = true,
                Letters          = candidateLetters,
            };
        }

        // 4. If it contains actual words or phrases, treat it as a direct open-ended text answer
        return new AnswerResult
        {
            IsMultipleChoice = false,
            DirectText       = cleaned,
        };
    }

    /// <summary>
    /// Determines whether the string strictly represents multiple-choice option letters.
    /// Rejects strings that contain sentences, verbs, or explanatory words to avoid
    /// mistaking Spanish words ("a", "e", "de") for options.
    /// </summary>
    private static List<char> ExtractStrictMultipleChoice(string text, HashSet<string> validLetters)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        // Normalize text by stripping common filler words: "opción", "opciones", "letra", "letras", "respuesta"
        var normalized = Regex.Replace(
            text,
            @"\b(opci[oó]n(es)?|letras?|respuestas?|correctas?|es\s+la)\b",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();

        // Check if normalized text is strictly option letters separated by commas, spaces, slashes, 'y', or dots
        // e.g. "A", "A, C", "A y B", "B)", "A/C"
        var tokens = Regex.Split(normalized, @"[,\s/y\-\.;\)]+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        if (tokens.Count == 0 || tokens.Count > 10)
            return [];

        // All tokens MUST be single valid option letters (e.g. 'A', 'B')
        var letters = new List<char>();
        foreach (var token in tokens)
        {
            var cleanToken = token.Trim().TrimEnd('.', ')', ':').ToUpperInvariant();
            if (cleanToken.Length == 1 && validLetters.Contains(cleanToken))
            {
                letters.Add(cleanToken[0]);
            }
            else
            {
                // Found a word that is not an option letter (e.g. "París", "Madrid", "fotosíntesis")
                // Therefore this is NOT a multiple-choice letter code!
                return [];
            }
        }

        return letters.Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Returns true if the result carries at least 1 valid letter or non-empty direct text.
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
