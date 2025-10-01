using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Supershow.Controllers;

[Authorize(Policy = "Sales Report")]
public class ReportController : Controller
{
    private readonly DB db;

    public ReportController(DB db)
    {
        this.db = db;
    }

    public IActionResult Index()
    {
        SalesReportVM vm = new();

        // Headers
        List<string> SalesReportHeaders = ["Total Revenue", "Revenue Growth", "Booking Revenue", "F&B Order Revenue", "Total Booking/F&B Order Count", "Average Spend per Booking/F&B Order"];
        foreach (string header in SalesReportHeaders)
        {
            vm.SalesReportAnnually.Add(header, []);
            vm.SalesReportQuarterly.Add(header, []);
        }

        foreach (var genre in db.Genres.ToList())
        {
            vm.SalesMovieGenreAnnually.Add(genre.Name, []);
            vm.SalesMovieGenreQuarterly.Add(genre.Name, []);
        }

        foreach (var category in db.FnbCategories.ToList())
        {
            vm.SalesFnbCategoryAnnually.Add(category.Name, []);
            vm.SalesFnbCategoryQuarterly.Add(category.Name, []);
        }

        DateTime today = DateTime.Today;
        int currentYear = today.Year;
        int currentQuarter = (today.Month - 1) / 3 + 1;
        DateTime startOfQuarter = new(today.Year, (currentQuarter - 1) * 3 + 1, 1);

        for (int i = 0; i <= 5; i++)
        {
            int year = currentYear - i;
            vm.Years.Add(year.ToString());

            // Booking revenue by year
            int bookingCount = db.Bookings.Count(b => b.CreatedAt.Year == year && b.Status == "Completed");
            decimal bookingRevenue = db.Bookings
                .Where(b => b.CreatedAt.Year == year && b.Status == "Completed")
                .SelectMany(b => b.Tickets)
                .Sum(t => t.Price);

            vm.SalesReportAnnually["Booking Revenue"].Add(bookingRevenue);

            // F&B order revenue by year
            int fnbOrderCount = db.FnbOrders.Count(o => o.CreatedAt.Year == year && o.Status == "Completed");
            decimal fnbOrderRevenue = db.FnbOrders
                .Where(o => o.CreatedAt.Year == year && o.Status == "Completed")
                .SelectMany(o => o.FnbOrderItems)
                .Sum(oi => oi.Price);
            vm.SalesReportAnnually["F&B Order Revenue"].Add(fnbOrderRevenue);

            // Total (annually)
            int totalCount = bookingCount + fnbOrderCount;
            decimal totalRevenue = bookingRevenue + fnbOrderRevenue;
            decimal averageSpend = totalCount > 0 ? totalRevenue / totalCount : 0;
            vm.SalesReportAnnually["Total Booking/F&B Order Count"].Add(totalCount);
            vm.SalesReportAnnually["Total Revenue"].Add(totalRevenue);
            vm.SalesReportAnnually["Average Spend per Booking/F&B Order"].Add(averageSpend);

            // Genre revenue by year
            foreach (var genre in db.Genres.ToList())
            {
                decimal genreRevenue = db.Bookings
                    .Where(b =>
                        b.CreatedAt.Year == year &&
                        b.Status == "Completed" &&
                        b.Showtime.Movie.Genres.Any(g => g.Id == genre.Id)
                    )
                    .SelectMany(b => b.Tickets)
                    .Sum(t => t.Price);
                vm.SalesMovieGenreAnnually[genre.Name].Add(genreRevenue);
            }

            // Category revenue by year
            foreach (var category in db.FnbCategories.ToList())
            {
                decimal categoryRevenue = db.FnbOrderItems
                    .Where(oi =>
                        oi.FnbOrder.CreatedAt.Year == year &&
                        oi.FnbItemVariant.FnbItem.FnbCategoryId == category.Id &&
                        oi.FnbOrder.Status == "Completed"
                    )
                    .Sum(oi => oi.Price);
                vm.SalesFnbCategoryAnnually[category.Name].Add(categoryRevenue);
            }

            // Quarter
            DateTime startQ = startOfQuarter.AddMonths(-3 * i);
            DateTime endQ = startQ.AddMonths(3);

            string Qheader = $"{startQ.Year} Q{(startQ.Month - 1) / 3 + 1}";
            vm.Quarters.Add(Qheader);

            // Booking revenue by quarter
            int bookingCountQ = db.Bookings.Count(b => b.CreatedAt >= startQ && b.CreatedAt < endQ && b.Status == "Completed");
            decimal bookingRevenueQ = db.Bookings
                .Where(b => b.CreatedAt >= startQ && b.CreatedAt < endQ && b.Status == "Completed")
                .SelectMany(b => b.Tickets)
                .Sum(t => t.Price);
            vm.SalesReportQuarterly["Booking Revenue"].Add(bookingRevenueQ);

            // F&B order revenue by quarter
            int fnbOrderCountQ = db.FnbOrders.Count(o => o.CreatedAt >= startQ && o.CreatedAt < endQ && o.Status == "Completed");
            decimal fnbOrderRevenueQ = db.FnbOrders
                .Where(o => o.CreatedAt >= startQ && o.CreatedAt < endQ && o.Status == "Completed")
                .SelectMany(o => o.FnbOrderItems)
                .Sum(oi => oi.Price);
            vm.SalesReportQuarterly["F&B Order Revenue"].Add(fnbOrderRevenueQ);

            // Total (quarter)
            int totalCountQ = bookingCountQ + fnbOrderCountQ;
            decimal totalRevenueQ = bookingRevenueQ + fnbOrderRevenueQ;
            decimal averageSpendQ = totalCountQ > 0 ? totalRevenueQ / totalCountQ : 0;
            vm.SalesReportQuarterly["Total Booking/F&B Order Count"].Add(totalCountQ);
            vm.SalesReportQuarterly["Total Revenue"].Add(totalRevenueQ);
            vm.SalesReportQuarterly["Average Spend per Booking/F&B Order"].Add(averageSpendQ);

            // Genre revenue by quarter
            foreach (var genre in db.Genres.ToList())
            {
                decimal genreRevenue = db.Bookings
                    .Where(b =>
                        b.CreatedAt >= startQ && b.CreatedAt < endQ &&
                        b.Status == "Completed" &&
                        b.Showtime.Movie.Genres.Any(g => g.Id == genre.Id)
                    )
                    .SelectMany(b => b.Tickets)
                    .Sum(t => t.Price);
                vm.SalesMovieGenreQuarterly[genre.Name].Add(genreRevenue);
            }

            // Category revenue by quarter
            foreach (var category in db.FnbCategories.ToList())
            {
                decimal categoryRevenue = db.FnbOrderItems
                    .Where(oi =>
                        oi.FnbOrder.CreatedAt >= startQ && oi.FnbOrder.CreatedAt < endQ &&
                        oi.FnbItemVariant.FnbItem.FnbCategoryId == category.Id &&
                        oi.FnbOrder.Status == "Completed"
                    )
                    .Sum(oi => oi.Price);
                vm.SalesFnbCategoryQuarterly[category.Name].Add(categoryRevenue);
            }
        }

        // Annual Growth
        decimal? prevRevenue = null;
        List<decimal?> growthList = [];

        for (int i = vm.Years.Count - 1; i >= 0; i--) // oldest -> newest
        {
            var currentRevenue = vm.SalesReportAnnually["Total Revenue"][i];
            decimal? growth;

            if (prevRevenue == null || prevRevenue.Value == 0)
                growth = null;
            else
                growth = (currentRevenue - prevRevenue.Value) / prevRevenue.Value * 100;

            growthList.Add(growth);
            prevRevenue = currentRevenue;
        }

        growthList.Reverse();
        vm.SalesReportAnnually["Revenue Growth"] = growthList;

        // Quarter Growth
        decimal? prevRevenueQ = null;
        List<decimal?> growthListQ = [];

        for (int i = vm.Quarters.Count - 1; i >= 0; i--) // oldest -> newest
        {
            var currentRevenue = vm.SalesReportQuarterly["Total Revenue"][i];
            decimal? growth;

            if (prevRevenueQ == null || prevRevenueQ.Value == 0)
                growth = null;
            else
                growth = (currentRevenue - prevRevenueQ.Value) / prevRevenueQ.Value * 100;

            growthListQ.Add(growth);
            prevRevenueQ = currentRevenue;
        }

        growthListQ.Reverse();
        vm.SalesReportQuarterly["Revenue Growth"] = growthListQ;

        return View(vm);
    }
}
