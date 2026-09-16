namespace QuizHelper.AI;

/// <summary>
/// Common interface for all AI provider clients.
/// </summary>
public interface IAIClient
{
    /// <summary>
    /// Sends the question text to the AI and returns the raw response string.
    /// Throws <see cref="TimeoutException"/> if the response exceeds the timeout.
    /// Throws <see cref="HttpRequestException"/> on network errors.
    /// </summary>
    Task<string> AskAsync(string questionText, CancellationToken ct = default);
}
