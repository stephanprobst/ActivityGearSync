using System.Globalization;
using System.Xml.Linq;
using ActivityGearSync.Models;

namespace ActivityGearSync.Shared;

public static class GpxExporter
{
    private static readonly XNamespace GpxNs = "http://www.topografix.com/GPX/1/1";
    private static readonly XNamespace GpxTpxNs = "http://www.garmin.com/xmlschemas/TrackPointExtension/v1";
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
            new XElement(GpxNs + "gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "ActivityGearSync"),
                new XAttribute(XNamespace.Xmlns + "gpxtpx", GpxTpxNs),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNs),
                new XAttribute(XsiNs + "schemaLocation",
                    "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                CreateMetadata(activity, startTime),
                CreateTrack(activity, streams, startTime)));

        doc.Save(filePath);
    }

    private static XElement CreateMetadata(StravaActivity activity, DateTime startTime)
    {
        return new XElement(GpxNs + "metadata",
            new XElement(GpxNs + "name", activity.Name),
            new XElement(GpxNs + "time", startTime.ToString("o", CultureInfo.InvariantCulture)));
    }

    private static XElement CreateTrack(StravaActivity activity, ActivityStreams streams, DateTime startTime)
    {
        var trackPoints = new List<XElement>();
        int pointCount = streams.LatLng!.Data.Count;

        for (int i = 0; i < pointCount; i++)
        {
            trackPoints.Add(CreateTrackPoint(streams, i, startTime));
        }

        return new XElement(GpxNs + "trk",
            new XElement(GpxNs + "name", activity.Name),
            new XElement(GpxNs + "type", MapActivityType(activity.Type)),
            new XElement(GpxNs + "trkseg", trackPoints));
    }

    private static XElement CreateTrackPoint(ActivityStreams streams, int index, DateTime startTime)
    {
        var latLngElement = streams.LatLng!.Data[index];
        double lat = latLngElement.EnumerateArray().First().GetDouble();
        double lng = latLngElement.EnumerateArray().Last().GetDouble();
        int timeOffset = streams.Time!.Data[index].GetInt32();
        var pointTime = startTime.AddSeconds(timeOffset);

        var trkpt = new XElement(GpxNs + "trkpt",
            new XAttribute("lat", lat.ToString("F7", CultureInfo.InvariantCulture)),
            new XAttribute("lon", lng.ToString("F7", CultureInfo.InvariantCulture)));

        if (streams.Altitude?.Data is { Count: > 0 } && index < streams.Altitude.Data.Count)
        {
            double ele = streams.Altitude.Data[index].GetDouble();
            trkpt.Add(new XElement(GpxNs + "ele", ele.ToString("F1", CultureInfo.InvariantCulture)));
        }

        trkpt.Add(new XElement(GpxNs + "time", pointTime.ToString("o", CultureInfo.InvariantCulture)));

        var extensions = CreateTrackPointExtensions(streams, index);
        if (extensions is not null)
        {
            trkpt.Add(extensions);
        }

        return trkpt;
    }

    private static XElement? CreateTrackPointExtensions(ActivityStreams streams, int index)
    {
        var tpxElements = new List<XElement>();

        if (streams.HeartRate?.Data is { Count: > 0 } && index < streams.HeartRate.Data.Count)
        {
            int hr = streams.HeartRate.Data[index].GetInt32();
            tpxElements.Add(new XElement(GpxTpxNs + "hr", hr));
        }

        if (streams.Cadence?.Data is { Count: > 0 } && index < streams.Cadence.Data.Count)
        {
            int cad = streams.Cadence.Data[index].GetInt32();
            tpxElements.Add(new XElement(GpxTpxNs + "cad", cad));
        }

        if (tpxElements.Count == 0)
        {
            return null;
        }

        return new XElement(GpxNs + "extensions",
            new XElement(GpxTpxNs + "TrackPointExtension", tpxElements));
    }

    private static string MapActivityType(string stravaType)
    {
        return stravaType switch
        {
            "Run" or "TrailRun" or "VirtualRun" => "running",
            "Ride" or "MountainBikeRide" or "GravelRide" or "EBikeRide" or "VirtualRide" => "cycling",
            "Walk" => "walking",
            "Hike" => "hiking",
            "Swim" or "OpenWaterSwim" => "swimming",
            _ => "other"
        };
    }
}
