namespace Aula.Protocol;

/// <summary>Sleep timeout choices, as the vendor's combo item data (FUN_0043b420).</summary>
public enum SleepTime : byte
{
    Never = 0,
    OneMinute = 1,
    FiveMinutes = 2,
    ThirtyMinutes = 3,
}

/// <summary>
/// Keyboard-wide options sent together in one report (FUN_00414230). The firmware can't report them,
/// so every send must carry all of them. Defaults match the vendor app's fresh install.
/// </summary>
public sealed record DeviceOptions
{
    public const int MinResponseLevel = 1;
    public const int MaxResponseLevel = 5;

    public bool DisableWinKey { get; init; }
    public bool DisableAltF4 { get; init; }
    public bool DisableAltTab { get; init; }

    /// <summary>False: FN works while held. True: a tap toggles the FN layer on and off.</summary>
    public bool FnToggle { get; init; }

    public SleepTime Sleep { get; init; } = SleepTime.Never;

    /// <summary>Debounce level 1–5 (wired ≈ 2–3 ms up to ≈ 17–18 ms). Raise it if keys chatter.</summary>
    public int ResponseLevel { get; init; } = 1;

    public void Validate()
    {
        if (ResponseLevel is < MinResponseLevel or > MaxResponseLevel)
            throw new ArgumentOutOfRangeException(nameof(ResponseLevel), "Response level is 1–5.");
        if (!Enum.IsDefined(Sleep)) throw new ArgumentOutOfRangeException(nameof(Sleep));
    }
}
