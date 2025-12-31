using ActivityGearSync.Models;

namespace ActivityGearSync.Infrastructure;

public sealed class RateLimitExceededException : Exception
{
    public RateLimitInfo? RateLimitInfo { get; }
    public TimeSpan? RetryAfter { get; }

    public RateLimitExceededException()
    {
    }

    public RateLimitExceededException(string message)
        : base(message)
    {
    }

    public RateLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public RateLimitExceededException(string message, RateLimitInfo? rateLimitInfo, TimeSpan? retryAfter)
        : base(message)
    {
        RateLimitInfo = rateLimitInfo;
        RetryAfter = retryAfter;
    }
}
