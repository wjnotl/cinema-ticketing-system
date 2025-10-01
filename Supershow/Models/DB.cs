using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Models;

public class DB : DbContext
{
    public DB(DbContextOptions<DB> options) : base(options)
    {

    }

    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     // Loop through all foreign keys and force them to Restrict
    //     foreach (var fk in modelBuilder.Model
    //                         .GetEntityTypes()
    //                         .SelectMany(e => e.GetForeignKeys()))
    //     {
    //         fk.DeleteBehavior = DeleteBehavior.Restrict;
    //     }

    //     base.OnModelCreating(modelBuilder);
    // }

    // DB Set
    public DbSet<State> States { get; set; }
    public DbSet<SeatType> SeatTypes { get; set; }
    public DbSet<FnbCategory> FnbCategories { get; set; }
    public DbSet<Experience> Experiences { get; set; }
    public DbSet<Language> Languages { get; set; }
    public DbSet<Subtitle> Subtitles { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Classification> Classifications { get; set; }
    public DbSet<AccountType> AccountTypes { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Verification> Verifications { get; set; }
    public DbSet<Cinema> Cinemas { get; set; }
    public DbSet<Hall> Halls { get; set; }
    public DbSet<Seat> Seats { get; set; }
    public DbSet<Movie> Movies { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Showtime> Showtimes { get; set; }
    public DbSet<FnbItem> FnbItems { get; set; }
    public DbSet<FnbItemVariant> FnbItemVariants { get; set; }
    public DbSet<FnbInventory> FnbInventories { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<FnbOrder> FnbOrders { get; set; }
    public DbSet<FnbOrderItem> FnbOrderItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }
}

#nullable disable warnings

public class State
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Cinema> Cinemas { get; set; } = [];
}

public class SeatType
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public int ColumnSpan { get; set; }
    [Precision(4, 2)]
    public decimal Price { get; set; }
    [Precision(4, 2)]
    public decimal WeekendPrice { get; set; }

    public List<Seat> Seats { get; set; } = [];
    public List<Experience> Experiences { get; set; } = [];
}

public class FnbCategory
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<FnbItem> FnbItems { get; set; } = [];
}

public class Experience
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string Slug { get; set; }
    [MaxLength(100)]
    public string TagLine { get; set; }
    [MaxLength(500)]
    public string Description { get; set; }
    [MaxLength(300)]
    public string Includes { get; set; }
    [Precision(4, 2)]
    public decimal Price { get; set; }
    [MaxLength(50)]
    public string Banner { get; set; }

    public List<Hall> Halls { get; set; } = [];
    public List<SeatType> SeatTypes { get; set; } = [];
}

public class Language
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Movie> Movies { get; set; } = [];
}

public class Subtitle
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Movie> Movies { get; set; } = [];
}

public class Genre
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Movie> Movies { get; set; } = [];
}

public class Classification
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }

    public List<Movie> Movies { get; set; } = [];
}

public class AccountType
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public bool IsHQStaff { get; set; }

    public List<Account> Accounts { get; set; } = [];
}

