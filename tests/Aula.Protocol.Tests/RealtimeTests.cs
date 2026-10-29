using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class RealtimeTests
{
    [Fact]
    public void Realtime_frame_packs_entries_sequentially()
    {
        var f = Commands.RealtimeFrame(new Dictionary<int, (byte, byte, byte)> { [50] = (1, 2, 3), [7] = (4, 5, 6) });
        Assert.Equal(512, f.Length);
        Assert.Equal("07040506" + "32010203" + "00000000", FakeTransport.Hex(f, 12));
    }

    [Fact]
    public void Realtime_sequence_is_0420_eight_slices_then_0402()
    {
        var t = new FakeTransport();
        new AulaKeyboard(t, sleep: _ => { }).SendRealtimeFrame(new Dictionary<int, (byte, byte, byte)> { [1] = (9, 9, 9) });
        var summary = string.Join(" ", t.Log.Select(e => $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));
        Assert.Equal("set:0420 get:0420 set:0109 " + string.Join(" ", Enumerable.Repeat("set:0000", 7)) + " set:0402 get:0402", summary);
        Assert.Equal(8, t.Log[0].Data[8]);
    }
}
