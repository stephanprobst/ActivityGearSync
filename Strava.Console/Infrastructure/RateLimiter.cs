namespace Strava.Console.Infrastructure;

public sealed class RateLimiter
{
    private readonly Queue<DateTime> _requestTimes = new();
    private const int MaxRequestsPer15Min = 100;
    private const int SafetyBuffer = 5;

    public int RemainingRequests
    {
        get
        {
            CleanupOldRequests();
            return MaxRequestsPer15Min - _requestTimes.Count;
        }
    }

    public async Task WaitIfNeededAsync(CancellationToken cancellationToken = default)
    {
        CleanupOldRequests();

        if (_requestTimes.Count >= MaxRequestsPer15Min - SafetyBuffer)
        {
            var oldestRequest = _requestTimes.Peek();
            var waitUntil = oldestRequest.AddMinutes(15);
            var waitTime = waitUntil - DateTime.UtcNow;

            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken);
                CleanupOldRequests();
            }
        }

        _requestTimes.Enqueue(DateTime.UtcNow);
    }

    public void RecordRequest()
    {
        _requestTimes.Enqueue(DateTime.UtcNow);
    }

    private void CleanupOldRequests()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-15);
        while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
        {
            _requestTimes.Dequeue();
        }
    }
}
