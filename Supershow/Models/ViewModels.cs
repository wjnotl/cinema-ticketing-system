using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace Supershow.Models;

#nullable disable warnings

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }

    [StringLength(100)]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}

public class RegisterVM
{
    [StringLength(50, ErrorMessage = "Name must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(100, ErrorMessage = "Email must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class ForgotPasswordVM
{
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailLogin", "Auth", ErrorMessage = "{0} is not registered.")]
    public string Email { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    public string ConfirmPassword { get; set; }
}

public class ChangeEmailVM
{
    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }

    [StringLength(100)]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Compare("Email", ErrorMessage = "Emails do not match.")]
    [DisplayName("Confirm Email")]
    public string ConfirmEmail { get; set; }
}

public class AccountProfileVM
{
    [StringLength(50, ErrorMessage = "Name must not exceed {0} characters.")]
    public string Name { get; set; }
    public string? Email { get; set; }
    public bool RemoveImage { get; set; }
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile? Image { get; set; }
}

public class ChangePasswordVM
{
    [StringLength(100)]
    [DataType(DataType.Password)]
    [DisplayName("Current Password")]
    public string CurrentPassword { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [RegularExpression(
        @"(?=.{6,})(?=.*\d)(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+",
        ErrorMessage = "Password must have at least 6 characters, one number, one uppercase letter, one lowercase letter and one special character."
    )]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }

    [StringLength(100, ErrorMessage = "Password must not exceed {0} characters.")]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [DisplayName("Confirm New Password")]
    public string ConfirmPassword { get; set; }
}

public class MovieListingVM
{
    public string Category { get; set; }
    public List<int> Genres { get; set; } = [];
    public List<int> Languages { get; set; } = [];
    public List<int> Classifications { get; set; } = [];
    public List<Genre> AvailableGenres { get; set; } = [];
    public List<Language> AvailableLanguages { get; set; } = [];
    public List<Classification> AvailableClassifications { get; set; } = [];
    public List<Movie> Results { get; set; } = [];
}

public class ShowtimeFilter
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class MovieShowtimeVM
{
    public Movie Movie { get; set; }
    public int? CinemaId { get; set; }
    public Cinema? Cinema { get; set; }
    public DateTime? Date { get; set; }
    public List<int> Times { get; set; } = [];
    public List<int> Experiences { get; set; } = [];
    public List<ShowtimeFilter> AvailableTimeFilters { get; set; } = [];
    public List<Experience> AvailableExperiences { get; set; } = [];
    public List<DateTime> AvailableDateFilters { get; set; } = [];
    public Dictionary<State, List<Cinema>> AvailableCinemas { get; set; } = [];
    public Dictionary<Experience, List<Showtime>> Results { get; set; } = [];
}

public class CinemaShowtimeVM
{
    public Cinema Cinema { get; set; }
    public DateTime? Date { get; set; }
    public List<int> Genres { get; set; } = [];
    public List<int> Languages { get; set; } = [];
    public List<int> Classifications { get; set; } = [];
    public List<int> Times { get; set; } = [];
    public List<int> Experiences { get; set; } = [];
    public List<Genre> AvailableGenres { get; set; } = [];
    public List<Language> AvailableLanguages { get; set; } = [];
    public List<Classification> AvailableClassifications { get; set; } = [];
    public List<ShowtimeFilter> AvailableTimeFilters { get; set; } = [];
    public List<Experience> AvailableExperiences { get; set; } = [];
    public List<DateTime> AvailableDateFilters { get; set; } = [];
    public Dictionary<State, List<Cinema>> AvailableCinemas { get; set; } = [];
    public Dictionary<Movie, Dictionary<Experience, List<Showtime>>> Results { get; set; } = [];
}

public class ExperienceShowtimeVM
{
    public Experience Experience { get; set; }
    public int? CinemaId { get; set; }
    public Cinema? Cinema { get; set; }
    public DateTime? Date { get; set; }
    public List<int> Genres { get; set; } = [];
    public List<int> Languages { get; set; } = [];
    public List<int> Classifications { get; set; } = [];
    public List<int> Times { get; set; } = [];
    public List<Genre> AvailableGenres { get; set; } = [];
    public List<Language> AvailableLanguages { get; set; } = [];
    public List<Classification> AvailableClassifications { get; set; } = [];
    public List<ShowtimeFilter> AvailableTimeFilters { get; set; } = [];
    public List<Experience> AvailableExperiences { get; set; } = [];
    public List<DateTime> AvailableDateFilters { get; set; } = [];
    public Dictionary<State, List<Cinema>> AvailableCinemas { get; set; } = [];
    public Dictionary<Movie, Dictionary<Experience, List<Showtime>>> Results { get; set; } = [];
}

public class PaymentVM
{
    [DisplayName("Payment Option")]
    [Remote("CheckInsufficientBalance", "Payment", AdditionalFields = "Amount", ErrorMessage = "Insufficient balance.")]
    public string Option { get; set; }
    public decimal Amount { get; set; }
}

public class ReloadVM
{
    [Range(2.00, 1000.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    public decimal Amount { get; set; }
}

public class CinemaInfoVM
{
    public Cinema Cinema { get; set; }
    public List<Experience> Experiences { get; set; } = [];
    public int TotalHalls { get; set; }
    public int TotalSeats { get; set; }
}

public class HomePageVM
{
    public List<BannerVM> Banners { get; set; } = [];
    public List<Movie> NowShowing { get; set; } = [];
    public List<Movie> BookEarly { get; set; } = [];
    public List<Movie> ComingSoon { get; set; } = [];
}

public class BannerVM
{
    public Movie? Movie { get; set; }
    public bool HaveShowtime { get; set; }
}

public class MovieInfoVM
{
    public Movie Movie { get; set; }
    public int TotalReviews { get; set; }
    [Precision(2, 1)]
    public decimal AverageRating { get; set; }
    public int TotalTicketsSold { get; set; }
    public string FilterRating { get; set; } = "all";
    public List<Review> Reviews { get; set; } = [];
    public ReviewInputVM NewReview { get; set; } = new();
}

public class ReviewInputVM
{
    public int Id { get; set; }
    [Range(1, 5, ErrorMessage = "{0} must be between {1} and {2}")]
    public int Rating { get; set; }
    [StringLength(100, ErrorMessage = "{0} must not exceed {1} characters.")]
    public string Comment { get; set; }
}

public class FnbMenuVM
{
    public FnbOrder FnbOrder { get; set; }
    public int? CategoryId { get; set; }
    public FnbCategory? Category { get; set; }
    public List<FnbCategory> AvailableCategories { get; set; } = [];
    public List<FnbItem> Results { get; set; } = [];
}

public class WalletVM
{
    public string? Option { get; set; }
    public Dictionary<string, string> Options { get; set; } = [];
    public decimal Balance { get; set; }
    public List<WalletTransaction> Results { get; set; } = [];
}

public class HistoryVM
{
    public string? Option { get; set; }
    public Dictionary<string, string> Options { get; set; } = [];
    public List<object> Results { get; set; } = [];
}

public class ManageCustomerVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Account> Results { get; set; }
}

public class ManageAdminVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> AdminTypes { get; set; } = [];
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableAdminTypes { get; set; } = [];
    public IPagedList<Account> Results { get; set; }
}

public class ManageMovieVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Release Date From")]
    [DataType(DataType.Date)]
    public DateTime? ReleaseFrom { get; set; }
    [DisplayName("Release Date To")]
    [DataType(DataType.Date)]
    public DateTime? ReleaseTo { get; set; }
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Movie> Results { get; set; }
}

public class ManageFnbItemVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Categories { get; set; } = [];
    [DisplayName("Min Variants Count")]
    public int? MinVariantsCount { get; set; }
    [DisplayName("Max Variants Count")]
    public int? MaxVariantsCount { get; set; }
    [DisplayName("Creation Date From")]
    [DataType(DataType.Date)]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Creation Date To")]
    [DataType(DataType.Date)]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableCategories { get; set; } = [];
    public IPagedList<FnbItem> Results { get; set; }
}

