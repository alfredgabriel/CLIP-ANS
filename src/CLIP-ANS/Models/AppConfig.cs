using System.Text.Json.Serialization;

namespace QuizHelper.Models;

/// <summary>
/// Serializable configuration POCO — stored in %APPDATA%\CLIP-ANS\config.json.
/// API keys are NOT stored here; they live in Windows Credential Manager.
/// </summary>
public class AppConfig
{
    // ── AI Provider ──────────────────────────────────────────────────────────
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "groq";  // "groq" | "openai"

    [JsonPropertyName("model")]
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 8;

    // ── Detection behaviour ──────────────────────────────────────────────────
    [JsonPropertyName("min_text_length")]
    public int MinTextLength { get; set; } = 80;

    [JsonPropertyName("debounce_ms")]
    public int DebounceMs { get; set; } = 400;

    [JsonPropertyName("poll_interval_ms")]
    public int PollIntervalMs { get; set; } = 250;

    // ── Startup ──────────────────────────────────────────────────────────────
    [JsonPropertyName("show_window_on_start")]
    public bool ShowWindowOnStart { get; set; } = true;

    // ── Color map: letter (A-E) → hex color string ───────────────────────────
    [JsonPropertyName("color_map")]
    public Dictionary<string, string> ColorMap { get; set; } = new()
    {
        ["A"] = "#FF4444",  // Red
        ["B"] = "#44FF66",  // Green
        ["C"] = "#4488FF",  // Blue
        ["D"] = "#FFD700",  // Yellow
        ["E"] = "#AA44FF",  // Purple
    };

    // ── Multi-answer strategy ─────────────────────────────────────────────────
    /// <summary>"blend" | "cycle" | "split"</summary>
    [JsonPropertyName("multi_answer_strategy")]
    public string MultiAnswerStrategy { get; set; } = "blend";

    [JsonPropertyName("cycle_interval_ms")]
    public int CycleIntervalMs { get; set; } = 700;

    // ── Derived helpers ───────────────────────────────────────────────────────
    public static Dictionary<string, string> DefaultColorMap => new()
    {
        ["A"] = "#FF4444",
        ["B"] = "#44FF66",
        ["C"] = "#4488FF",
        ["D"] = "#FFD700",
        ["E"] = "#AA44FF",
    };

    public static Dictionary<string, string[]> ProviderModels => new()
    {
        ["groq"]   = ["llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768", "gemma2-9b-it"],
        ["openai"]  = ["gpt-4o-mini", "gpt-4o"],
    };
}
