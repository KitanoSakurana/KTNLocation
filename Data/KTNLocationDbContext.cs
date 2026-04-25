using KTNLocation.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KTNLocation.Data;

public sealed class KTNLocationDbContext : DbContext
{
    public KTNLocationDbContext(DbContextOptions<KTNLocationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CountyLocation> CountyLocations => Set<CountyLocation>();

    public DbSet<IpRangeLocation> IpRangeLocations => Set<IpRangeLocation>();

    public DbSet<ClientPublicKey> ClientPublicKeys => Set<ClientPublicKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClientPublicKey>()
            .HasKey(x => x.ClientId);

        modelBuilder.Entity<CountyLocation>()
            .HasIndex(x => new { x.Province, x.City, x.County });

        modelBuilder.Entity<IpRangeLocation>()
            .HasIndex(x => new { x.StartIpNumber, x.EndIpNumber });

        modelBuilder.Entity<IpRangeLocation>()
            .HasIndex(x => new { x.Province, x.City, x.County });

        base.OnModelCreating(modelBuilder);
    }
}
