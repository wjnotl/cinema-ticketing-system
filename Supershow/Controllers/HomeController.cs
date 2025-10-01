using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Supershow.Controllers;

public class HomeController : Controller
{
    private readonly DB db;
    private readonly ShowtimeService showSrv;

    public HomeController(DB db, ShowtimeService showSrv)
    {
        this.db = db;
        this.showSrv = showSrv;
    }

    public IActionResult Index()
    {
        var account = HttpContext.GetAccount();
        if (account != null && account.AccountType.Name != "Customer")
        {
            return RedirectToAction("Admin");
        }

        List<BannerVM> Banners = [new()];
        List<Movie> NowShowing = [];
        List<Movie> ComingSoon = [];
        List<Movie> BookEarly = [];

        foreach (var movie in db.Movies.Where(m => !m.IsDeleted && m.Status != "Inactive").ToList())
        {
            bool haveShowtime = showSrv.MovieHasActiveShowtime(movie.Id);
            if (movie.Banner != null)
            {
                Banners.Add(new()
                {
                    Movie = movie,
                    HaveShowtime = haveShowtime
                });
            }

            if (movie.ReleaseDate > DateTime.Now)
            {
                if (haveShowtime)
                {
                    BookEarly.Add(movie);
                }
                else
                {
                    ComingSoon.Add(movie);
                }
            }
            else
            {
                NowShowing.Add(movie);
            }
        }

        // Shuffle
        GeneratorService.Shuffle(Banners);
        GeneratorService.Shuffle(NowShowing);
        GeneratorService.Shuffle(BookEarly);
        GeneratorService.Shuffle(ComingSoon);

        var vm = new HomePageVM
        {
            Banners = Banners,
            NowShowing = NowShowing,
            BookEarly = BookEarly,
            ComingSoon = ComingSoon
        };

        return View(vm);
    }

    [Authorize(Policy = "Admin Home")]
    public IActionResult Admin()
    {
        DateTime startOfThisMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        ViewBag.Past8Months = new List<string>();
        ViewBag.NewCustomers = new List<int>();
        ViewBag.NewBookings = new List<int>();
        ViewBag.NewOrders = new List<int>();

        for (int i = 7; i >= 0; i--) // 7 months ago → this month
        {
            DateTime startMonth = startOfThisMonth.AddMonths(-i);
            DateTime nextMonth = startMonth.AddMonths(1);

            ViewBag.Past8Months.Add(startMonth.ToString("MMM yyyy"));

            ViewBag.NewCustomers.Add(db.Accounts.Count(a =>
                a.AccountType.Name == "Customer" &&
                a.CreatedAt >= startMonth &&
                a.CreatedAt < nextMonth &&
                !a.IsDeleted
            ));

            ViewBag.NewBookings.Add(db.Bookings.Count(b =>
                b.CreatedAt >= startMonth &&
                b.CreatedAt < nextMonth &&
                b.Status == "Completed"
            ));

            ViewBag.NewOrders.Add(db.FnbOrders.Count(o =>
                o.CreatedAt >= startMonth &&
                o.CreatedAt < nextMonth &&
                o.Status == "Completed"
            ));
        }

        ViewBag.AvailableDevicesType = new List<string> { "computer", "phone", "tablet" };
        ViewBag.AvailableDevices = new List<string> { "Computer", "Phone", "Tablet" };
        ViewBag.DevicesCount = new List<int>();
        foreach (string device in ViewBag.AvailableDevicesType)
        {
            ViewBag.DevicesCount.Add(db.Devices.Count(d => d.DeviceType == device));
        }

        ViewBag.AvailableStatuses = new List<string> { "Pending", "Unpaid", "Confirmed", "Completed" };
        ViewBag.BookingStatuses = new List<int>();
        ViewBag.OrderStatuses = new List<int>();
        foreach (string status in ViewBag.AvailableStatuses)
        {
            ViewBag.BookingStatuses.Add(db.Bookings.Count(b => b.Status == status));
            ViewBag.OrderStatuses.Add(db.FnbOrders.Count(o => o.Status == status));
        }

        return View();
    }
}
