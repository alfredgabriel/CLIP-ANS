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
    public string Model { get; protected set; } = string.Empty;
    public string CurrentModel => Model;
    public IReadOnlyList<string> CandidateModels { get; protected set; } = [];
    protected abstract int TimeoutSeconds { get; }

    public event Action<string>? ModelAutoSwitched;

    private static readonly HttpClient _http = new();

    private const string SystemPrompt =
        "Eres un asistente de examen experto y ultra-preciso. Sigue estas reglas ESTRICTAMENTE:\n" +
        "CASO 1 — PREGUNTA TIPO TEST (hay opciones A, B, C, D… o una lista de alternativas):\n" +
        "  - Si las opciones van precedidas de letras (A, B, C, D, E, F…), responde SOLO con esa(s) letra(s) mayúscula(s) separadas por coma si son varias. Ejemplo: A  /  A,C  /  B,D,F\n" +
        "  - Si las opciones NO van precedidas de letras pero hay una lista de alternativas, asume A=1ª, B=2ª, C=3ª, D=4ª, E=5ª, F=6ª y responde igual.\n" +
        "  - PROHIBIDO cualquier palabra, explicación o puntuación extra.\n" +
        "CASO 2 — PREGUNTA ABIERTA / DE DESARROLLO / DE ESCRIBIR (no hay opciones alternativas):\n" +
        "  - Responde con el prefijo exacto 'RESPUESTA: ' seguido de la respuesta completa, explicada y desarrollada con la profundidad necesaria para responder de forma precisa y total a la pregunta.\n" +
        "  - NO limites artificialmente la respuesta si la pregunta requiere una explicación o detalle para ser correcta.\n" +
        "REGLA GLOBAL: nunca mezcles ambos formatos. Elige el caso correcto y responde únicamente en ese formato.";

    private static bool IsTokenOrRateLimitError(int statusCode, string message)
    {
        if (statusCode == 429) return true;
        var lower = message.ToLowerInvariant();
        return lower.Contains("rate limit") ||
               lower.Contains("rate_limit") ||
               lower.Contains("token") ||
               lower.Contains("tpm") ||
               lower.Contains("rpm") ||
               lower.Contains("quota") ||
               lower.Contains("insufficient_quota") ||
               lower.Contains("capacity") ||
               lower.Contains("overloaded") ||
               lower.Contains("decommissioned") ||
               lower.Contains("not found") ||
               lower.Contains("model_not_found");
    }

    public async Task<string> AskAsync(string questionText, CancellationToken ct = default)
    {
        // Build candidate list prioritizing the current active Model
        var modelsToTry = new List<string> { Model };
        foreach (var m in CandidateModels)
        {
            if (!string.IsNullOrWhiteSpace(m) && !modelsToTry.Contains(m))
                modelsToTry.Add(m);
        }

        string lastError = "Error desconocido al consultar la IA";

        for (int i = 0; i < modelsToTry.Count; i++)
        {
            var attemptModel = modelsToTry[i];
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

                var payload = new
                {
                    model    = attemptModel,
                    messages = new[]
                    {
                        new { role = "system",  content = SystemPrompt },
                        new { role = "user",    content = questionText }
                    },
                    max_tokens  = 1000,
                    temperature = 0.0
                };

                var json    = JsonSerializer.Serialize(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, EndpointUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

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

                    lastError = errorMsg;

                    // If rate limit / tokens exhausted and we have more candidates, switch automatically!
                    if (IsTokenOrRateLimitError((int)response.StatusCode, errorMsg) && i + 1 < modelsToTry.Count)
                    {
                        var nextModel = modelsToTry[i + 1];
                        System.Diagnostics.Debug.WriteLine($"[CLIP-ANS] Modelo {attemptModel} sin tokens/límite alcanzado ({errorMsg}). Cambiando a: {nextModel}");
                        continue;
                    }

                    throw new InvalidOperationException(errorMsg);
                }

                var doc = JsonDocument.Parse(body);
                var msg = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
                string content = msg.TryGetProperty("content", out var cProp) ? cProp.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(content) && msg.TryGetProperty("reasoning", out var rProp))
                {
                    content = rProp.GetString() ?? string.Empty;
                }

                // If we successfully used a fallback model, update current model and notify!
                if (attemptModel != Model)
                {
                    Model = attemptModel;
                    ModelAutoSwitched?.Invoke(attemptModel);
                }

                return content;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (i + 1 < modelsToTry.Count) continue;
                throw new TimeoutException($"La IA no respondió en {TimeoutSeconds} segundos.");
            }
            catch (Exception ex) when (i + 1 < modelsToTry.Count && IsTokenOrRateLimitError(0, ex.Message))
            {
                lastError = ex.Message;
                continue;
            }
        }

        throw new InvalidOperationException(lastError);
    }
}

// ── Groq ─────────────────────────────────────────────────────────────────────

/// <summary>Groq AI client (OpenAI-compatible endpoint) with auto-failover on token exhaustion.</summary>
public sealed class GroqClient : OpenAICompatibleClient
{
    protected override string EndpointUrl => "https://api.groq.com/openai/v1/chat/completions";
    protected override string ApiKey { get; }
    protected override int TimeoutSeconds { get; }

    public static readonly string[] DefaultGroqModels =
    [
        "llama3-70b-8192",
        "meta-llama/llama-4-scout-17b-16e-instruct",
        "llama-3.1-8b-instant"
    ];

    public GroqClient(string apiKey, string model = "llama-3.3-70b-versatile", int timeoutSeconds = 8, IEnumerable<string>? candidateModels = null)
    {
        ApiKey          = apiKey;
        Model           = string.IsNullOrWhiteSpace(model) ? "llama3-70b-8192" : model;
        TimeoutSeconds  = timeoutSeconds;
        CandidateModels = (candidateModels != null && candidateModels.Any())
            ? candidateModels.Distinct().ToList()
            : DefaultGroqModels;
    }
}

// ── OpenAI ────────────────────────────────────────────────────────────────────

/// <summary>OpenAI client with auto-failover on token exhaustion.</summary>
public sealed class OpenAIClient : OpenAICompatibleClient
{
    protected override string EndpointUrl => "https://api.openai.com/v1/chat/completions";
    protected override string ApiKey { get; }
    protected override int TimeoutSeconds { get; }

    public static readonly string[] DefaultOpenAIModels =
    [
        "gpt-4o",
        "gpt-4o-mini"
    ];

    public OpenAIClient(string apiKey, string model = "gpt-4o", int timeoutSeconds = 8, IEnumerable<string>? candidateModels = null)
    {
        ApiKey          = apiKey;
        Model           = string.IsNullOrWhiteSpace(model) ? "gpt-4o" : model;
        TimeoutSeconds  = timeoutSeconds;
        CandidateModels = (candidateModels != null && candidateModels.Any())
            ? candidateModels.Distinct().ToList()
            : DefaultOpenAIModels;
    }
}
