namespace KTNLocation.Options;

public sealed class GeoProviderOptions
{
    public const string SectionName = "GeoProviders";

    public bool AutoDownloadOnStartup { get; set; } = true;
    public bool WaitForDownloadOnStartup { get; set; } = true;
    public int UpdateIntervalHours { get; set; } = 24;
    public string[] ProviderOrder { get; set; } = ["ip2region", "geolite", "loyalsoldier", "geoip2cn"];

    public string Ip2RegionV4XdbPath { get; set; } = "GeoData/ip2region/ip2region_v4.xdb";
    public string Ip2RegionV6XdbPath { get; set; } = "GeoData/ip2region/ip2region_v6.xdb";
    public string Ip2RegionV4XdbUrl { get; set; } = "https://raw.githubusercontent.com/lionsoul2014/ip2region/master/data/ip2region_v4.xdb";
    public string Ip2RegionV6XdbUrl { get; set; } = "https://raw.githubusercontent.com/lionsoul2014/ip2region/master/data/ip2region_v6.xdb";

    public string GeoLiteCountryMmdbPath { get; set; } = "GeoData/geolite/GeoLite2-Country.mmdb";
    public string GeoLiteCountryMmdbUrl { get; set; } = "https://github.com/P3TERX/GeoLite.mmdb/raw/download/GeoLite2-Country.mmdb";

    public string LoyalSoldierCountryMmdbPath { get; set; } = "GeoData/loyalsoldier/Country.mmdb";
    public string LoyalSoldierCountryMmdbUrl { get; set; } = "https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb";

    public string GeoIP2CnMmdbPath { get; set; } = "GeoData/geoip2cn/Country.mmdb";
    public string GeoIP2CnMmdbUrl { get; set; } = "https://raw.githubusercontent.com/Hackl0us/GeoIP2-CN/release/Country.mmdb";
}
