namespace PowerPlug.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Win = 8 }

public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey);

public interface IHotkeyService
{
    /// <summary>Returns false if the combination is already taken (e.g. by Windows or another app).</summary>
    bool TryRegister(Hotkey hotkey, Action callback, out IDisposable? registration);
}
