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
    public string Model { get; set; } = "llama3-70b-8192";

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 8;

    // ── Detection behaviour ──────────────────────────────────────────────────
    [JsonPropertyName("min_text_length")]
    public int MinTextLength { get; set; } = 20;

    [JsonPropertyName("debounce_ms")]
    public int DebounceMs { get; set; } = 400;

    [JsonPropertyName("poll_interval_ms")]
    public int PollIntervalMs { get; set; } = 250;

    [JsonPropertyName("detect_text")]
    public bool DetectText { get; set; } = true;

    [JsonPropertyName("detect_screenshots")]
    public bool DetectScreenshots { get; set; } = true;

    [JsonPropertyName("show_notifications")]
    public bool ShowNotifications { get; set; } = true;

    // ── Startup ──────────────────────────────────────────────────────────────
    [JsonPropertyName("show_window_on_start")]
    public bool ShowWindowOnStart { get; set; } = true;

    // ── Overlay ──────────────────────────────────────────────────────────────
    [JsonPropertyName("show_overlay")]
    public bool ShowOverlay { get; set; } = false;

    [JsonPropertyName("overlay_opacity")]
    public double OverlayOpacity { get; set; } = 0.90;

    // ── Color map: letter (A-Z) → hex color string ───────────────────────────
    [JsonPropertyName("color_map")]
    public Dictionary<string, string> ColorMap { get; set; } = new()
    {
        ["A"] = "#FF4444",  // Red
        ["B"] = "#44FF66",  // Green
        ["C"] = "#4488FF",  // Blue
        ["D"] = "#FFD700",  // Yellow
        ["E"] = "#AA44FF",  // Purple
        ["F"] = "#FF8800",  // Orange
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
        ["F"] = "#FF8800",
    };

    // Default colors for new letters added dynamically
    private static readonly string[] _extraColors =
    [
        "#FF8800", "#00CCFF", "#FF44AA", "#88FF00",
        "#FF2244", "#00FFCC", "#FFCC00", "#AA00FF",
        "#FF6600", "#00FF88", "#4400FF", "#FF0066",
        "#AAAAAA", "#00AAFF", "#FFAA00", "#00FFAA",
        "#CC44FF", "#FF44CC", "#44FFCC", "#CCFF44",
    ];

    /// <summary>Returns the next alphabetical letter not yet in ColorMap, or null if A-Z exhausted.</summary>
    public string? NextAvailableLetter()
    {
        for (char c = 'A'; c <= 'Z'; c++)
            if (!ColorMap.ContainsKey(c.ToString())) return c.ToString();
        return null;
    }

    /// <summary>Default hex color to assign when a new letter is added.</summary>
    public string DefaultColorForNewLetter()
    {
        int idx = ColorMap.Count - 1;
        return idx >= 0 && idx < _extraColors.Length ? _extraColors[idx] : "#AAAAAA";
    }

    public static Dictionary<string, string[]> ProviderModels => new()
    {
        ["groq"]   = ["llama3-70b-8192", "meta-llama/llama-4-scout-17b-16e-instruct", "llama-3.1-8b-instant"],
        ["openai"] = ["gpt-4o", "gpt-4o-mini"],
    };
}
