namespace KTNLocation.Helpers;

public static class CoordinateConverter
{
    private const double A = 6378245.0;
    private const double Ee = 0.00669342162296594323;

    public static bool IsOutOfChina(double lat, double lon)
        => lon < 72.004 || lon > 137.8347 || lat < 0.8293 || lat > 55.8271;

    public static (double Lat, double Lon) Gcj02ToWgs84(double gcjLat, double gcjLon)
    {
        if (IsOutOfChina(gcjLat, gcjLon)) return (gcjLat, gcjLon);
        var wLat = gcjLat; var wLon = gcjLon;
        for (var i = 0; i < 6; i++)
        {
            var (gLat, gLon) = Wgs84ToGcj02(wLat, wLon);
            wLat += gcjLat - gLat;
            wLon += gcjLon - gLon;
        }
        return (wLat, wLon);
    }

    public static (double Lat, double Lon) Wgs84ToGcj02(double wgsLat, double wgsLon)
    {
        if (IsOutOfChina(wgsLat, wgsLon)) return (wgsLat, wgsLon);
        var (dLat, dLon) = Delta(wgsLat, wgsLon);
        return (wgsLat + dLat, wgsLon + dLon);
    }

    private static (double DLat, double DLon) Delta(double lat, double lon)
    {
        var dLat = TransformLat(lon - 105.0, lat - 35.0);
        var dLon = TransformLon(lon - 105.0, lat - 35.0);
        var radLat = lat / 180.0 * Math.PI;
        var magic = Math.Sin(radLat);
        magic = 1 - Ee * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);
        dLat = dLat * 180.0 / (A * (1 - Ee) / (magic * sqrtMagic) * Math.PI);
        dLon = dLon * 180.0 / (A / sqrtMagic * Math.Cos(radLat) * Math.PI);
        return (dLat, dLon);
    }

    private static double TransformLat(double x, double y)
    {
        var r = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x));
        r += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        r += (20.0 * Math.Sin(y * Math.PI) + 40.0 * Math.Sin(y / 3.0 * Math.PI)) * 2.0 / 3.0;
        r += (160.0 * Math.Sin(y / 12.0 * Math.PI) + 320 * Math.Sin(y * Math.PI / 30.0)) * 2.0 / 3.0;
        return r;
    }

    private static double TransformLon(double x, double y)
    {
        var r = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x));
        r += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        r += (20.0 * Math.Sin(x * Math.PI) + 40.0 * Math.Sin(x / 3.0 * Math.PI)) * 2.0 / 3.0;
        r += (150.0 * Math.Sin(x / 12.0 * Math.PI) + 300.0 * Math.Sin(x / 30.0 * Math.PI)) * 2.0 / 3.0;
        return r;
    }
}
