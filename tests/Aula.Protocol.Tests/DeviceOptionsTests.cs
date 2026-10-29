using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class DeviceOptionsTests
{
    [Fact]
    public void Options_init_is_0417_with_bytes_2_and_8_set()
    {
        var r = Commands.OptionsInit();
        Assert.Equal("0417010000000000" + "01", FakeTransport.Hex(r, 9));
    }

    [Fact]
    public void Options_data_layout_matches_vendor()
    {
        var r = Commands.OptionsData(new DeviceOptions
        {
            DisableWinKey = true, FnToggle = true, Sleep = SleepTime.FiveMinutes, ResponseLevel = 3,
        });
        Assert.Equal("000100000101020003", FakeTransport.Hex(r, 9));
        Assert.Equal("AA55", FakeTransport.Hex(r[62..], 2));
    }

    [Fact]
    public void Lock_bytes_are_alt_tab_then_alt_f4_then_win()
    {
        Assert.Equal(1, Commands.OptionsData(new DeviceOptions { DisableAltTab = true })[2]);
        Assert.Equal(1, Commands.OptionsData(new DeviceOptions { DisableAltF4 = true })[3]);
        Assert.Equal(1, Commands.OptionsData(new DeviceOptions { DisableWinKey = true })[4]);
    }

    [Fact]
    public void Default_options_match_a_fresh_vendor_install()
    {
        Assert.Equal("000100000000000001", FakeTransport.Hex(Commands.OptionsData(new DeviceOptions()), 9));
    }

    [Fact]
    public void Options_reject_out_of_range_response_level()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Commands.OptionsData(new DeviceOptions { ResponseLevel = 6 }));
    }

    [Fact]
    public void Options_sequence_matches_vendor_order()
    {
        var t = new FakeTransport();
        new AulaKeyboard(t, sleep: _ => { }).SetOptions(new DeviceOptions());
        var summary = string.Join(" ", t.Log.Select(e => $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));
        Assert.Equal("set:0418 get:0418 set:0417 get:0417 set:0001 set:0402 get:0402", summary);
    }
}
