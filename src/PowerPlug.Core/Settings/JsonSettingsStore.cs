using System.Text.Json;
using PowerPlug.Core.Logging;

namespace PowerPlug.Core.Settings;

/// <summary>
/// JSON settings with crash-safe writes (temp file + atomic replace). A corrupt file is moved aside
/// to <c>.corrupt</c> and defaults are returned, so a bad file can never prevent startup.
/// </summary>
public sealed class JsonSettingsStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    private readonly ILog _log;

    public JsonSettingsStore(string path, ILog? log = null)
    {
        _path = path;
        _log = log ?? NullLog.Instance;
    }

    public T Load()
    {
        if (!File.Exists(_path)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _log.Error($"Settings unreadable, using defaults: {_path}", ex);
            try { File.Move(_path, _path + ".corrupt", overwrite: true); } catch (IOException) { }
            return new T();
        }
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