public class ManageCinemaVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> States { get; set; } = [];
    [DisplayName("Min Halls Count")]
    public int? MinHallsCount { get; set; }
    [DisplayName("Max Halls Count")]
    public int? MaxHallsCount { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStates { get; set; } = [];
    public IPagedList<Cinema> Results { get; set; }
}

public class ManageBookingVM
{
    public int? CinemaId { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Min Seats Count")]
    public int? MinSeatsCount { get; set; }
    [DisplayName("Max Seats Count")]
    public int? MaxSeatsCount { get; set; }
    [DisplayName("Created At From")]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Created At To")]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<Booking> Results { get; set; }
}

public class ManageFnbOrderVM
{
    public int? CinemaId { get; set; }
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Statuses { get; set; } = [];
    [DisplayName("Min Items Count")]
    public int? MinItemsCount { get; set; }
    [DisplayName("Max Items Count")]
    public int? MaxItemsCount { get; set; }
    [DisplayName("Created At From")]
    public DateTime? CreationFrom { get; set; }
    [DisplayName("Created At To")]
    public DateTime? CreationTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableStatuses { get; set; } = [];
    public IPagedList<FnbOrder> Results { get; set; }
}

public class ManageExperienceVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Price")]
    public decimal? MinPrice { get; set; }
    [DisplayName("Max Price")]
    public decimal? MaxPrice { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<Experience> Results { get; set; }
}

public class ManageSeatTypeVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Price")]
    public decimal? MinPrice { get; set; }
    [DisplayName("Max Price")]
    public decimal? MaxPrice { get; set; }
    [DisplayName("Min Weekend Price")]
    public decimal? MinWeekendPrice { get; set; }
    [DisplayName("Max Weekend Price")]
    public decimal? MaxWeekendPrice { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<SeatType> Results { get; set; }
}

public class ManageShowtimeVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int? CinemaId { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Start Time From")]
    public DateTime? StartTimeFrom { get; set; }
    [DisplayName("Start Time To")]
    public DateTime? StartTimeTo { get; set; }
    [DisplayName("End Time From")]
    public DateTime? EndTimeFrom { get; set; }
    [DisplayName("End Time To")]
    public DateTime? EndTimeTo { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<Showtime> Results { get; set; }
}

public class ManageHallVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int? CinemaId { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    public List<string> Experiences { get; set; } = [];
    [DisplayName("Min Rows Count")]
    public int? MinRowsCount { get; set; }
    [DisplayName("Max Rows Count")]
    public int? MaxRowsCount { get; set; }
    [DisplayName("Min Columns Count")]
    public int? MinColumnsCount { get; set; }
    [DisplayName("Max Columns Count")]
    public int? MaxColumnsCount { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public List<string> AvailableExperiences { get; set; } = [];
    public IPagedList<Hall> Results { get; set; }
}

public class ManageFnbVariantVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int? ItemId { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Price")]
    public decimal? MinPrice { get; set; }
    [DisplayName("Max Price")]
    public decimal? MaxPrice { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<FnbItemVariant> Results { get; set; }
}

public class ManageFnbInventoryVM
{
    public string? Dir { get; set; }
    public string? Sort { get; set; }
    public int? CinemaId { get; set; }
    public int Page { get; set; } = 1;
    public string? Search { get; set; }
    public string? SearchOption { get; set; }
    [DisplayName("Min Stock Count")]
    public int? MinStockCount { get; set; }
    [DisplayName("Max Stock Count")]
    public int? MaxStockCount { get; set; }
    public List<SelectListItem> AvailableSearchOptions { get; set; } = [];
    public IPagedList<FnbInventory> Results { get; set; }
}

public class EditAdminVM
{
    public int Id { get; set; }
    public Account? Account { get; set; }
    [DisplayName("Admin Type")]
    public int AdminType { get; set; }
    [DisplayName("Cinema Branch")]
    public int? CinemaBranch { get; set; }
    public List<SelectListItem> AvailableAdminTypes { get; set; } = [];
    public List<int> AvailableHQAdminTypes { get; set; } = [];
    public List<SelectListItem> AvailableCinemas { get; set; } = [];
}

public class AddAdminVM
{
    [StringLength(50, ErrorMessage = "Name must not exceed {0} characters.")]
    public string Name { get; set; }

