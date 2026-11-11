namespace PowerPlug.Core.Logging;

/// <summary>Size-capped file log: never grows beyond ~2x <c>maxBytes</c> (one backup file).</summary>
public sealed class RollingFileLog : ILog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _lock = new();

    public RollingFileLog(string path, long maxBytes = 1_000_000)
    {
        _path = path;
        _maxBytes = maxBytes;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} | {exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > _maxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                File.AppendAllText(_path, $"{DateTime.UtcNow:O} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { /* logging must never crash the host */ }
        catch (UnauthorizedAccessException) { }
    }
}
