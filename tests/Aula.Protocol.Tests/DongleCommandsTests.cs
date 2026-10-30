using Aula.Protocol;
using Xunit;

namespace Aula.Protocol.Tests;

public class DongleCommandsTests
{
    [Fact]
    public void Seal_puts_the_byte_sum_last()
    {
        var sealedReport = DongleTransport.Seal(DongleCommands.Battery());
        Assert.Equal(33, sealedReport.Length);
        Assert.Equal(0x20, sealedReport[1]);
        Assert.Equal(0x01, sealedReport[2]);
        Assert.Equal(0x21, sealedReport[32]);
    }

    [Fact]
    public void Lighting_is_the_wired_data_after_the_header()
    {
        var settings = new LightingSettings { Effect = LightingEffect.Static, Red = 1, Green = 2, Blue = 3, Brightness = 4, Speed = 2 };
        var r = DongleCommands.Lighting(settings);
        Assert.Equal(new byte[] { 0, 0x05, 0x10, 0 }, r[..4]);
        Assert.Equal(Commands.LightingData(settings)[..16], r[4..20]);
        Assert.Equal(new byte[] { 0xAA, 0x55 }, r[18..20]);
    }

    [Fact]
    public void Clock_and_options_end_with_the_marker_inside_the_small_report()
    {
        var clock = DongleCommands.Clock(new DateTime(2026, 10, 30, 14, 5, 6));
        Assert.Equal(new byte[] { 0, 0x0C, 0x10, 0, 0, 1, 0x5A, 26, 10, 30, 14, 5, 6 }, clock[..13]);
        Assert.Equal(new byte[] { 0xAA, 0x55 }, clock[18..20]);

        var options = DongleCommands.Options(new DeviceOptions { DisableWinKey = true });
        Assert.Equal(new byte[] { 0, 0x07, 0x10, 0, 0, 1 }, options[..6]);
        Assert.Equal(1, options[8]);
        Assert.Equal(new byte[] { 0xAA, 0x55 }, options[18..20]);
    }

    [Theory]
    [InlineData(KeyLayer.Normal, 0x10)]
    [InlineData(KeyLayer.Fn, 0x12)]
    public void Key_maps_take_21_chunks_with_a_short_last_one(KeyLayer layer, byte command)
    {
        var chunks = DongleCommands.KeyMap(layer, new Dictionary<int, KeyAction> { [1] = KeyAction.Key(0x04) }).ToList();
        Assert.Equal(21, chunks.Count);
        Assert.All(chunks, c => Assert.Equal(command, c[1]));
        Assert.Equal(0x1C, chunks[0][2]);
        Assert.Equal(0x10, chunks[20][2]);
        Assert.Equal(20, chunks[20][3]);
        Assert.Equal(new byte[] { 0x02, 0, 0x04, 0 }, chunks[0][8..12]);
        // The marker at 574 falls in the last chunk, at 14 of its 16 bytes.
        Assert.Equal(new byte[] { 0xAA, 0x55 }, chunks[20][18..20]);
    }

    [Fact]
    public void Macros_carry_the_leftover_length_and_the_end_marker_in_an_extra_chunk()
    {
        IReadOnlyList<MacroEvent> macro = [new(MacroEventKind.KeyDown, 0x04, 20), new(MacroEventKind.KeyUp, 0x04, 20)];
        var chunks = DongleCommands.Macros([macro]).ToList();
        var used = 400 + 8 + 2 * 8;
        Assert.Equal(used / 28 + 2, chunks.Count);
        Assert.All(chunks, c => Assert.Equal(0x09, c[1]));
        Assert.Equal(0x1C, chunks[0][2]);
        Assert.Equal(used % 28, chunks[^1][2]);
        Assert.Equal(new byte[] { 0xAA, 0x55 }, chunks[^1][30..32]);
    }

    [Fact]
    public void Music_scales_levels_to_rows()
    {
        var levels = new float[21];
        levels[0] = 1f;
        levels[1] = 0.5f;
        levels[20] = 2f;
        var r = DongleCommands.Music(levels, 1, 2, 3);
        Assert.Equal(new byte[] { 0, 0x0B, 0x1C, 0, 1, 2, 3, 6, 3 }, r[..9]);
        Assert.Equal(6, r[27]);
    }
}
