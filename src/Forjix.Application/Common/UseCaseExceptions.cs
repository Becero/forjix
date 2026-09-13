namespace Forjix.Application.Common;

public sealed class RequestValidationException(params string[] errors) : Exception("Validation failed.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class ResourceNotFoundException(string message) : Exception(message);

public sealed class ResourceConflictException(string message) : Exception(message);

public sealed class PermissionDeniedException(string message) : Exception(message);