    [StringLength(100, ErrorMessage = "Email must not exceed {0} characters.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Remote("CheckEmailRegister", "Auth", ErrorMessage = "{0} already registered.")]
    public string Email { get; set; }
    [DisplayName("Admin Type")]
    public int AdminType { get; set; }
    [DisplayName("Cinema Branch")]
    public int? CinemaBranch { get; set; }
    public List<SelectListItem> AvailableAdminTypes { get; set; } = [];
    public List<int> AvailableHQAdminTypes { get; set; } = [];
    public List<SelectListItem> AvailableCinemas { get; set; } = [];
}

public class EditCinemaVM
{
    public int Id { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(50)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUniqueEdit", "Cinema", AdditionalFields = "Id", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(200)]
    public string Address { get; set; }
    [RegularExpression(@"\d+(\.\d{1,6})?", ErrorMessage = "{0} must be a number with no more than 6 decimal places.")]
    public decimal Latitude { get; set; }
    [RegularExpression(@"\d+(\.\d{1,6})?", ErrorMessage = "{0} must be a number with no more than 6 decimal places.")]
    public decimal Longitude { get; set; }
    [StringLength(100)]
    [DisplayName("Operating Hours")]
    public string OperatingHours { get; set; }
    [StringLength(50)]
    [DisplayName("Contact Number")]
    public string ContactNumber { get; set; }
    public int State { get; set; }
    public List<SelectListItem> AvailableStates { get; set; } = [];
}

public class AddCinemaVM
{
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(50)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Cinema", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [StringLength(200)]
    public string Address { get; set; }
    [RegularExpression(@"\d+(\.\d{1,6})?", ErrorMessage = "{0} must be a number with no more than 6 decimal places.")]
    public decimal Latitude { get; set; }
    [RegularExpression(@"\d+(\.\d{1,6})?", ErrorMessage = "{0} must be a number with no more than 6 decimal places.")]
    public decimal Longitude { get; set; }
    [StringLength(100)]
    [DisplayName("Operating Hours")]
    public string OperatingHours { get; set; }
    [StringLength(50)]
    [DisplayName("Contact Number")]
    public string ContactNumber { get; set; }
    public int State { get; set; }
    public List<SelectListItem> AvailableStates { get; set; } = [];
}

public class EditHallVM
{
    public int CinemaId { get; set; }
    public int Id { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    public int Experience { get; set; }
    public List<SelectListItem> AvailableExperiences { get; set; } = [];
}

public class AddHallVM
{
    public int CinemaId { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    public int Experience { get; set; }
    public List<SelectListItem> AvailableExperiences { get; set; } = [];
}

public class EditMovieVM
{
    public bool HasActiveShowtimes { get; set; }
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    [StringLength(50)]
    public string Title { get; set; }
    [StringLength(50)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUniqueEdit", "Movie", AdditionalFields = "Id", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [DisplayName("Release Date")]
    [DataType(DataType.Date)]
    public DateTime ReleaseDate { get; set; }
    [Range(15, 360, ErrorMessage = "{0} must be between {1} and {2} minutes.")]
    public int Duration { get; set; }
    [StringLength(50)]
    public string Director { get; set; }
    [StringLength(1000)]
    public string Cast { get; set; }
    [StringLength(1000)]
    public string Synopsis { get; set; }
    [StringLength(255)]
    [RegularExpression(@"^(https?:\/\/)?([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,6}(:\d{1,5})?(\/[\w\-._~:/?#[\]@!$&'()*+,;=%]*)?$", ErrorMessage = "{0} must be a valid URL.")]
    public string? Trailer { get; set; }
    [Range(10.00, 300.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places.")]
    public decimal Price { get; set; }
    [StringLength(30)]
    public string Status { get; set; }
    [DisplayName("Spoken Language")]
    public int SpokenLanguage { get; set; }
    public int Classification { get; set; }
    public List<int> Subtitles { get; set; } = [];
    public List<int> Genres { get; set; } = [];
    [DisplayName("Poster Image Scale")]
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double PosterImageScale { get; set; }
    public double PosterImageX { get; set; }
    public double PosterImageY { get; set; }
    public double PosterPreviewWidth { get; set; }
    public double PosterPreviewHeight { get; set; }
    [DisplayName("Poster Image")]
    public IFormFile? PosterImage { get; set; }
    public bool RemoveBannerImage { get; set; }
    [DisplayName("Banner Image Scale")]
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double BannerImageScale { get; set; }
    public double BannerImageX { get; set; }
    public double BannerImageY { get; set; }
    public double BannerPreviewWidth { get; set; }
    public double BannerPreviewHeight { get; set; }
    [DisplayName("Banner Image")]
    public IFormFile? BannerImage { get; set; }
    public List<SelectListItem> AvailableStatuses { get; set; } = [];
    public List<SelectListItem> AvailableSpokenLanguages { get; set; } = [];
    public List<SelectListItem> AvailableClassifications { get; set; } = [];
    public List<Subtitle> AvailableSubtitles { get; set; } = [];
    public List<Genre> AvailableGenres { get; set; } = [];
}

public class AddMovieVM
{
    [StringLength(50)]
    public string Title { get; set; }
    [StringLength(50)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "{0} can only contain lowercase letters, numbers, and hyphens.")]
    [Remote("IsSlugUnique", "Movie", ErrorMessage = "{0} has been taken.")]
    public string Slug { get; set; }
    [DisplayName("Release Date")]
    [DataType(DataType.Date)]
    public DateTime ReleaseDate { get; set; }
    [Range(15, 360, ErrorMessage = "{0} must be between {1} and {2} minutes.")]
    public int Duration { get; set; }
    [StringLength(50)]
    public string Director { get; set; }
    [StringLength(1000)]
    public string Cast { get; set; }
    [StringLength(1000)]
    public string Synopsis { get; set; }
    [StringLength(255)]
    [RegularExpression(@"^(https?:\/\/)?([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,6}(:\d{1,5})?(\/[\w\-._~:/?#[\]@!$&'()*+,;=%]*)?$", ErrorMessage = "{0} must be a valid URL.")]
    public string? Trailer { get; set; }
    [Range(10.00, 300.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places.")]
    public decimal Price { get; set; }
    [StringLength(30)]
    public string Status { get; set; }
    [DisplayName("Spoken Language")]
    public int SpokenLanguage { get; set; }
    public int Classification { get; set; }
    public List<int> Subtitles { get; set; } = [];
    public List<int> Genres { get; set; } = [];
    [DisplayName("Poster Image Scale")]
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double PosterImageScale { get; set; }
    public double PosterImageX { get; set; }
    public double PosterImageY { get; set; }
    public double PosterPreviewWidth { get; set; }
    public double PosterPreviewHeight { get; set; }
    [DisplayName("Poster Image")]
    public IFormFile PosterImage { get; set; }
    [DisplayName("Banner Image Scale")]
    [Range(0.1, 2.0, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    public double BannerImageScale { get; set; }
    public double BannerImageX { get; set; }
    public double BannerImageY { get; set; }
    public double BannerPreviewWidth { get; set; }
    public double BannerPreviewHeight { get; set; }
    [DisplayName("Banner Image")]
    public IFormFile? BannerImage { get; set; }
    public List<SelectListItem> AvailableStatuses { get; set; } = [];
    public List<SelectListItem> AvailableSpokenLanguages { get; set; } = [];
    public List<SelectListItem> AvailableClassifications { get; set; } = [];
    public List<Subtitle> AvailableSubtitles { get; set; } = [];
    public List<Genre> AvailableGenres { get; set; } = [];
}

public class EditShowtimeVM
{
    public int CinemaId { get; set; }
    public bool HasBookings { get; set; }
    public int Id { get; set; }
    [DisplayName("Movie")]
    public int MovieId { get; set; }
    public string MovieTitle { get; set; }
    public int? MovieDuration { get; set; }
    [DisplayName("Hall")]
    public int HallId { get; set; }
    public string HallName { get; set; }
    [DisplayName("Start Time")]
    [Remote("IsAvailable", "Showtime", AdditionalFields = "Id, MovieId, HallId", ErrorMessage = "This time slot is not available.")]
    public DateTime StartTime { get; set; }
    public List<SelectListItem> AvailableMovies { get; set; } = [];
    public List<SelectListItem> AvailableHalls { get; set; } = [];
}

public class AddShowtimeVM
{
    public int CinemaId { get; set; }
    [DisplayName("Movie")]
    public int MovieId { get; set; }
    [DisplayName("Hall")]
    public int HallId { get; set; }
    [DisplayName("Start Time")]
    public DateTime StartTime { get; set; }
    public List<SelectListItem> AvailableMovies { get; set; } = [];
    public List<SelectListItem> AvailableHalls { get; set; } = [];
}

public class EditFnbItemVM
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    public int FnbCategory { get; set; }
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile? Image { get; set; }
    public List<SelectListItem> AvailableCategories { get; set; } = [];
}

public class AddFnbItemVM
{
    [StringLength(50)]
    public string Name { get; set; }
    public int FnbCategory { get; set; }
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile Image { get; set; }
    public List<SelectListItem> AvailableCategories { get; set; } = [];
}

public class EditFnbVariantVM
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(100)]
    public string? Description { get; set; }
    [Range(2.00, 200.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    public decimal Price { get; set; }
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile? Image { get; set; }
}

public class AddFnbVariantVM
{
    public int ItemId { get; set; }
    [StringLength(50)]
    public string Name { get; set; }
    [StringLength(100)]
    public string? Description { get; set; }
    [Range(2.00, 200.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    public decimal Price { get; set; }
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    public IFormFile Image { get; set; }
}

public class EditFnbInventoryVM
{
    public int CinemaId { get; set; }
    [DisplayName("F&B Item Variant")]
    public int VariantId { get; set; }
    [DisplayName("Stock Count")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock count cannot be negative.")]
    public int StockCount { get; set; }
}

public class AddFnbInventoryVM
{
    public int CinemaId { get; set; }
    [DisplayName("F&B Item Variant")]
    public int VariantId { get; set; }
    [DisplayName("Stock Count")]
    [Range(0, int.MaxValue, ErrorMessage = "Stock count cannot be negative.")]
    public int StockCount { get; set; }
    public List<SelectListItem> AvailableVariants { get; set; } = [];
}

public class EditExperienceVM
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Slug { get; set; }
    [StringLength(100)]
    [DisplayName("Tag Line")]
    public string TagLine { get; set; }
    [StringLength(500)]
    public string Description { get; set; }
    [StringLength(300)]
    public string Includes { get; set; }
    [Range(0.00, 50.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    public decimal Price { get; set; }
    public double ImageScale { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double PreviewWidth { get; set; }
    public double PreviewHeight { get; set; }
    [DisplayName("Banner Image")]
    public IFormFile? BannerImage { get; set; }
}

public class EditSeatTypeVM
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int ColumnSpan { get; set; }
    [Range(0.00, 50.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    public decimal Price { get; set; }
    [Range(0.00, 50.00, ErrorMessage = "{0} must be between {1:F2} and {2:F2}")]
    [RegularExpression(@"\d+(\.\d{1,2})?", ErrorMessage = "{0} must be a number with no more than 2 decimal places")]
    [DisplayName("Weekend Price")]
    public decimal WeekendPrice { get; set; }
}

public class EditHallLayoutVM
{
    public Hall Hall { get; set; }
    public int? AddSeatTypeId { get; set; }
    public int? AddSeatRow { get; set; }
    public int? AddSeatColumn { get; set; }
    public int? DeleteSeatId { get; set; }
    public int? AddRow { get; set; }
    public int? AddColumn { get; set; }
    public int? DeleteRow { get; set; }
    public int? DeleteColumn { get; set; }
    public List<SeatType> AvailableSeatTypes { get; set; } = [];
}

public class SalesReportVM
{
    public List<string> Years { get; set; } = [];
    public List<string> Quarters { get; set; } = [];
    public Dictionary<string, List<decimal?>> SalesReportAnnually { get; set; } = [];
    public Dictionary<string, List<decimal?>> SalesReportQuarterly { get; set; } = [];
    public Dictionary<string, List<decimal>> SalesMovieGenreAnnually { get; set; } = [];
    public Dictionary<string, List<decimal>> SalesMovieGenreQuarterly { get; set; } = [];
    public Dictionary<string, List<decimal>> SalesFnbCategoryAnnually { get; set; } = [];
    public Dictionary<string, List<decimal>> SalesFnbCategoryQuarterly { get; set; } = [];
}