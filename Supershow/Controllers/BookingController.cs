using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class BookingController : Controller
{
    private readonly DB db;
    private readonly BookingService bookSrv;

    public BookingController(DB db, BookingService bookSrv)
    {
        this.db = db;
        this.bookSrv = bookSrv;
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Create(int id)
    {
        var booking = db.Bookings.FirstOrDefault(b => b.AccountId == HttpContext.GetAccount()!.Id && (b.Status == "Pending" || b.Status == "Unpaid"));
        if (booking != null)
        {
            TempData["Message"] = "Finish or cancel your current booking first";
            return RedirectToAction("History", "Account");
        }

        var showtime = db.Showtimes.FirstOrDefault(s =>
            s.Id == id && !s.IsDeleted &&
            s.StartTime.AddMinutes(-20) > DateTime.Now
        );
        if (showtime == null)
        {
            return NotFound();
        }

        booking = new Booking
        {
            Status = "Pending",
            ExpiresAt = DateTime.Now.AddMinutes(5),
            ShowtimeId = id,
            AccountId = HttpContext.GetAccount()!.Id
        };
        db.Bookings.Add(booking);
        db.SaveChanges();

        return RedirectToAction("Seats", new { id = booking.Id });
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Seats(string id)
    {
        var booking = db.Bookings
            .Include(b => b.Showtime.Movie.SpokenLanguage)
            .Include(b => b.Showtime.Hall.Experience)
            .Include(b => b.Showtime.Hall.Seats)
                .ThenInclude(s => s.SeatType)
            .Include(b => b.Showtime.Hall.Cinema)
            .Include(b => b.Showtime.Bookings)
                .ThenInclude(bk => bk.Tickets)
            .FirstOrDefault(b =>
                b.Id == id &&
                b.AccountId == HttpContext.GetAccount()!.Id &&
                b.Status == "Pending" &&
                b.ExpiresAt > DateTime.Now
            );

        if (booking == null)
        {
            return NotFound();
        }

        if (Request.Method == "POST")
        {
            if (booking.Tickets.Count == 0)
            {
                TempData["Message"] = "Please select at least one seat";
                return RedirectToAction("Seats", new { id });
            }

            return RedirectToAction("Checkout", new { id });
        }

        if (Request.IsAjax())
        {
            return PartialView("_HallLayout", booking);
        }

        ViewBag.IncludeSingleSeats = booking.Showtime.Hall.Seats.Any(s => s.SeatType.ColumnSpan == 1 && !s.IsDeleted);
        ViewBag.IncludeTwinSeats = booking.Showtime.Hall.Seats.Any(s => s.SeatType.ColumnSpan == 2 && !s.IsDeleted);
        ViewBag.ExpiredTimestamp = new DateTimeOffset(booking.ExpiresAt!.Value).ToUnixTimeMilliseconds();

        return View(booking);
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Checkout(string id)
    {
        var booking = db.Bookings
            .Include(b => b.Showtime.Movie.SpokenLanguage)
            .Include(b => b.Showtime.Hall.Experience)
            .Include(b => b.Showtime.Hall.Seats)
                .ThenInclude(s => s.SeatType)
            .Include(b => b.Showtime.Hall.Cinema)
            .Include(b => b.Showtime.Bookings)
                .ThenInclude(bk => bk.Tickets)
            .FirstOrDefault(b =>
                b.Id == id &&
                b.AccountId == HttpContext.GetAccount()!.Id &&
                b.Status == "Pending" &&
                b.ExpiresAt > DateTime.Now
            );

        if (booking == null)
        {
            return NotFound();
        }

        if (booking.Tickets.Count == 0)
        {
            TempData["Message"] = "Please select at least one seat";
            return RedirectToAction("Seats", new { id });
        }

        var totalTicketPrice = booking.Tickets.Sum(t => t.Price);

        if (Request.Method == "POST")
        {
            booking.Status = "Unpaid";
            booking.ExpiresAt = null;

            var payment = new Payment
            {
                ExpiresAt = DateTime.Now.AddMinutes(7),
                Amount = totalTicketPrice,
                BookingId = booking.Id,
                AccountId = HttpContext.GetAccount()!.Id
            };
            db.Payments.Add(payment);
            db.SaveChanges();

            return RedirectToAction("Process", "Payment", new { id = payment.Id });
        }

        ViewBag.ExpiredTimestamp = new DateTimeOffset(booking.ExpiresAt!.Value).ToUnixTimeMilliseconds();
        ViewBag.TotalTicketPrice = totalTicketPrice;

        return View(booking);
    }

    [HttpPost]
    [Authorize(Policy = "Cancel Bookings")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        var booking = db.Bookings
            .Include(b => b.Showtime.Hall)
            .FirstOrDefault(b => b.Id == id);
        if (booking == null)
        {
            return BadRequest("Booking not found");
        }

        if (account.AccountType.Name == "Customer")
        {
            if (booking.AccountId != account.Id)
            {
                return Unauthorized("Unauthorized access");
            }
        }
        else if (account.CinemaId != null && account.CinemaId != booking.Showtime.Hall.CinemaId)
        {
            return Unauthorized("Unauthorized access");
        }

        if (booking.Status != "Pending" && booking.Status != "Unpaid" && booking.Status != "Confirmed")
        {
            return BadRequest("Booking cannot be cancelled");
        }

        await bookSrv.CancelBooking(booking.Id);

        TempData["Message"] = "Booking canceled successfully";
        return Ok();
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Info(string id)
    {
        var booking = db.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Seat.SeatType)
            .Include(b => b.Showtime.Movie.SpokenLanguage)
            .Include(b => b.Showtime.Movie.Classification)
            .Include(b => b.Showtime.Movie.Subtitles)
            .Include(b => b.Showtime.Movie.Genres)
            .Include(b => b.Showtime.Hall.Experience)
            .Include(b => b.Showtime.Hall.Cinema)
            .FirstOrDefault(b =>
                b.Id == id &&
                b.AccountId == HttpContext.GetAccount()!.Id &&
                b.Status != "Pending"
            );

        if (booking == null)
        {
            return NotFound();
        }

        return View(booking);
    }

    [Authorize(Policy = "Manage Bookings")]
    public IActionResult Manage(ManageBookingVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (vm.CinemaId == null)
        {
            vm.CinemaId = account.CinemaId;

            if (vm.CinemaId == null)
            {
                // Handle HQ admin
                var cinemas = db.Cinemas
                    .Where(c => !c.IsDeleted)
                    .GroupBy(c => c.State)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(c => c.Name).ToList()
                    );
                return View("_SelectCinemas", cinemas);
            }
            else
            {
                // Handle branch admin
                return RedirectToAction(null, new { vm.CinemaId });
            }
        }
        else if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        Dictionary<string, Expression<Func<Booking, object>>> sortOptions = new()
        {
            { "Id", b => b.Id },
            { "Showtime Id", b => b.ShowtimeId },
            { "Customer Id", b => b.AccountId },
            { "Status", b => b.Status },
            { "Seats Count", b => b.Tickets.Count },
            { "Created At", b => b.CreatedAt },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.Last();
            vm.Dir = "desc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "id", Text = "Search By Id" },
            new() { Value = "showtime_id", Text = "Search By Showtime Id" },
            new() { Value = "customer_id", Text = "Search By Customer Id"}
        ];
        vm.AvailableStatuses = ["Pending", "Unpaid", "Confirmed", "Completed", "Canceled"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Bookings
            .Include(b => b.Tickets)
            .Where(b => b.Showtime.Hall.CinemaId == vm.CinemaId)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "id":
                    results = results.Where(b => b.Id.ToString().Contains(search));
                    break;
                case "showtime_id":
                    results = results.Where(b => b.ShowtimeId.ToString().Contains(search));
                    break;
                case "customer_id":
                    results = results.Where(b => b.AccountId.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Statuses.Count > 0)
        {
            results = results.Where(b => vm.Statuses.Contains(b.Status));
        }

        if (vm.MinSeatsCount != null && ModelState.IsValid("MinSeatsCount"))
        {
            results = results.Where(b => b.Tickets.Count >= vm.MinSeatsCount);
        }

        if (vm.MaxSeatsCount != null && ModelState.IsValid("MaxSeatsCount"))
        {
            results = results.Where(b => b.Tickets.Count <= vm.MaxSeatsCount);
        }

        if (vm.CreationFrom != null && ModelState.IsValid("CreationFrom"))
        {
            results = results.Where(b => b.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null && ModelState.IsValid("CreationTo"))
        {
            results = results.Where(b => b.CreatedAt <= vm.CreationTo);
        }

        // Sort
        results = vm.Dir == "asc"
            ? results.OrderBy(sortOptions[vm.Sort])
            : results.OrderByDescending(sortOptions[vm.Sort]);

        vm.Results = results.ToPagedList(vm.Page, 10);

        if (Request.IsAjax())
        {
            return PartialView("_Manage", vm);
        }

        ViewBag.CinemaName = cinema.Name;

        return View(vm);
    }

    [Authorize(Policy = "Manage Bookings")]
    public IActionResult Edit(string id)
    {
        var account = HttpContext.GetAccount()!;

        var booking = db.Bookings
            .Include(b => b.Account)
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
                .ThenInclude(t => t.Seat.SeatType)
            .Include(b => b.Showtime.Movie.SpokenLanguage)
            .Include(b => b.Showtime.Movie.Classification)
            .Include(b => b.Showtime.Movie.Subtitles)
            .Include(b => b.Showtime.Movie.Genres)
            .Include(b => b.Showtime.Hall.Experience)
            .Include(b => b.Showtime.Hall.Cinema)
            .FirstOrDefault(b => b.Id == id);
        if (booking == null)
        {
            return NotFound();
        }

        if (account.CinemaId != null && account.CinemaId != booking.Showtime.Hall.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        return View(booking);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Bookings")]
    public IActionResult Complete(string id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        var booking = db.Bookings
            .Include(b => b.Showtime.Hall)
            .FirstOrDefault(b => b.Id == id);
        if (booking == null)
        {
            return NotFound("Booking not found");
        }

        if (account.CinemaId != null && account.CinemaId != booking.Showtime.Hall.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized("Unauthorized access");
        }

        if (booking.Status != "Confirmed")
        {
            return BadRequest("Booking cannot be completed");
        }

        booking.Status = "Completed";
        db.SaveChanges();

        TempData["Message"] = "Booking completed successfully";
        return Ok();
    }
}

