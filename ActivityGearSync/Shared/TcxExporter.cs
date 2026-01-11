using System.Globalization;
using System.Xml.Linq;
using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

public static class TcxExporter
{
    private static readonly XNamespace TcxNs = "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2";
    private static readonly XNamespace TpxNs = "http://www.garmin.com/xmlschemas/ActivityExtension/v2";
    private static readonly XNamespace XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

    public static void Export(StravaActivity activity, ActivityStreams streams, string filePath)
    {
        if (streams.LatLng?.Data is null || streams.Time?.Data is null)
        {
            throw new InvalidOperationException("Activity has no GPS data");
        }

        var startTime = activity.StartDateLocal.ToUniversalTime();

        var doc = new XDocument(
            new XDeclaration(version: "1.0", encoding: "UTF-8", standalone: null),
            new XElement(TcxNs + "TrainingCenterDatabase",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNs),
                new XAttribute(XsiNs + "schemaLocation",
                    "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2 " +
                    "http://www.garmin.com/xmlschemas/TrainingCenterDatabasev2.xsd"),
                new XElement(TcxNs + "Activities",
                    CreateActivity(activity, streams, startTime))));

        doc.Save(filePath);
    }

    private static XElement CreateActivity(StravaActivity activity, ActivityStreams streams, DateTime startTime)
    {
        return new XElement(TcxNs + "Activity",
            new XAttribute("Sport", MapSportType(activity.Type)),
            new XElement(TcxNs + "Id", startTime.ToString("o", CultureInfo.InvariantCulture)),
            CreateLap(activity, streams, startTime),
            new XElement(TcxNs + "Notes", activity.Name));
    }

    private static XElement CreateLap(StravaActivity activity, ActivityStreams streams, DateTime startTime)
    {
        var trackPoints = new List<XElement>();
        int pointCount = streams.LatLng!.Data.Count;
        double cumulativeDistance = 0;
        double[]? prevLatLng = null;

        for (int i = 0; i < pointCount; i++)
        {
            var latLngElement = streams.LatLng.Data[i];
            double lat = latLngElement.EnumerateArray().First().GetDouble();
            double lng = latLngElement.EnumerateArray().Last().GetDouble();

            if (prevLatLng is not null)
            {
                cumulativeDistance += HaversineDistance(prevLatLng[0], prevLatLng[1], lat, lng);
            }

            prevLatLng = [lat, lng];
            trackPoints.Add(CreateTrackpoint(streams, i, startTime, cumulativeDistance));
        }

        return new XElement(TcxNs + "Lap",
            new XAttribute("StartTime", startTime.ToString("o", CultureInfo.InvariantCulture)),
            new XElement(TcxNs + "TotalTimeSeconds", activity.MovingTime),
            new XElement(TcxNs + "DistanceMeters", activity.Distance.ToString("F1", CultureInfo.InvariantCulture)),
            new XElement(TcxNs + "TriggerMethod", "Manual"),
            new XElement(TcxNs + "Track", trackPoints));
    }

    private static XElement CreateTrackpoint(ActivityStreams streams, int index, DateTime startTime, double distance)
    {
        var latLngElement = streams.LatLng!.Data[index];
        double lat = latLngElement.EnumerateArray().First().GetDouble();
        double lng = latLngElement.EnumerateArray().Last().GetDouble();
        int timeOffset = streams.Time!.Data[index].GetInt32();
        var pointTime = startTime.AddSeconds(timeOffset);

        var trackpoint = new XElement(TcxNs + "Trackpoint",
            new XElement(TcxNs + "Time", pointTime.ToString("o", CultureInfo.InvariantCulture)),
            new XElement(TcxNs + "Position",
                new XElement(TcxNs + "LatitudeDegrees", lat.ToString("F7", CultureInfo.InvariantCulture)),
                new XElement(TcxNs + "LongitudeDegrees", lng.ToString("F7", CultureInfo.InvariantCulture))));

        if (streams.Altitude?.Data is { Count: > 0 } && index < streams.Altitude.Data.Count)
        {
            double ele = streams.Altitude.Data[index].GetDouble();
            trackpoint.Add(new XElement(TcxNs + "AltitudeMeters", ele.ToString("F1", CultureInfo.InvariantCulture)));
        }

        trackpoint.Add(new XElement(TcxNs + "DistanceMeters", distance.ToString("F1", CultureInfo.InvariantCulture)));

        if (streams.HeartRate?.Data is { Count: > 0 } && index < streams.HeartRate.Data.Count)
        {
            int hr = streams.HeartRate.Data[index].GetInt32();
            trackpoint.Add(new XElement(TcxNs + "HeartRateBpm",
                new XElement(TcxNs + "Value", hr)));
        }

        if (streams.Cadence?.Data is { Count: > 0 } && index < streams.Cadence.Data.Count)
        {
            int cad = streams.Cadence.Data[index].GetInt32();
            trackpoint.Add(new XElement(TcxNs + "Cadence", cad));
        }

        var extensions = CreateTrackpointExtensions(streams, index);
        if (extensions is not null)
        {
            trackpoint.Add(extensions);
        }

        return trackpoint;
    }

    private static XElement? CreateTrackpointExtensions(ActivityStreams streams, int index)
    {
        var tpxElements = new List<XElement>();

        if (streams.Watts?.Data is { Count: > 0 } && index < streams.Watts.Data.Count)
        {
            int watts = streams.Watts.Data[index].GetInt32();
            tpxElements.Add(new XElement(TpxNs + "Watts", watts));
        }

        if (tpxElements.Count == 0)
        {
            return null;
        }

        return new XElement(TcxNs + "Extensions",
            new XElement(TpxNs + "TPX", tpxElements));
    }

    private static string MapSportType(string stravaType)
    {
        return stravaType switch
        {
            "Run" or "TrailRun" or "VirtualRun" => "Running",
            "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide" => "Biking",
            _ => "Other"
        };
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusMeters = 6371000;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                   (Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
