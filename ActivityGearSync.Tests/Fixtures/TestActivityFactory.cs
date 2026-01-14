using System.Text.Json;
using ActivityGearSync.Models;

namespace ActivityGearSync.Tests.Fixtures;

public static class TestActivityFactory
{
    public static StravaActivity CreateActivity(
        long id = 1,
        string name = "Test Activity",
        string type = "Run",
        string sportType = "Run",
        DateTime? startDate = null,
        float distance = 5000,
        int movingTime = 1800,
        string? gearId = null,
        bool commute = false,
        bool trainer = false,
        bool isPrivate = false,
        double[]? startLatLng = null)
    {
        return new StravaActivity
        {
            Id = id,
            Name = name,
            Type = type,
            SportType = sportType,
            StartDateLocal = startDate ?? DateTime.Now,
            Distance = distance,
            MovingTime = movingTime,
            GearId = gearId,
            Commute = commute,
            Trainer = trainer,
            Private = isPrivate,
            StartLatLng = startLatLng
        };
    }

    public static ActivityStreams CreateStreams(
        int pointCount = 10,
        bool includeAltitude = true,
        bool includeHeartRate = false,
        bool includeCadence = false,
        bool includeWatts = false)
    {
        var latLngData = new List<JsonElement>();
        var timeData = new List<JsonElement>();
        var altitudeData = includeAltitude ? new List<JsonElement>() : null;
        var heartRateData = includeHeartRate ? new List<JsonElement>() : null;
        var cadenceData = includeCadence ? new List<JsonElement>() : null;
        var wattsData = includeWatts ? new List<JsonElement>() : null;

        const double baseLat = 47.3769;
        const double baseLng = 8.5417;

        for (int i = 0; i < pointCount; i++)
        {
            latLngData.Add(JsonSerializer.SerializeToElement(new[] { baseLat + (i * 0.0001), baseLng + (i * 0.0001) }));
            timeData.Add(JsonSerializer.SerializeToElement(i * 10));
            altitudeData?.Add(JsonSerializer.SerializeToElement(400.0 + i));
            heartRateData?.Add(JsonSerializer.SerializeToElement(140 + i));
            cadenceData?.Add(JsonSerializer.SerializeToElement(80 + (i % 10)));
            wattsData?.Add(JsonSerializer.SerializeToElement(200 + (i * 5)));
        }

        return new ActivityStreams
        {
            LatLng = new StreamData { Data = latLngData },
            Time = new StreamData { Data = timeData },
            Altitude = altitudeData is not null ? new StreamData { Data = altitudeData } : null,
            HeartRate = heartRateData is not null ? new StreamData { Data = heartRateData } : null,
            Cadence = cadenceData is not null ? new StreamData { Data = cadenceData } : null,
            Watts = wattsData is not null ? new StreamData { Data = wattsData } : null
        };
    }

    public static ActivityStreams CreateEmptyStreams()
    {
        return new ActivityStreams
        {
            LatLng = null,
            Time = null
        };
    }

    public static RateLimitInfo CreateRateLimitInfo(
        int shortTermLimit = 100,
        int shortTermUsage = 50,
        int dailyLimit = 1000,
        int dailyUsage = 200)
    {
        return new RateLimitInfo(
            ShortTermLimit: shortTermLimit,
            ShortTermUsage: shortTermUsage,
            DailyLimit: dailyLimit,
            DailyUsage: dailyUsage,
            Timestamp: DateTime.UtcNow);
    }
}
