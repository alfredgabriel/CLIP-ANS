using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QuizHelper.AI;

/// <summary>
/// Base class for OpenAI-compatible chat completion endpoints (Groq, OpenAI, etc.)
/// </summary>
public abstract class OpenAICompatibleClient : IAIClient
{
    protected abstract string EndpointUrl { get; }
    protected abstract string ApiKey { get; }
    protected abstract string Model { get; }
    protected abstract int TimeoutSeconds { get; }

    private static readonly HttpClient _http = new();

    private const string SystemPrompt =
        "Eres un asistente de examen experto y ultra-preciso. " +
        "Dado un enunciado de pregunta tipo test con sus opciones (tengan o no letras identificativas): " +
        "1. Si las opciones no vienen precedidas por letras A, B, C, D, asume siempre que la 1ª opción es A, la 2ª es B, la 3ª es C, la 4ª es D, la 5ª es E. " +
        "2. Responde ÚNICAMENTE con la(s) letra(s) mayúscula(s) correcta(s) (ejemplo: A o B o A,C). " +
        "3. PROHIBIDO dar explicaciones, texto adicional o palabras. Responde EXCLUSIVAMENTE con la(s) letra(s).";

    public async Task<string> AskAsync(string questionText, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        var payload = new
        {
            model    = Model,
            messages = new[]
            {
                new { role = "system",  content = SystemPrompt },
                new { role = "user",    content = questionText }
            },
            max_tokens  = 150,
            temperature = 0.0
        };

        var json    = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        try
        {
            var response = await _http.SendAsync(request, cts.Token);
            var body     = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                string errorMsg = $"HTTP {(int)response.StatusCode}";
                try
                {
                    using var errDoc = JsonDocument.Parse(body);
                    if (errDoc.RootElement.TryGetProperty("error", out var errObj) &&
                        errObj.TryGetProperty("message", out var msgProp))
                    {
                        errorMsg = msgProp.GetString() ?? errorMsg;
                    }
                }
                catch { /* fallback to status code */ }

                throw new InvalidOperationException(errorMsg);
            }

            var doc = JsonDocument.Parse(body);
            var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            string content = msg.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(content) && msg.TryGetProperty("reasoning", out var rProp))
            {
                content = rProp.GetString() ?? string.Empty;
            }
            return content;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"La IA no respondió en {TimeoutSeconds} segundos.");
        }
    }
}

// ── Groq ─────────────────────────────────────────────────────────────────────

/// <summary>Groq AI client (OpenAI-compatible endpoint).</summary>
public sealed class GroqClient : OpenAICompatibleClient
{
    protected override string EndpointUrl => "https://api.groq.com/openai/v1/chat/completions";
    protected override string ApiKey { get; }
    protected override string Model { get; }
    protected override int TimeoutSeconds { get; }

    public GroqClient(string apiKey, string model = "qwen/qwen3.8-27b", int timeoutSeconds = 8)
    {
        ApiKey         = apiKey;
        Model          = model;
        TimeoutSeconds = timeoutSeconds;
    }
}

// ── OpenAI ────────────────────────────────────────────────────────────────────

/// <summary>OpenAI client.</summary>
public sealed class OpenAIClient : OpenAICompatibleClient
{
    protected override string EndpointUrl => "https://api.openai.com/v1/chat/completions";
    protected override string ApiKey { get; }
    protected override string Model { get; }
    protected override int TimeoutSeconds { get; }

    public OpenAIClient(string apiKey, string model = "gpt-4o-mini", int timeoutSeconds = 8)
    {
        ApiKey         = apiKey;
        Model          = model;
        TimeoutSeconds = timeoutSeconds;
    }
}
