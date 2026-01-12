using System.Xml.Linq;
using ActivityGearSync.Shared;
using ActivityGearSync.Tests.Fixtures;

namespace ActivityGearSync.Tests.Shared;

public class GpxExporterTests
{
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";

    [Test]
    public async Task Export_ValidActivity_CreatesValidGpxFile()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(name: "Morning Run");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        try
        {
            // Act
            GpxExporter.Export(activity, streams, tempFile);

            // Assert
            await Assert.That(File.Exists(tempFile)).IsTrue();

            var doc = XDocument.Load(tempFile);
            var gpx = doc.Root!;

            await Assert.That(gpx.Name.LocalName).IsEqualTo("gpx");
            await Assert.That(gpx.Attribute("version")?.Value).IsEqualTo("1.1");
            await Assert.That(gpx.Attribute("creator")?.Value).IsEqualTo("ActivityGearSync");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_ValidActivity_ContainsMetadata()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(name: "Test Run");
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        try
        {
            // Act
            GpxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var metadata = doc.Root!.Element(GpxNs + "metadata");

            await Assert.That(metadata).IsNotNull();
            await Assert.That(metadata!.Element(GpxNs + "name")?.Value).IsEqualTo("Test Run");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_ValidActivity_ContainsTrackPoints()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity();
        var streams = TestActivityFactory.CreateStreams(pointCount: 5);
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        try
        {
            // Act
            GpxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var trackPoints = doc.Descendants(GpxNs + "trkpt").ToList();

            await Assert.That(trackPoints.Count).IsEqualTo(5);
            await Assert.That(trackPoints[0].Attribute("lat")).IsNotNull();
            await Assert.That(trackPoints[0].Attribute("lon")).IsNotNull();
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_WithAltitude_IncludesElevation()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity();
        var streams = TestActivityFactory.CreateStreams(includeAltitude: true);
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        try
        {
            // Act
            GpxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            var elevations = doc.Descendants(GpxNs + "ele").ToList();

            await Assert.That(elevations.Count).IsGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Test]
    public async Task Export_NoGpsData_ThrowsInvalidOperationException()
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity();
        var streams = TestActivityFactory.CreateEmptyStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        // Act & Assert
        await Assert.That(() => GpxExporter.Export(activity, streams, tempFile))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [Arguments("Run", "running")]
    [Arguments("TrailRun", "running")]
    [Arguments("Ride", "cycling")]
    [Arguments("Walk", "walking")]
    [Arguments("Hike", "hiking")]
    [Arguments("Swim", "swimming")]
    [Arguments("Yoga", "other")]
    public async Task Export_ActivityType_MapsCorrectly(string activityType, string expectedGpxType)
    {
        // Arrange
        var activity = TestActivityFactory.CreateActivity(type: activityType);
        var streams = TestActivityFactory.CreateStreams();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.gpx");

        try
        {
            // Act
            GpxExporter.Export(activity, streams, tempFile);

            // Assert
            var doc = XDocument.Load(tempFile);
            string trackType = doc.Descendants(GpxNs + "type").First().Value;

            await Assert.That(trackType).IsEqualTo(expectedGpxType);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
