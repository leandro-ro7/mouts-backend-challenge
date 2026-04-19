namespace Ambev.DeveloperEvaluation.Domain.Exceptions;

/// <summary>
/// Thrown when an update is rejected because another request already modified the resource.
/// Translates to HTTP 409 Conflict at the API boundary.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
