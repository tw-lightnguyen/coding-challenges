using CodingChallenge.DriverManagement.Models;
using CodingChallenge.Share.Data;
using Microsoft.EntityFrameworkCore;

namespace CodingChallenge.DriverManagement.Data;

public class ApplicationDbContext : BaseDbContext
{
    public ApplicationDbContext(
        DbContextOptions<BaseDbContext> options)
        : base(options)
    {
    }

    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Offer> Offers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.OwnsOne(r => r.Coordinate);
        });
    }
}
