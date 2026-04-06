using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;

namespace Dash_DayTrip_API.Data
{
    public class ApiContext : DbContext
    {
        public DbSet<Form> Forms { get; set; } = null!;
        public DbSet<FormSettings> FormSettings { get; set; } = null!;
        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderPackage> OrderPackages { get; set; } = null!;
        public DbSet<OrderPayment> OrderPayments { get; set; } = null!;
        public DbSet<Booking> Bookings { get; set; } = null!;
        public DbSet<BookingGuest> BookingGuests { get; set; } = null!;
        public DbSet<BookingPayment> BookingPayments { get; set; } = null!;
        public DbSet<Promotion> Promotions { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;

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

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.Source, o.PaymentStatus, o.IsDeleted });

            modelBuilder.Entity<Order>()
                .Property(o => o.Source)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentStatus)
                .HasMaxLength(20)
                .IsRequired();

            // OrderPackage configuration
            modelBuilder.Entity<OrderPackage>()
                .HasOne(op => op.Order)
                .WithMany(o => o.OrderPackages)
                .HasForeignKey(op => op.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderPayment configuration
            modelBuilder.Entity<OrderPayment>()
                .HasOne(op => op.Order)
                .WithMany(o => o.OrderPayments)
                .HasForeignKey(op => op.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderPayment>()
                .HasIndex(op => new { op.OrderId, op.IsVoided, op.PaymentDate });

            modelBuilder.Entity<OrderPayment>()
                .HasIndex(op => new { op.OrderId, op.CreatedAt });

            modelBuilder.Entity<OrderPayment>()
                .HasIndex(op => op.IsVoided);

            // Booking (calendar) configuration
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Order)
                .WithMany()
                .HasForeignKey(b => b.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.BookingDate, b.Status, b.IsDeleted });

            // BookingGuest configuration
            modelBuilder.Entity<BookingGuest>()
                .HasOne(bg => bg.Booking)
                .WithMany()
                .HasForeignKey(bg => bg.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            // BookingPayment configuration
            modelBuilder.Entity<BookingPayment>()
                .HasOne(bp => bp.Booking)
                .WithMany()
                .HasForeignKey(bp => bp.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingPayment>()
                .HasOne(bp => bp.Order)
                .WithMany()
                .HasForeignKey(bp => bp.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingPayment>()
                .HasIndex(bp => new { bp.BookingId, bp.IsVoided, bp.PaymentDate });

            modelBuilder.Entity<BookingPayment>()
                .HasIndex(bp => new { bp.OrderId, bp.IsVoided, bp.PaymentDate });

            modelBuilder.Entity<BookingPayment>()
                .HasIndex(bp => bp.TransactionRef);

            // Promotion configuration (one promotion record per order)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Promotion)
                .WithOne(p => p.Order)
                .HasForeignKey<Promotion>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Promotion>()
                .HasIndex(p => p.OrderId)
                .IsUnique();

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
            modelBuilder.Entity<Promotion>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        }

        private static void ConfigureDecimalPrecision(ModelBuilder modelBuilder)
        {
            // FormSettings decimals
            modelBuilder.Entity<FormSettings>()
                .Property(fs => fs.DepositAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<FormSettings>()
                .Property(fs => fs.SSTPercentage).HasColumnType("decimal(5,2)");
            modelBuilder.Entity<FormSettings>()
                .Property(fs => fs.BookingGratuityAmount).HasColumnType("decimal(10,2)");

            // Package decimals
            modelBuilder.Entity<Package>()
                .Property(p => p.Price).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Package>()
                .Property(p => p.BoatFareAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Package>()
                .Property(p => p.GratuityAmount).HasColumnType("decimal(10,2)");

            // Order decimals
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
                .Property(o => o.AmountPaid).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<Order>()
                .Property(o => o.BalanceDue).HasColumnType("decimal(10,2)");

            // OrderPackage decimals
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.UnitPrice).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.LineTotal).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.BoatFareAmount).HasColumnType("decimal(10,2)");
            modelBuilder.Entity<OrderPackage>()
                .Property(op => op.GratuityAmount).HasColumnType("decimal(10,2)");

            // OrderPayment decimals
            modelBuilder.Entity<OrderPayment>()
                .Property(op => op.Amount).HasColumnType("decimal(10,2)");

            // BookingPayment decimals
            modelBuilder.Entity<BookingPayment>()
                .Property(bp => bp.Amount).HasColumnType("decimal(10,2)");

            // Promotion decimals
            modelBuilder.Entity<Promotion>()
                .Property(p => p.DiscountValue).HasColumnType("decimal(10,2)");
        }
    }
}