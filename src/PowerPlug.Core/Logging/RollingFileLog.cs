namespace PowerPlug.Core.Logging;

/// <summary>
/// Writes the log to a text file, but never lets it grow without limit:
/// when the file gets bigger than <c>maxBytes</c> it is renamed to "*.1" (replacing the previous
/// backup) and a fresh file is started. So the log never takes more than about twice <c>maxBytes</c>.
/// </summary>
public sealed class RollingFileLog : ILog
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _lock = new(); // several threads may log at the same time

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
                // Too big? Move the old file aside so the log stays small.
                if (File.Exists(_path) && new FileInfo(_path).Length > _maxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);

                // Timestamps are in UTC so logs are comparable between computers.
                File.AppendAllText(_path, $"{DateTime.UtcNow:O} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException) { /* logging must never crash the app */ }
        catch (UnauthorizedAccessException) { /* same: ignore if the file cannot be written */ }
    }
}
