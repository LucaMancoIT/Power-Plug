using System.Text.Json;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Settings;

/// <summary>
/// Loads and saves settings as a JSON text file.
/// Safety features:
///  - Saving writes to a temporary file first and then swaps it in, so a crash while saving
///    can never leave a half-written settings file.
///  - If the file is damaged, it is renamed to "*.corrupt" and default settings are used,
///    so a broken file can never stop the app from starting.
/// </summary>
public sealed class JsonSettingsStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true }; // readable file
    private readonly string _path;
    private readonly ILog _log;

    public JsonSettingsStore(string path, ILog? log = null)
    {
        _path = path;
        _log = log ?? NullLog.Instance;
    }

    /// <summary>Reads the settings. Returns defaults if the file is missing or unreadable.</summary>
    public T Load()
    {
        if (!File.Exists(_path)) return new T(); // first start: nothing saved yet
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _log.Error($"Settings unreadable, using defaults: {_path}", ex);
            // Keep the damaged file so the user can look at it, but get it out of the way.
            try { File.Move(_path, _path + ".corrupt", overwrite: true); } catch (IOException) { }
            return new T();
        }
    }

    /// <summary>Writes the settings safely (temporary file first, then replace).</summary>
    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, _path, overwrite: true); // swap in the finished file in one step
    }
}