public class Account
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string PasswordHash { get; set; }
    [MaxLength(50)]
    public string? Image { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DeletionAt { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; } = false;
    [Precision(15, 2)]
    public decimal WalletBalance { get; set; }
    public int? CinemaId { get; set; }
    public int AccountTypeId { get; set; }
    [NotMapped]
    public string Status
    {
        get
        {
            if (IsBanned) return "Banned";

            if (DeletionAt != null) return "ToDelete";

            if (IsDeleted) return "Deleted";

            if (LockoutEnd != null) return "Timeout";

            return "Active";
        }
    }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Cinema? Cinema { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public AccountType AccountType { get; set; }
    public List<Device> Devices { get; set; } = [];
    public List<Verification> Verifications { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
    public List<WalletTransaction> WalletTransactions { get; set; } = [];
    public List<FnbOrder> FnbOrders { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

public class Device
{
    public int Id { get; set; }
    [MaxLength(100)]
    public string Address { get; set; }
    [MaxLength(20)]
    public string DeviceOS { get; set; }
    [MaxLength(20)]
    public string DeviceType { get; set; }
    [MaxLength(20)]
    public string DeviceBrowser { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsVerified { get; set; }
    public int AccountId { get; set; }

    public Account Account { get; set; }
    public Verification? Verification { get; set; }
    public List<Session> Sessions { get; set; } = [];
}

public class Session
{
    [Key]
    [MaxLength(50)]
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int DeviceId { get; set; }

    public Device Device { get; set; }
}

public class Verification
{
    [Key]
    [MaxLength(50)]
    public string Token { get; set; }
    [MaxLength(6)]
    public string OTP { get; set; }
    public bool IsVerified { get; set; }
    [MaxLength(30)]
    public string Action { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int? AccountId { get; set; }
    public int? DeviceId { get; set; }

    public Account Account { get; set; }
    public Device Device { get; set; }
}

public class Cinema
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string Slug { get; set; }
    [MaxLength(200)]
    public string Address { get; set; }
    [Precision(10, 6)]
    public decimal Latitude { get; set; }
    [Precision(10, 6)]
    public decimal Longitude { get; set; }
    [MaxLength(100)]
    public string OperatingHours { get; set; }
    [MaxLength(50)]
    public string ContactNumber { get; set; }
    public bool IsDeleted { get; set; } = false;
    public int StateId { get; set; }

    public State State { get; set; }
    public List<Hall> Halls { get; set; } = [];
    public List<FnbInventory> FnbInventories { get; set; } = [];
    public List<FnbOrder> FnbOrders { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
}

public class Hall
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public bool IsDeleted { get; set; } = false;
    public int ExperienceId { get; set; }
    public int CinemaId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Experience Experience { get; set; }
    public Cinema Cinema { get; set; }
    public List<Seat> Seats { get; set; } = [];
    public List<Showtime> Showtimes { get; set; } = [];
}

public class Seat
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    public int Row { get; set; }
    public string Columns { get; set; } = "";
    [NotMapped]
    public List<int> ColumnsList
    {
        get
        {
            List<int> list = [];

            if (string.IsNullOrEmpty(Columns))
            {
                return list;
            }

            foreach (var column in Columns.Split(","))
            {
                list.Add(int.Parse(column));
            }

            return list;
        }
        set
        {
            if (value == null || value.Count == 0)
            {
                Columns = "";
            }
            else
            {
                Columns = string.Join(",", value);
            }
        }
    }
    public bool IsDeleted { get; set; } = false;
    public int HallId { get; set; }
    public int SeatTypeId { get; set; }

    public Hall Hall { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public SeatType SeatType { get; set; }
    public List<Ticket> Tickets { get; set; } = [];
}

public class Movie
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Title { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int Duration { get; set; }
    [MaxLength(50)]
    public string Director { get; set; }
    [MaxLength(1000)]
    public string Cast { get; set; }
    [MaxLength(1000)]
    public string Synopsis { get; set; }
    [MaxLength(255)]
    public string? Trailer { get; set; }
    [MaxLength(50)]
    public string Poster { get; set; }
    [MaxLength(50)]
    public string? Banner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [Precision(5, 2)]
    public decimal Price { get; set; }
    [MaxLength(30)]
    public string Status { get; set; }
    [MaxLength(50)]
    public string Slug { get; set; }
    public int SpokenLanguageId { get; set; }
    public int ClassificationId { get; set; }
    public bool IsDeleted { get; set; } = false;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Language SpokenLanguage { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Classification Classification { get; set; }
    public List<Subtitle> Subtitles { get; set; } = [];
    public List<Genre> Genres { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<Showtime> Showtimes { get; set; } = [];
}

[PrimaryKey("AccountId", "MovieId")]
public class Review
{
    public int AccountId { get; set; }
    public int MovieId { get; set; }
    public int Rating { get; set; }
    [MaxLength(200)]
    public string Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Account Account { get; set; }
    public Movie Movie { get; set; }
}

public class Showtime
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public int HallId { get; set; }
    public int MovieId { get; set; }
    public bool IsDeleted { get; set; } = false;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Hall Hall { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Movie Movie { get; set; }
    public List<Booking> Bookings { get; set; } = [];
}

public class FnbItem
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; } = false;
    public int FnbCategoryId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public FnbCategory FnbCategory { get; set; }
    public List<FnbItemVariant> FnbItemVariants { get; set; } = [];
}

public class FnbItemVariant
{
    public int Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
    [MaxLength(100)]
    public string? Description { get; set; }
    [MaxLength(50)]
    public string Image { get; set; }
    [Precision(5, 2)]
    public decimal Price { get; set; }
    public bool IsDeleted { get; set; } = false;
    public int FnbItemId { get; set; }

    public FnbItem FnbItem { get; set; }
    public List<FnbInventory> FnbInventories { get; set; } = [];
    public List<FnbOrderItem> FnbOrderItems { get; set; } = [];
}

[PrimaryKey("CinemaId", "FnbItemVariantId")]
public class FnbInventory
{
    public int FnbItemVariantId { get; set; }
    public int CinemaId { get; set; }
    public int Quantity { get; set; }

    public FnbItemVariant FnbItemVariant { get; set; }
    public Cinema Cinema { get; set; }
}

public class Booking
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [MaxLength(20)]
    public string Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int ShowtimeId { get; set; }
    public int AccountId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Showtime Showtime { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    public List<Ticket> Tickets { get; set; } = [];
    public Payment? Payment { get; set; }
}

public class Ticket
{
    public int Id { get; set; }
    public string BookingId { get; set; }
    public int SeatId { get; set; }
    [Precision(5, 2)]
    public decimal Price { get; set; }

    public Booking Booking { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Seat Seat { get; set; }
}

public class FnbOrder
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [MaxLength(20)]
    public string Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PickupExpiresAt { get; set; }
    public int CinemaId { get; set; }
    public int AccountId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Cinema Cinema { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Payment Payment { get; set; }
    public List<FnbOrderItem> FnbOrderItems { get; set; } = [];
}

[PrimaryKey("FnbOrderId", "FnbItemVariantId")]
public class FnbOrderItem
{
    [MaxLength(50)]
    public string FnbOrderId { get; set; }
    public int FnbItemVariantId { get; set; }
    public int Quantity { get; set; }
    [Precision(5, 2)]
    public decimal Price { get; set; }

    public FnbOrder FnbOrder { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public FnbItemVariant FnbItemVariant { get; set; }
}

public class Payment
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Precision(10, 2)]
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    [MaxLength(20)]
    public string? PaymentType { get; set; }
    [MaxLength(100)]
    public string? Details { get; set; }
    public string? BookingId { get; set; }
    public string? FnbOrderId { get; set; }
    public int AccountId { get; set; }

    public Booking? Booking { get; set; }
    public FnbOrder? FnbOrder { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    public List<WalletTransaction> WalletTransactions { get; set; } = [];
}

public class WalletTransaction
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Precision(10, 2)]
    public decimal Amount { get; set; }
    [MaxLength(100)]
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int AccountId { get; set; }
    public string? PaymentId { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Account Account { get; set; }
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public Payment? Payment { get; set; }
}