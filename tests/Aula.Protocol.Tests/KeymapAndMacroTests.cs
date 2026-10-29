using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class KeymapAndMacroTests
{
    [Fact]
    public void Key_actions_match_vendor_encodings()
    {
        Assert.Equal(new KeyAction(0x02, 0x00, 0x04, 0), KeyAction.Key(0x04));
        Assert.Equal(new KeyAction(0x02, 0x01, 0x06, 0), KeyAction.Key(0x06, KeyModifiers.LeftCtrl));
        // A lone modifier becomes its bit, with no usage.
        Assert.Equal(new KeyAction(0x02, 0x02, 0x00, 0), KeyAction.Key(0xE1));
        Assert.Equal(new KeyAction(0x01, 0x03, 0xFF, 0), KeyAction.Mouse(MouseAction.WheelDown));
        Assert.Equal(new KeyAction(0x03, 0x92, 0x01, 0), KeyAction.Consumer(0x192));
        Assert.Equal(new KeyAction(0x06, 0x02, 0x01, 0x05), KeyAction.Macro(2, MacroPlayMode.Repeat, 5));
        Assert.Equal(new KeyAction(0x05, 0x03, 0, 0), KeyAction.Disabled);
    }

    [Fact]
    public void Key_action_round_trips_through_uint()
    {
        var a = KeyAction.Macro(7, MacroPlayMode.WhileHeld);
        Assert.Equal(a, KeyAction.FromUInt32(a.ToUInt32()));
    }

    [Fact]
    public void Key_map_init_uses_11_or_27_with_byte8_9()
    {
        Assert.Equal("0411", FakeTransport.Hex(Commands.KeyMapInit(KeyLayer.Normal), 2));
        Assert.Equal("0427", FakeTransport.Hex(Commands.KeyMapInit(KeyLayer.Fn), 2));
        Assert.Equal(9, Commands.KeyMapInit(KeyLayer.Fn)[8]);
    }

    [Fact]
    public void Key_map_table_puts_actions_at_index_times_4()
    {
        var t = Commands.KeyMapTable(new Dictionary<int, KeyAction> { [2] = KeyAction.Key(0x04) });
        Assert.Equal(576, t.Length);
        Assert.Equal("02000400", FakeTransport.Hex(t[8..12], 4));
        Assert.Equal(0, t[4]);
        Assert.Equal("AA55", FakeTransport.Hex(t[574..], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Commands.KeyMapTable(new Dictionary<int, KeyAction> { [143] = KeyAction.Key(4) }));
    }

    [Fact]
    public void Key_map_sequence_matches_vendor_order()
    {
        var t = new FakeTransport();
        new AulaKeyboard(t, sleep: _ => { }).SetKeyMap(KeyLayer.Normal, new Dictionary<int, KeyAction>());
        var summary = string.Join(" ", t.Log.Select(e => $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));
        Assert.Equal(
            "set:0418 get:0418 set:0411 get:0411 " +
            string.Join(" ", Enumerable.Repeat("set:0000", 9)) +
            " set:0402 get:0402 set:04F0 get:04F0",
            summary);
    }

    [Fact]
    public void Macro_buffer_layout_matches_vendor()
    {
        var b = MacroSet.Build([
            [new MacroEvent(MacroEventKind.KeyDown, 0x04, 50), new MacroEvent(MacroEventKind.KeyUp, 0x04, 3)],
            [],
        ]);
        // Slot 0 at offset 400, slot 1 empty.
        Assert.Equal("90010000", FakeTransport.Hex(b[0..4], 4));
        Assert.Equal("FFFFFFFF", FakeTransport.Hex(b[4..8], 4));
        Assert.Equal("0400000000000000", FakeTransport.Hex(b[400..408], 8));
        Assert.Equal("000004B0" + "32000050" + "00000430" + "0A000050", FakeTransport.Hex(b[408..424], 16));
        // 424 bytes → 6 full reports + 1 partial + the vendor's extra one = 8 reports.
        Assert.Equal(8 * 64, b.Length);
        Assert.Equal("AA55", FakeTransport.Hex(b[^2..], 2));
    }

    [Fact]
    public void Macro_buffer_adds_an_extra_report_on_exact_multiples()
    {
        // 400 + 8 + 5·8 = 448 = 7 reports exactly; the vendor still adds one.
        var events = Enumerable.Repeat(new MacroEvent(MacroEventKind.KeyDown, 4, 10), 5).ToList();
        Assert.Equal((448 / 64 + 1) * 64, MacroSet.Build([events]).Length);
    }

    [Fact]
    public void Macro_sequence_matches_vendor_order()
    {
        var t = new FakeTransport();
        new AulaKeyboard(t, sleep: _ => { }).SetMacros([[new MacroEvent(MacroEventKind.KeyDown, 4, 10)]]);
        var summary = string.Join(" ", t.Log.Select(e => $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));
        Assert.StartsWith("set:0419 get:0419 set:0415 get:0415 set:9001", summary);
        Assert.EndsWith("set:0402 get:0402", summary);
        Assert.Equal(8, t.Log[2].Data[8]);
    }

    [Fact]
    public void Macro_buffer_rejects_overflow()
    {
        var big = Enumerable.Repeat(new MacroEvent(MacroEventKind.KeyDown, 4, 10), 400).ToList();
        Assert.Throws<ArgumentException>(() => MacroSet.Build([big]));
    }
}
