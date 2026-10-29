namespace Aula.Protocol;

/// <summary>Main backlight effects, numbered as the firmware expects (vendor layout default = Rolling).</summary>
public enum LightingEffect : byte
{
    Off = 0,
    Static = 1,
    SingleOn = 2,
    SingleOff = 3,
    Glittering = 4,
    Falling = 5,
    Colourful = 6,
    Breath = 7,
    Spectrum = 8,
    Outward = 9,
    Scrolling = 10,
    Rolling = 11,
    Rotating = 12,
    Explode = 13,
    Launch = 14,
    Ripples = 15,
    Flowing = 16,
    Pulsating = 17,
    Tilt = 18,
    Shuttle = 19,
}

/// <summary>Light-bar/side modes from the vendor's LED layer list (combo index + 0x1F).</summary>
public enum ZoneMode : byte
{
    Flowing = 0x1F,
    Red = 0x20,
    Yellow = 0x21,
    Green = 0x22,
    IceBlue = 0x23,
    Blue = 0x24,
    Pink = 0x25,
    White = 0x26,
    Neon = 0x27,
    Off = 0x28,
}

/// <summary>Which controls an effect uses (the vendor's config_func bitmask).</summary>
[Flags]
public enum LightingControls : byte
{
    None = 0,
    Brightness = 0x01,
    Speed = 0x02,
    DirectionLeftRight = 0x04,
    DirectionUpDown = 0x08,
    RgbSliders = 0x10,
    ColorAndColorful = 0x20,
}

public static class LightingEffects
{
    public static LightingControls ControlsFor(LightingEffect effect) => (LightingControls)(effect switch
    {
        LightingEffect.Off => 0x00,
        LightingEffect.Static => 0x31,
        LightingEffect.Colourful or LightingEffect.Spectrum => 0x03,
        LightingEffect.Scrolling => 0x3B,
        LightingEffect.Rolling or LightingEffect.Rotating or LightingEffect.Flowing or LightingEffect.Tilt => 0x37,
        _ => 0x33,
    });
}

public sealed record LightingSettings
{
    public const int MaxBrightness = 5;
    public const int MaxSpeed = 5;

    public LightingEffect Effect { get; init; } = LightingEffect.Rolling;
    public byte Red { get; init; } = 255;
    public byte Green { get; init; } = 255;
    public byte Blue { get; init; } = 255;
    public bool Colorful { get; init; }
    public int Brightness { get; init; } = MaxBrightness;
    public int Speed { get; init; } = 3;
    public int Direction { get; init; }

    public void Validate()
    {
        if (!Enum.IsDefined(Effect)) throw new ArgumentOutOfRangeException(nameof(Effect));
        if (Brightness is < 0 or > MaxBrightness) throw new ArgumentOutOfRangeException(nameof(Brightness));
        if (Speed is < 0 or > MaxSpeed) throw new ArgumentOutOfRangeException(nameof(Speed));
        if (Direction is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(Direction));
    }
}
