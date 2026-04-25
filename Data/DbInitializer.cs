using KTNLocation.Helpers;
using KTNLocation.Models.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTNLocation.Data;

public sealed class DbInitializer
{
    private readonly KTNLocationDbContext _dbContext;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(KTNLocationDbContext dbContext, ILogger<DbInitializer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureSqliteDirectory();

        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (!await _dbContext.CountyLocations.AnyAsync(cancellationToken))
        {
            _dbContext.CountyLocations.AddRange(GetSeedCounties());
        }

        if (!await _dbContext.IpRangeLocations.AnyAsync(cancellationToken))
        {
            _dbContext.IpRangeLocations.AddRange(GetSeedIpRanges());
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Location seed data initialized.");
        }
    }

    private void EnsureSqliteDirectory()
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(sqliteBuilder.DataSource))
        {
            return;
        }

        var fullPath = Path.IsPathRooted(sqliteBuilder.DataSource)
            ? sqliteBuilder.DataSource
            : Path.GetFullPath(sqliteBuilder.DataSource, Directory.GetCurrentDirectory());

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static IEnumerable<CountyLocation> GetSeedCounties()
    {
        return
        [
            new CountyLocation
            {
                Country = "中国",
                Province = "浙江省",
                City = "宁波市",
                County = "海曙区",
                Latitude = 29.8590,
                Longitude = 121.5490
            },
            new CountyLocation
            {
                Country = "中国",
                Province = "浙江省",
                City = "杭州市",
                County = "余杭区",
                Latitude = 30.2730,
                Longitude = 120.1190
            },
            new CountyLocation
            {
                Country = "中国",
                Province = "广东省",
                City = "深圳市",
                County = "南山区",
                Latitude = 22.5333,
                Longitude = 113.9304
            },
            new CountyLocation
            {
                Country = "中国",
                Province = "北京市",
                City = "北京市",
                County = "朝阳区",
                Latitude = 39.9215,
                Longitude = 116.4436
            },
            new CountyLocation
            {
                Country = "中国",
                Province = "上海市",
                City = "上海市",
                County = "浦东新区",
                Latitude = 31.2215,
                Longitude = 121.5441
            }
        ];
    }

    private static IEnumerable<IpRangeLocation> GetSeedIpRanges()
    {
        return
        [
            CreateIpRange("1.0.1.0", "1.0.3.255", "中国", "上海市", "上海市", "浦东新区", 31.2215, 121.5441),
            CreateIpRange("14.16.0.0", "14.31.255.255", "中国", "广东省", "深圳市", "南山区", 22.5333, 113.9304),
            CreateIpRange("36.96.0.0", "36.127.255.255", "中国", "北京市", "北京市", "朝阳区", 39.9215, 116.4436),
            CreateIpRange("39.160.0.0", "39.191.255.255", "中国", "浙江省", "杭州市", "余杭区", 30.2730, 120.1190),
            CreateIpRange("58.240.0.0", "58.255.255.255", "中国", "浙江省", "宁波市", "海曙区", 29.8590, 121.5490)
        ];
    }

    private static IpRangeLocation CreateIpRange(
        string startIp,
        string endIp,
        string country,
        string province,
        string city,
        string county,
        double latitude,
        double longitude)
    {
        if (!IpAddressHelper.TryToIPv4Number(startIp, out var startIpNumber, out _)
            || !IpAddressHelper.TryToIPv4Number(endIp, out var endIpNumber, out _))
        {
            throw new InvalidOperationException("Seed IP range has invalid IPv4 format.");
        }

        return new IpRangeLocation
        {
            StartIp = startIp,
            EndIp = endIp,
            StartIpNumber = startIpNumber,
            EndIpNumber = endIpNumber,
            Country = country,
            Province = province,
            City = city,
            County = county,
            Latitude = latitude,
            Longitude = longitude
        };
    }
}
