using System.Globalization;
using System.Net.Http.Headers;
using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

public static class RateLimitHeaderParser
{
    private static class HeaderNames
    {
        public const string ReadLimit = "X-ReadRateLimit-Limit";
        public const string ReadUsage = "X-ReadRateLimit-Usage";
        public const string Limit = "X-RateLimit-Limit";
        public const string Usage = "X-RateLimit-Usage";
        public const string RetryAfter = "Retry-After";
    }

    public static RateLimitInfo? Parse(HttpResponseHeaders headers)
    {
        // Try read-specific headers first (more restrictive for this app)
        if (TryParseRateLimitPair(
            headers,
            HeaderNames.ReadLimit,
            HeaderNames.ReadUsage,
            out var info))
        {
            return info;
        }

        // Fall back to general headers
        if (TryParseRateLimitPair(
            headers,
            HeaderNames.Limit,
            HeaderNames.Usage,
            out info))
        {
            return info;
        }

        return null;
    }

    public static TimeSpan? ParseRetryAfter(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues(HeaderNames.RetryAfter, out var values))
        {
            return null;
        }

        string? retryAfterValue = values.FirstOrDefault();
        if (string.IsNullOrEmpty(retryAfterValue))
        {
            return null;
        }

        // Try parsing as seconds first (most common)
        if (int.TryParse(retryAfterValue, CultureInfo.InvariantCulture, out int seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Try parsing as HTTP-date
        if (DateTimeOffset.TryParse(retryAfterValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retryDate))
        {
            var delay = retryDate - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }

    private static bool TryParseRateLimitPair(
        HttpResponseHeaders headers,
        string limitHeader,
        string usageHeader,
        out RateLimitInfo? info)
    {
        info = null;

        if (!headers.TryGetValues(limitHeader, out var limitValues) ||
            !headers.TryGetValues(usageHeader, out var usageValues))
        {
            return false;
        }

        string? limitValue = limitValues.FirstOrDefault();
        string? usageValue = usageValues.FirstOrDefault();

        if (string.IsNullOrEmpty(limitValue) || string.IsNullOrEmpty(usageValue))
        {
            return false;
        }

        // Parse "shortTerm,daily" format
        if (!TryParseCommaSeparatedInts(limitValue, out int shortTermLimit, out int dailyLimit) ||
            !TryParseCommaSeparatedInts(usageValue, out int shortTermUsage, out int dailyUsage))
        {
            return false;
        }

        info = new RateLimitInfo(
            shortTermLimit,
            shortTermUsage,
            dailyLimit,
            dailyUsage,
            DateTime.UtcNow);

        return true;
    }

    private static bool TryParseCommaSeparatedInts(string value, out int first, out int second)
    {
        first = 0;
        second = 0;

        string[] parts = value.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0].Trim(), CultureInfo.InvariantCulture, out first) &&
               int.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out second);
    }
}
