using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class PerKeyAndZoneTests
{
    [Fact]
    public void Zone_data_sets_only_mode_and_trailer()
    {
        var r = Commands.ZoneData(ZoneMode.Red);
        Assert.Equal("20" + new string('0', 26) + "AA55", FakeTransport.Hex(r, 16));
    }

    [Fact]
    public void User_light_mode_is_0x80_with_brightness_in_byte_9()
    {
        var r = Commands.UserLightModeData(4);
        Assert.Equal("80" + new string('0', 16) + "04" + new string('0', 8) + "AA55", FakeTransport.Hex(r, 16));
    }

    [Fact]
    public void Per_key_table_puts_index_and_rgb_at_index_times_4()
    {
        var t = Commands.PerKeyTable(new Dictionary<int, (byte, byte, byte)>
        {
            [1] = (0x11, 0x22, 0x33),
            [123] = (0xAA, 0xBB, 0xCC),
        });
        Assert.Equal(576, t.Length);
        Assert.Equal("01112233", FakeTransport.Hex(t[4..8], 4));
        Assert.Equal("7BAABBCC", FakeTransport.Hex(t[492..496], 4));
        Assert.Equal(0, t[8]);
        Assert.Equal(0xAA, t[574]);
        Assert.Equal(0x55, t[575]);
    }

    [Fact]
    public void Per_key_table_rejects_indices_that_would_hit_the_trailer()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Commands.PerKeyTable(new Dictionary<int, (byte, byte, byte)> { [143] = (1, 2, 3) }));
    }

    [Fact]
    public void Per_key_sequence_matches_vendor_order()
    {
        var t = new FakeTransport();
        var kb = new AulaKeyboard(t, sleep: _ => { });
        kb.SetPerKeyColors(new Dictionary<int, (byte, byte, byte)> { [1] = (255, 0, 0) }, brightness: 5);

        var summary = string.Join(" ", t.Log.Select(e => $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));
        Assert.Equal(
            "set:0418 get:0418 set:0413 get:0413 set:8000 set:0402 get:0402 set:04F0 " +
            "set:0418 get:0418 set:0423 get:0423 " +
            string.Join(" ", Enumerable.Repeat("set:0000", 9)) +
            " set:0402 get:0402 set:04F0 get:04F0",
            summary);
        Assert.Equal(9, t.Log.First(e => e.Data[1] == 0x23).Data[8]);
        // The first slice carries slot 1 at bytes 4–7.
        Assert.Equal("01FF0000", FakeTransport.Hex(t.Log[12].Data[4..8], 4));
    }
}
