using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage Seat Types")]
public class SeatTypeController : Controller
{
    private readonly DB db;

    public SeatTypeController(DB db)
    {
        this.db = db;
    }

    public IActionResult Manage(ManageSeatTypeVM vm)
    {
        Dictionary<string, Expression<Func<SeatType, object>>> sortOptions = new()
        {
            { "Id", s => s.Id },
            { "Name", s => s.Name },
            { "Price", s => s.Price },
            { "Weekend Price", s => s.WeekendPrice },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "id", Text = "Search By Id" }
        ];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.SeatTypes.AsQueryable();

        
        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(s => s.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(s => s.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinPrice != null && ModelState.IsValid("MinPrice"))
        {
            results = results.Where(s => s.Price >= vm.MinPrice);
        }

        if (vm.MaxPrice != null && ModelState.IsValid("MaxPrice"))
        {
            results = results.Where(s => s.Price <= vm.MaxPrice);
        }

        if (vm.MinWeekendPrice != null && ModelState.IsValid("MinWeekendPrice"))
        {
            results = results.Where(s => s.WeekendPrice >= vm.MinWeekendPrice);
        }

        if (vm.MaxWeekendPrice != null && ModelState.IsValid("MaxWeekendPrice"))
        {
            results = results.Where(s => s.WeekendPrice <= vm.MaxWeekendPrice);
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

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var seatType = db.SeatTypes.FirstOrDefault(s => s.Id == id);
        if (seatType == null)
        {
            return NotFound();
        }

        EditSeatTypeVM vm = new()
        {
            Id = seatType.Id,
            Name = seatType.Name,
            ColumnSpan = seatType.ColumnSpan,
            Price = seatType.Price,
            WeekendPrice = seatType.WeekendPrice
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditSeatTypeVM vm)
    {
        var seatType = db.SeatTypes.FirstOrDefault(s => s.Id == vm.Id);
        if (seatType == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            seatType.Price = vm.Price;
            seatType.WeekendPrice = vm.WeekendPrice;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        return View(vm);
    }
}