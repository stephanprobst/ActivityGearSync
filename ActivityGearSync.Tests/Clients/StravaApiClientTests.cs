using System.Net;
using System.Text.Json;
using ActivityGearSync.Clients;
using ActivityGearSync.Interfaces;
using ActivityGearSync.Models;
using ActivityGearSync.Shared;
using Imposter.Abstractions;

namespace ActivityGearSync.Tests.Clients;

public class StravaApiClientTests
{
    private static StravaTokens CreateValidTokens()
    {
        return new StravaTokens
        {
            AccessToken = "test_access_token",
            RefreshToken = "test_refresh_token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };
    }

    private static StravaAthlete CreateTestAthlete()
    {
        return new StravaAthlete
        {
            Id = 12345,
            FirstName = "Test",
            LastName = "User"
        };
    }

    private static IStravaAuthClient GetMockInstance(IStravaAuthClientImposter imposter)
    {
        return ((IHaveImposterInstance<IStravaAuthClient>)imposter).Instance();
    }

    [Test]
    public async Task GetAthleteAsync_WhenNotAuthenticated_ThrowsInvalidOperationException()
    {
        // Arrange
        var authClientImposter = new IStravaAuthClientImposter();
        authClientImposter.GetValidTokensAsync().Returns(Task.FromResult<StravaTokens>(null!));

        var httpClient = new HttpClient(new TestHttpMessageHandler());
        var apiClient = new StravaApiClient(httpClient, GetMockInstance(authClientImposter));

        // Act & Assert
        await Assert.That(async () => await apiClient.GetAthleteAsync())
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task GetAthleteAsync_WithValidToken_SendsAuthorizedRequest()
    {
        // Arrange
        var tokens = CreateValidTokens();
        var authClientImposter = new IStravaAuthClientImposter();
        authClientImposter.GetValidTokensAsync().ReturnsAsync(tokens);

        var handler = new TestHttpMessageHandler();
        handler.SetResponse(HttpStatusCode.OK, CreateTestAthlete());

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.strava.com") };
        var apiClient = new StravaApiClient(httpClient, GetMockInstance(authClientImposter));

        // Act
        var result = await apiClient.GetAthleteAsync();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Id).IsEqualTo(12345);
        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Headers.Authorization).IsNotNull();
        await Assert.That(handler.LastRequest!.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(handler.LastRequest!.Headers.Authorization!.Parameter).IsEqualTo("test_access_token");
    }

    [Test]
    public async Task UpdateActivityGearAsync_WhenNotAuthenticated_ThrowsInvalidOperationException()
    {
        // Arrange
        var authClientImposter = new IStravaAuthClientImposter();
        authClientImposter.GetValidTokensAsync().Returns(Task.FromResult<StravaTokens>(null!));

        var httpClient = new HttpClient(new TestHttpMessageHandler());
        var apiClient = new StravaApiClient(httpClient, GetMockInstance(authClientImposter));

        // Act & Assert
        await Assert.That(async () => await apiClient.UpdateActivityGearAsync(123, "gear1"))
            .ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task UpdateActivityGearAsync_WithValidToken_SendsAuthorizedPutRequest()
    {
        // Arrange
        var tokens = CreateValidTokens();
        var authClientImposter = new IStravaAuthClientImposter();
        authClientImposter.GetValidTokensAsync().ReturnsAsync(tokens);

        var activity = new StravaActivity
        {
            Id = 123,
            Name = "Test Run",
            Type = "Run",
            SportType = "Run",
            StartDateLocal = DateTime.Now,
            GearId = "gear1"
        };

        var handler = new TestHttpMessageHandler();
        handler.SetResponse(HttpStatusCode.OK, activity);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.strava.com") };
        var apiClient = new StravaApiClient(httpClient, GetMockInstance(authClientImposter));

        // Act
        var result = await apiClient.UpdateActivityGearAsync(123, "gear1");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Put);
        await Assert.That(handler.LastRequest!.RequestUri!.ToString()).Contains("/activities/123");
    }

    [Test]
    public async Task GetActivityStreamsAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var tokens = CreateValidTokens();
        var authClientImposter = new IStravaAuthClientImposter();
        authClientImposter.GetValidTokensAsync().ReturnsAsync(tokens);

        var handler = new TestHttpMessageHandler();
        handler.SetResponse(HttpStatusCode.NotFound);

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://www.strava.com") };
        var apiClient = new StravaApiClient(httpClient, GetMockInstance(authClientImposter));

        // Act
        var result = await apiClient.GetActivityStreamsAsync(123);

        // Assert
        await Assert.That(result).IsNull();
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private HttpStatusCode _statusCode = HttpStatusCode.OK;
        private string _responseContent = "{}";

        public HttpRequestMessage? LastRequest { get; private set; }

        public void SetResponse(HttpStatusCode statusCode, object? content = null)
        {
            _statusCode = statusCode;
            _responseContent = content is not null
                ? JsonSerializer.Serialize(content, AppJsonContext.Default.Options)
                : "{}";
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
