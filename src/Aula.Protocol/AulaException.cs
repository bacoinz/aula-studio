namespace Aula.Protocol;

public class AulaException(string message) : Exception(message);

/// <summary>Raised when a request would break a hardware safety rule; nothing was sent.</summary>
public sealed class AulaSafetyException(string message) : AulaException(message);
