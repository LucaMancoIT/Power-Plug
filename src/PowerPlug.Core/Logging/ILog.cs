namespace PowerPlug.Core.Logging;

/// <summary>
/// A simple "diary" for the app. Code writes short messages here about what happened,
/// which helps when something goes wrong. Three levels: Info (normal), Warn (odd but fine),
/// Error (something failed).
/// </summary>
public interface ILog
{
    void Info(string message);
    void Warn(string message);

    /// <param name="message">What went wrong, in plain words.</param>
    /// <param name="exception">The technical error details, if there are any.</param>
    void Error(string message, Exception? exception = null);
}

/// <summary>A log that discards everything. Used in tests and where no log is needed.</summary>
public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? exception = null) { }
}
