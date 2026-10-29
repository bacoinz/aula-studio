using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class CommandTests
{
    [Fact]
    public void Control_commands_are_64_bytes_with_id_first()
    {
        Assert.Equal("0418", FakeTransport.Hex(Commands.Begin(), 2));
        Assert.Equal("0402", FakeTransport.Hex(Commands.Apply(), 2));
        Assert.Equal("04F0", FakeTransport.Hex(Commands.Finalize(), 2));
        Assert.All(new[] { Commands.Begin(), Commands.Apply(), Commands.Finalize() },
            r => Assert.Equal(64, r.Length));
    }

    [Fact]
    public void Lcd_header_carries_slot_and_little_endian_page_count()
    {
        var r = Commands.LcdHeader(slot: 1, pageCount: 2231);
        Assert.Equal("0472010000000000B708", FakeTransport.Hex(r, 10));
    }

    [Fact]
    public void Clock_data_matches_vendor_layout()
    {
        // Wednesday 29 Oct 2026, 12:34:56
        var r = Commands.ClockData(new DateTime(2026, 10, 29, 12, 34, 56));
        Assert.Equal("00015A1A0A1D0C223800" + "04", FakeTransport.Hex(r, 11));
        Assert.Equal(0xAA, r[62]);
        Assert.Equal(0x55, r[63]);
    }

    [Fact]
    public void Lighting_init_sets_only_byte_8()
    {
        Assert.Equal("041300000000000001", FakeTransport.Hex(Commands.LightingInit(), 9));
    }

    [Fact]
    public void Lighting_data_matches_vendor_layout()
    {
        var r = Commands.LightingData(new LightingSettings
        {
            Effect = LightingEffect.Breath, Red = 0x11, Green = 0x22, Blue = 0x33,
            Colorful = true, Brightness = 4, Speed = 2, Direction = 1,
        });
        Assert.Equal("07112233000000000104020100" + "00AA55", FakeTransport.Hex(r, 16));
    }

    [Fact]
    public void Lighting_off_leaves_parameters_zero()
    {
        var r = Commands.LightingData(new LightingSettings { Effect = LightingEffect.Off });
        Assert.Equal("0000000000000000000000000000AA55", FakeTransport.Hex(r, 16));
    }

    [Fact]
    public void Lighting_rejects_out_of_range_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Commands.LightingData(new LightingSettings { Brightness = 6 }));
    }
}
