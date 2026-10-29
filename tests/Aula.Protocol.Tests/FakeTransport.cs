using Aula.Protocol;

namespace Aula.Protocol.Tests;

/// <summary>Records every report and page; acknowledges pages unless told to stop.</summary>
internal sealed class FakeTransport : IAulaTransport
{
    public List<(string Kind, byte[] Data)> Log { get; } = [];
    public int? StopAckingAtPage { get; init; }
    private int _pages;

    public void SetFeature(ReadOnlySpan<byte> report) => Log.Add(("set", report.ToArray()));

    public byte[] GetFeature(ReadOnlySpan<byte> request)
    {
        Log.Add(("get", request.ToArray()));
        var reply = request.ToArray();
        reply[3] = 0x01;
        return reply;
    }

    public byte[]? WriteLcdPage(ReadOnlySpan<byte> page, TimeSpan ackTimeout)
    {
        Log.Add(("page", page.ToArray()));
        return _pages++ == StopAckingAtPage ? null : [0x01, 0x5A, 0x02, 0x00];
    }

    public void Dispose() { }

    public static string Hex(byte[] data, int count) => Convert.ToHexString(data, 0, count);
}
