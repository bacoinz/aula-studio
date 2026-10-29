namespace Aula.Protocol;

/// <summary>
/// Raw access to the two vendor interfaces of the F108 Pro:
/// the configuration interface (64-byte feature reports) and the LCD interface (4096-byte output pages).
/// </summary>
public interface IAulaTransport : IDisposable
{
    /// <summary>Sends a 64-byte feature report (report ID 0 is added by the transport).</summary>
    void SetFeature(ReadOnlySpan<byte> report);

    /// <summary>
    /// Reads a 64-byte feature report. The vendor app pre-fills the buffer with the command it just sent,
    /// so callers pass that command as <paramref name="request"/>.
    /// </summary>
    byte[] GetFeature(ReadOnlySpan<byte> request);

    /// <summary>Writes one 4096-byte LCD page and waits for the keyboard's acknowledgement.</summary>
    /// <returns>The acknowledgement bytes, or null when none arrived within the timeout.</returns>
    byte[]? WriteLcdPage(ReadOnlySpan<byte> page, TimeSpan ackTimeout);
}
