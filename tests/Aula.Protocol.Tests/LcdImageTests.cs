using Aula.Protocol;

namespace Aula.Protocol.Tests;

public class LcdImageTests
{
    private static byte[] Frame(byte fill = 0)
    {
        var f = new byte[LcdImage.FrameBytes];
        Array.Fill(f, fill);
        return f;
    }

    [Fact]
    public void Single_frame_uses_16_pages_with_header_and_ff_padding()
    {
        var img = LcdImage.Create([Frame()], [100]);
        var b = img.Buffer.Span;

        Assert.Equal(16, img.PageCount);
        Assert.Equal(1, b[0]);
        Assert.Equal(50, b[1]);
        Assert.True(b[2..256].ToArray().All(x => x == 0xFF));
        Assert.True(b[256..(256 + LcdImage.FrameBytes)].ToArray().All(x => x == 0));
        Assert.True(b[(256 + LcdImage.FrameBytes)..].ToArray().All(x => x == 0xFF));
    }

    [Fact]
    public void Maximum_of_141_frames_is_accepted()
    {
        var frames = Enumerable.Range(0, LcdImage.MaxFrames).Select(_ => Frame()).ToList();
        var img = LcdImage.Create(frames, frames.Select(_ => 40).ToList());
        Assert.Equal(141, img.FrameCount);
        Assert.Equal(LcdImage.PagesFor(141), img.PageCount);
        Assert.Equal(2231, img.PageCount);
    }

    [Fact]
    public void More_than_141_frames_is_refused()
    {
        var frames = Enumerable.Range(0, LcdImage.MaxFrames + 1).Select(_ => Frame()).ToList();
        Assert.Throws<AulaSafetyException>(() => LcdImage.Create(frames, frames.Select(_ => 40).ToList()));
    }

    [Fact]
    public void Wrong_frame_size_is_refused()
    {
        Assert.Throws<AulaSafetyException>(() => LcdImage.Create([new byte[100]], [100]));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 50)]
    [InlineData(510, 255)]
    [InlineData(5000, 255)]
    public void Delay_is_encoded_in_2ms_steps(int ms, byte expected) =>
        Assert.Equal(expected, LcdImage.EncodeDelay(ms));

    [Fact]
    public void Solid_red_is_rgb565_little_endian()
    {
        var b = LcdImage.Solid(255, 0, 0).Buffer.Span;
        Assert.Equal(0x00, b[256]);
        Assert.Equal(0xF8, b[257]);
    }
}
