using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class KeyboardSequenceTests
{
    private static (AulaKeyboard Kb, FakeTransport T, List<TimeSpan> Sleeps) Create(int? stopAt = null)
    {
        var t = new FakeTransport { StopAckingAtPage = stopAt };
        var sleeps = new List<TimeSpan>();
        return (new AulaKeyboard(t, sleep: sleeps.Add), t, sleeps);
    }

    private static string Summary(FakeTransport t) =>
        string.Join(" ", t.Log.Select(e => e.Kind == "page" ? "page" : $"{e.Kind}:{FakeTransport.Hex(e.Data, 2)}"));

    [Fact]
    public void Clock_sync_reads_back_every_step()
    {
        var (kb, t, sleeps) = Create();
        kb.SyncClock(new DateTime(2026, 1, 1));
        Assert.Equal("set:0418 get:0418 set:0428 get:0428 set:0001 get:0001 set:0402 get:0402", Summary(t));
        Assert.All(sleeps, s => Assert.Equal(TimeSpan.FromMilliseconds(35), s));
    }

    [Fact]
    public void Lighting_skips_readback_on_data_and_finalize()
    {
        var (kb, t, _) = Create();
        kb.SetLighting(new LightingSettings { Effect = LightingEffect.Static });
        Assert.Equal("set:0418 get:0418 set:0413 get:0413 set:01FF set:0402 get:0402 set:04F0", Summary(t));
    }

    [Fact]
    public void Lcd_upload_sends_header_pages_then_apply()
    {
        var (kb, t, _) = Create();
        var img = LcdImage.Solid(0, 0, 255);
        kb.UploadLcdImage(img);

        var pages = t.Log.Where(e => e.Kind == "page").ToList();
        Assert.Equal(16, pages.Count);
        Assert.Equal(img.Buffer.ToArray(), pages.SelectMany(p => p.Data).ToArray());
        Assert.StartsWith("set:0418 get:0418 set:0472 get:0472 page", Summary(t));
        Assert.EndsWith("page set:0402 get:0402", Summary(t));
        Assert.Equal(16, t.Log.First(e => e.Kind == "set" && e.Data[1] == 0x72).Data[8]);
    }

    [Fact]
    public void Lcd_upload_aborts_without_apply_when_ack_is_missing()
    {
        var (kb, t, _) = Create(stopAt: 3);
        Assert.Throws<AulaException>(() => kb.UploadLcdImage(LcdImage.Solid(0, 0, 0)));
        Assert.Equal(4, t.Log.Count(e => e.Kind == "page"));
        Assert.DoesNotContain(t.Log, e => e.Kind == "set" && e.Data[1] == 0x02);
    }
}
