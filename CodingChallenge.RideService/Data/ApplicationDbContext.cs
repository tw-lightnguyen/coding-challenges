using CodingChallenge.RideService.Models;
using Microsoft.EntityFrameworkCore;

namespace CodingChallenge.RideService.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Driver> Drivers { get; set; }
    public DbSet<Ride> Rides { get; set; }
    public DbSet<Passenger> Passengers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ride>(entity =>
        {
            entity.OwnsOne(r => r.Pickup);
            entity.OwnsOne(r => r.Destination);
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.OwnsOne(d => d.Coordinate);
        });
    }
}
