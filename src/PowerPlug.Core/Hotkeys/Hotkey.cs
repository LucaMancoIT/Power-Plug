namespace PowerPlug.Core.Hotkeys;

/// <summary>The "helper" keys that are held down together with a normal key (e.g. Ctrl in Ctrl+C).</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

/// <summary>
/// A keyboard shortcut: modifier keys plus one main key.
/// <paramref name="VirtualKey"/> is the Windows key code of the main key (for example 0x54 = the letter T).
/// </summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey);

/// <summary>
/// Registers global keyboard shortcuts that work in any program.
/// The real version lives in the App project (it needs Windows); tests use a fake one.
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Tries to claim the shortcut. Returns false if it is already taken
    /// (by Windows or by another program). Dispose <paramref name="registration"/> to release it.
    /// </summary>
    bool TryRegister(Hotkey hotkey, Action callback, out IDisposable? registration);
}
