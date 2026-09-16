using System.Text.Json;
using System.Windows.Security.Permissions;
using QuizHelper.Models;
using Windows.Security.Credentials;

namespace QuizHelper.Core;

/// <summary>
/// Manages persistence:
///   - config.json in %APPDATA%\QuizHelper\ for non-sensitive settings
///   - Windows Credential Manager (PasswordVault) for API keys
/// </summary>
public class ConfigService
{
    private const string AppFolder = "QuizHelper";
    private const string ConfigFile = "config.json";
    private const string CredentialResource = "QuizHelper_ApiKey";

    private readonly string _configDir;
    private readonly string _configPath;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public ConfigService()
    {
        _configDir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolder);
        _configPath = Path.Combine(_configDir, ConfigFile);
    }

    // ── Config file ──────────────────────────────────────────────────────────

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Load error: {ex.Message}");
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var json = JsonSerializer.Serialize(config, _jsonOpts);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Save error: {ex.Message}");
        }
    }

    public bool ConfigExists() => File.Exists(_configPath);

    // ── Windows Credential Manager ───────────────────────────────────────────

    /// <summary>
    /// Saves the API key for a given provider into Windows Credential Manager.
    /// credentialName: e.g. "groq" or "openai"
    /// </summary>
    public void SaveApiKey(string provider, string apiKey)
    {
        try
        {
            var vault = new PasswordVault();
            // Remove existing entry if any
            try
            {
                var existing = vault.Retrieve(CredentialResource, provider);
                vault.Remove(existing);
            }
            catch { /* not found, fine */ }

            vault.Add(new PasswordCredential(CredentialResource, provider, apiKey));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigService] SaveApiKey error: {ex.Message}");
            throw new InvalidOperationException(
                "No se pudo guardar la API key en Credential Manager. " +
                "Verifica los permisos de la aplicación.", ex);
        }
    }

    /// <summary>
    /// Retrieves the API key for the given provider. Returns null if not found.
    /// </summary>
    public string? LoadApiKey(string provider)
    {
        try
        {
            var vault = new PasswordVault();
            var cred  = vault.Retrieve(CredentialResource, provider);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes the API key for the given provider from Credential Manager.
    /// </summary>
    public void DeleteApiKey(string provider)
    {
        try
        {
            var vault = new PasswordVault();
            var cred  = vault.Retrieve(CredentialResource, provider);
            vault.Remove(cred);
        }
        catch { /* not found, ignore */ }
    }
}
