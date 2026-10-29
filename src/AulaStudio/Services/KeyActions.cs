using Aula.Protocol;
using AulaStudio.Localization;

namespace AulaStudio.Services;

/// <summary>What a key can be set to, and how each choice reads in the UI.</summary>
public static class KeyActions
{
    public sealed record Choice<T>(T Value, string NameKey)
    {
        public string Name => Loc.T(NameKey);
    }

    public static readonly Choice<MouseAction>[] Mouse =
    [
        new(MouseAction.Left, "Mouse_Left"), new(MouseAction.Right, "Mouse_Right"), new(MouseAction.Middle, "Mouse_Middle"),
        new(MouseAction.Back, "Mouse_Back"), new(MouseAction.Forward, "Mouse_Forward"),
        new(MouseAction.WheelUp, "Mouse_WheelUp"), new(MouseAction.WheelDown, "Mouse_WheelDown"),
    ];

    /// <summary>HID consumer-page usages the vendor app offers (FUN_004194e0).</summary>
    public static readonly Choice<ushort>[] Media =
    [
        new(0xCD, "Media_PlayPause"), new(0xB7, "Media_Stop"), new(0xB6, "Media_Previous"), new(0xB5, "Media_Next"),
        new(0xE9, "Media_VolumeUp"), new(0xEA, "Media_VolumeDown"), new(0xE2, "Media_Mute"),
        new(0x183, "Media_Player"), new(0x192, "Media_Calculator"), new(0x18A, "Media_Mail"), new(0x194, "Media_Computer"),
        new(0x223, "Media_WebHome"), new(0x221, "Media_WebSearch"), new(0x224, "Media_WebBack"),
        new(0x225, "Media_WebForward"), new(0x227, "Media_WebRefresh"), new(0x22A, "Media_WebFavorites"),
    ];

    public static readonly Choice<MacroPlayMode>[] PlayModes =
    [
        new(MacroPlayMode.Once, "Macro_ModeOnce"), new(MacroPlayMode.Repeat, "Macro_ModeRepeat"),
        new(MacroPlayMode.UntilPressedAgain, "Macro_ModeToggle"), new(MacroPlayMode.WhileHeld, "Macro_ModeHold"),
    ];

    /// <summary>Common shortcuts offered as one-click presets.</summary>
    public static readonly (string NameKey, KeyAction Action)[] Shortcuts =
    [
        ("Shortcut_Copy", KeyAction.Key(0x06, KeyModifiers.LeftCtrl)),
        ("Shortcut_Paste", KeyAction.Key(0x19, KeyModifiers.LeftCtrl)),
        ("Shortcut_Cut", KeyAction.Key(0x1B, KeyModifiers.LeftCtrl)),
        ("Shortcut_Undo", KeyAction.Key(0x1D, KeyModifiers.LeftCtrl)),
        ("Shortcut_Desktop", KeyAction.Key(0x07, KeyModifiers.LeftWin)),
        ("Shortcut_Explorer", KeyAction.Key(0x08, KeyModifiers.LeftWin)),
        ("Shortcut_Lock", KeyAction.Key(0x0F, KeyModifiers.LeftWin)),
        ("Shortcut_SwitchWindow", KeyAction.Key(0x2B, KeyModifiers.LeftAlt)),
        ("Shortcut_Screenshot", KeyAction.Key(0x16, KeyModifiers.LeftWin | KeyModifiers.LeftShift)),
        ("Shortcut_TaskManager", KeyAction.Key(0x29, KeyModifiers.LeftCtrl | KeyModifiers.LeftShift)),
    ];

    private static readonly (KeyModifiers Bit, string Name)[] ModifierNames =
    [
        (KeyModifiers.LeftCtrl, "Ctrl"), (KeyModifiers.RightCtrl, "Ctrl Dir."), (KeyModifiers.LeftShift, "Shift"),
        (KeyModifiers.RightShift, "Shift Dir."), (KeyModifiers.LeftAlt, "Alt"), (KeyModifiers.RightAlt, "AltGr"),
        (KeyModifiers.LeftWin, "Win"), (KeyModifiers.RightWin, "Win Dir."),
    ];

    public static string KeyCombo(KeyModifiers mods, byte hid)
    {
        var parts = ModifierNames.Where(m => mods.HasFlag(m.Bit)).Select(m => m.Name).ToList();
        if (hid != 0) parts.Add(HidKeys.Name(hid));
        return string.Join(" + ", parts);
    }

    /// <summary>A full description, e.g. "Ctrl + C" or "Macro: Login (3×)".</summary>
    public static string Describe(KeyAction a, IReadOnlyList<string> macroNames)
    {
        if (a.IsDefault) return Loc.T("KeyType_Default");
        if (a == KeyAction.Disabled) return Loc.T("KeyType_Disabled");
        switch (a.Type)
        {
            case 0x02:
                return KeyCombo((KeyModifiers)a.B1, a.B2);
            case 0x01:
                var m = Mouse.FirstOrDefault(c => KeyAction.Mouse(c.Value) == a);
                return m?.Name ?? Loc.T("KeyType_Mouse");
            case 0x03:
                var usage = (ushort)(a.B1 | a.B2 << 8);
                return Media.FirstOrDefault(c => c.Value == usage)?.Name ?? $"0x{usage:X3}";
            case 0x06:
                var name = a.B1 < macroNames.Count ? macroNames[a.B1] : $"#{a.B1 + 1}";
                var suffix = (MacroPlayMode)a.B2 == MacroPlayMode.Repeat ? $" ({a.B3}×)" : "";
                return $"{Loc.T("KeyType_Macro")}: {name}{suffix}";
            default:
                return $"{a.B0:X2}{a.B1:X2}{a.B2:X2}{a.B3:X2}";
        }
    }

    /// <summary>A short keycap caption for the keyboard drawing.</summary>
    public static string Caption(KeyAction a, IReadOnlyList<string> macroNames)
    {
        if (a == KeyAction.Disabled) return "⊘";
        return a.Type switch
        {
            0x02 => KeyCombo((KeyModifiers)a.B1, a.B2).Replace(" + ", "+"),
            0x01 => "🖱",
            0x03 => "♪",
            0x06 => $"M{a.B1 + 1}",
            _ => "•",
        };
    }
}
