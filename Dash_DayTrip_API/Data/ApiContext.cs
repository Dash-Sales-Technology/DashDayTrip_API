using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;

namespace Dash_DayTrip_API.Data
{
    public class ApiContext : DbContext
    {
        public DbSet<Form> Forms { get; set; }
        public DbSet<FormSettings> FormSettings { get; set; }
        public DbSet<Package> Packages { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderPackage> OrderPackages { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingGuest> BookingGuests { get; set; } = null!;

        public ApiContext(DbContextOptions<ApiContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Form configuration
            modelBuilder.Entity<Form>()
                .HasIndex(f => f.Status);
            modelBuilder.Entity<Form>()
                .HasIndex(f => f.IsDefault);

            // FormSettings - One-to-One with Form
            modelBuilder.Entity<FormSettings>()
                .HasOne(fs => fs.Form)
                .WithOne(f => f.FormSettings)
                .HasForeignKey<FormSettings>(fs => fs.FormId);

            // Package configuration
            modelBuilder.Entity<Package>()
                .HasOne(p => p.Form)
                .WithMany(f => f.Packages)
                .HasForeignKey(p => p.FormId)
                .OnDelete(DeleteBehavior.Cascade);

            // Order configuration
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.Status);
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ReferenceNumber);

            // OrderPackage configuration
            modelBuilder.Entity<OrderPackage>()
                .HasOne(op => op.Order)
                .WithMany(o => o.OrderPackages)
                .HasForeignKey(op => op.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Booking (calendar) configuration
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Order)
                .WithMany()
                .HasForeignKey(b => b.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // BookingGuest configuration
            modelBuilder.Entity<BookingGuest>()
                .HasOne(bg => bg.Booking)
                .WithMany()
                .HasForeignKey(bg => bg.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure decimal precision for financial fields
            ConfigureDecimalPrecision(modelBuilder);

            // Global query filters to automatically skip soft-deleted rows
            modelBuilder.Entity<Order>().HasQueryFilter(o => !o.IsDeleted);
            modelBuilder.Entity<Form>().HasQueryFilter(f => !f.IsDeleted);
            modelBuilder.Entity<FormSettings>().HasQueryFilter(fs => !fs.IsDeleted);
            modelBuilder.Entity<Package>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Booking>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<OrderPackage>().HasQueryFilter(op => !op.IsDeleted);
            modelBuilder.Entity<BookingGuest>().HasQueryFilter(bg => !bg.IsDeleted);
        }

        private void ConfigureDecimalPrecision(ModelBuilder modelBuilder)
        {
            // FormSettings decimals
            modelBuilder.Entity<FormSettings>()
                .Property(fs => fs.DepositAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<FormSettings>()
                .Property(fs => fs.SSTPercentage).HasColumnType("decimal(5,2)");

            // Package decimals
            modelBuilder.Entity<Package>()
                .Property(p => p.Price).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Package>()
                .Property(p => p.BoatFareAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Package>()
                .Property(p => p.GratuityAmount).HasColumnType("decimal(10,2)");

            // Order decimals (was Booking)
            modelBuilder.Entity<Order>()
                .Property(o => o.Subtotal).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalBoatFare).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.TotalGratuity).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.GrandTotal).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.DepositPaid).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.BalanceDue).HasColumnType("decimal(10,2)");

            // OrderPackage decimals (was BookingPackage)
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.UnitPrice).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.LineTotal).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.BoatFareAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.GratuityAmount).HasColumnType("decimal(10,2)");
        }
    }
}