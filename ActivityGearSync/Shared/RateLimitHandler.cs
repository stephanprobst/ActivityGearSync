namespace ActivityGearSync.Shared;

public sealed class RateLimitHandler(RateLimiter rateLimiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Wait if we're approaching rate limits before sending
        await rateLimiter.WaitIfNeededAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        // Parse and update rate limit info from every response
        var rateLimitInfo = RateLimitHeaderParser.Parse(response.Headers);
        if (rateLimitInfo is not null)
        {
            rateLimiter.UpdateFromServer(rateLimitInfo);
        }

        return response;
    }
}
