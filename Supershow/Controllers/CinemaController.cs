using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class CinemaController : Controller
{
    private readonly DB db;
    private readonly ShowtimeService showSrv;
    private readonly FnbOrderService fnbSrv;
    public CinemaController(DB db, ShowtimeService showSrv, FnbOrderService fnbSrv)
    {
        this.db = db;
        this.showSrv = showSrv;
        this.fnbSrv = fnbSrv;
    }

    public IActionResult Index()
    {
        var slug = db.Cinemas.Where(c => !c.IsDeleted).FirstOrDefault()?.Slug;
        if (string.IsNullOrEmpty(slug))
        {
            return NotFound();
        }

        return RedirectToAction("Showtimes", new { slug });
    }

    [Route("Cinema/Info/{slug}")]
    public IActionResult Info(string slug)
    {
        var cinema = db.Cinemas.Include(c => c.Halls).FirstOrDefault(c => c.Slug == slug && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        List<Experience> availableExperiences = [];
        foreach (var experience in db.Experiences)
        {
            if (cinema.Halls.Any(h => !h.IsDeleted && h.ExperienceId == experience.Id))
            {
                availableExperiences.Add(experience);
            }
        }

        var vm = new CinemaInfoVM
        {
            Cinema = cinema,
            TotalHalls = db.Halls.Where(h => h.CinemaId == cinema.Id && !h.IsDeleted).Count(),
            TotalSeats = db.Seats.Where(s => s.Hall.CinemaId == cinema.Id && !s.IsDeleted).Count(),
            Experiences = availableExperiences
        };

        return View(vm);
    }

    [Route("Cinema/Showtimes/{slug}")]
    public IActionResult Showtimes(string slug, CinemaShowtimeVM vm)
    {
        var cinema = db.Cinemas.FirstOrDefault(c => c.Slug == slug && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        vm.Cinema = cinema;
        vm.AvailableTimeFilters = [
            new ShowtimeFilter {
                Id = 1,
                Name = "Before 12:00 PM"
            },
            new ShowtimeFilter {
                Id = 2,
                Name = "12:00 PM - 6:00 PM"
            },
            new ShowtimeFilter {
                Id = 3,
                Name = "After 6:00 PM"
            }
        ];
        vm.AvailableGenres = db.Genres.ToList() ?? [];
        vm.AvailableLanguages = db.Languages.ToList() ?? [];
        vm.AvailableClassifications = db.Classifications.ToList() ?? [];
        vm.AvailableExperiences = db.Experiences.ToList() ?? [];
        vm.AvailableCinemas = db.Cinemas.Where(c => !c.IsDeleted)
            .GroupBy(c => c.State)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Name).ToList()
            );

        var results = db.Showtimes
            .Include(s => s.Movie)
                .ThenInclude(m => m.Genres)
            .Include(s => s.Hall)
                .ThenInclude(h => h.Experience)
            .Include(s => s.Hall)
                .ThenInclude(h => h.Seats)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.Tickets)
            .Where(s =>
                !s.IsDeleted &&
                s.Hall.Cinema.Id == cinema.Id &&
                s.StartTime.AddMinutes(-20) > DateTime.Now
            )
            .AsQueryable();

        vm.AvailableDateFilters = results.Select(s => s.StartTime.Date).Distinct().OrderBy(d => d).ToList();
        if (vm.AvailableDateFilters.Count > 0 && (vm.Date == null || !vm.AvailableDateFilters.Contains(vm.Date.Value)))
        {
            vm.Date = vm.AvailableDateFilters.First();
        }

        // Apply filters
        results = results.Where(s => s.StartTime.Date == vm.Date);
        if (vm.Times.Count > 0)
        {
            results = results.Where(s =>
                vm.Times.Contains(1) && s.StartTime.Hour < 12 ||
                vm.Times.Contains(2) && s.StartTime.Hour >= 12 && s.StartTime.Hour < 18 ||
                vm.Times.Contains(3) && s.StartTime.Hour >= 18
            );
        }
        if (vm.Experiences.Count > 0)
        {
            results = results.Where(s => vm.Experiences.Contains(s.Hall.ExperienceId));
        }
        if (vm.Genres.Count > 0)
        {
            results = results.Where(s => s.Movie.Genres.Any(g => vm.Genres.Contains(g.Id)));
        }
        if (vm.Languages.Count > 0)
        {
            results = results.Where(s => vm.Languages.Contains(s.Movie.SpokenLanguage.Id));
        }
        if (vm.Classifications.Count > 0)
        {
            results = results.Where(s => vm.Classifications.Contains(s.Movie.Classification.Id));
        }

        vm.Results = results
            .GroupBy(s => s.Movie)
            .ToDictionary(
                g => g.Key,
                g => g
                    .GroupBy(s => s.Hall.Experience)
                    .ToDictionary(
                        eg => eg.Key,
                        eg => eg.OrderBy(s => s.StartTime).ToList()
                    )
            );

        if (Request.IsAjax())
        {
            return PartialView("_Showtimes", (vm.Cinema, vm.Results));
        }

        return View(vm);
    }

    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Manage(ManageCinemaVM vm)
    {
        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null)
        {
            return RedirectToAction("Edit", new { id = account.CinemaId });
        }

        Dictionary<string, Expression<Func<Cinema, object>>> sortOptions = new()
        {
            { "Id", c => c.Id },
            { "Name", c => c.Name },
            { "Slug", c => c.Slug },
            { "State", c => c.State.Name },
            { "Halls Count", c => c.Halls.Count(h => !h.IsDeleted) },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = new()
        {
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "slug", Text = "Search By Slug" },
            new() { Value = "id", Text = "Search By Id"}
        };
        vm.AvailableStates = db.States.Select(s => s.Name).ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Cinemas
            .Include(c => c.State)
            .Include(c => c.Halls)
            .AsQueryable()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(c => c.Name.Contains(search));
                    break;
                case "slug":
                    results = results.Where(c => c.Slug.Contains(search));
                    break;
                case "id":
                    results = results.Where(c => c.Id.ToString().Contains(search));
                    break;
            }
        }

        if (vm.States.Count > 0)
        {
            results = results.Where(c => vm.States.Contains(c.State.Name));
        }

        if (vm.MinHallsCount != null && ModelState.IsValid("MinHallsCount"))
        {
            results = results.Where(c => c.Halls.Count(h => !h.IsDeleted) >= vm.MinHallsCount);
        }

        if (vm.MaxHallsCount != null && ModelState.IsValid("MaxHallsCount"))
        {
            results = results.Where(c => c.Halls.Count(h => !h.IsDeleted) <= vm.MaxHallsCount);
        }

        results = vm.Dir == "asc"
            ? results.OrderBy(sortOptions[vm.Sort])
            : results.OrderByDescending(sortOptions[vm.Sort]);

        vm.Results = results.ToPagedList(vm.Page, 10);

        ViewBag.AccountType = HttpContext.GetAccount()!.AccountType.Name;

        if (Request.IsAjax())
        {
            return PartialView("_Manage", vm);
        }

        return View(vm);
    }

    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Add()
    {
        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null)
        {
            return Unauthorized();
        }

        AddCinemaVM vm = new()
        {
            AvailableStates = db.States.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList()
        };
        vm.State = int.Parse(vm.AvailableStates.First().Value);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Add(AddCinemaVM vm)
    {
        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null)
        {
            return Unauthorized();
        }

        var availableStates = db.States;

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("State") && !availableStates.Any(s => s.Id == vm.State))
        {
            ModelState.AddModelError("State", "Invalid state.");
        }

        if (ModelState.IsValid)
        {
            db.Cinemas.Add(new()
            {
                Name = vm.Name,
                Slug = vm.Slug,
                Address = vm.Address,
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                StateId = vm.State,
                OperatingHours = vm.OperatingHours,
                ContactNumber = vm.ContactNumber,
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage");
        }

        vm.AvailableStates = availableStates.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();

        return View(vm);
    }

    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Edit(int id)
    {
        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null && account.CinemaId != id)
        {
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == id && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        EditCinemaVM vm = new()
        {
            Id = id,
            Name = cinema.Name,
            Slug = cinema.Slug,
            Address = cinema.Address,
            Latitude = cinema.Latitude,
            Longitude = cinema.Longitude,
            State = cinema.StateId,
            OperatingHours = cinema.OperatingHours,
            ContactNumber = cinema.ContactNumber,
            AvailableStates = db.States.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Edit(EditCinemaVM vm)
    {
        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null && account.CinemaId != vm.Id)
        {
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.Id && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        var AvailableStates = db.States;

        if (ModelState.IsValid("Slug") && !IsSlugUniqueEdit(vm.Slug, vm.Id))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("State") && !AvailableStates.Any(s => s.Id == vm.State))
        {
            ModelState.AddModelError("State", "Invalid state.");
        }

        if (ModelState.IsValid)
        {
            cinema.Name = vm.Name;
            cinema.Slug = vm.Slug;
            cinema.Address = vm.Address;
            cinema.Latitude = vm.Latitude;
            cinema.Longitude = vm.Longitude;
            cinema.StateId = vm.State;
            cinema.OperatingHours = vm.OperatingHours;
            cinema.ContactNumber = vm.ContactNumber;
            db.SaveChanges();

            TempData["Message"] = $"Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Cinemas")]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;
        if (account.CinemaId != null)
        {
            return Unauthorized("Unauthorized access");
        }

        var cinema = db.Cinemas
            .Include(c => c.Accounts)
            .Include(c => c.Halls)
            .ThenInclude(h => h.Seats)
            .ThenInclude(s => s.Tickets)
            .FirstOrDefault(c => c.Id == id && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        if (cinema.Accounts.Count > 0)
        {
            return BadRequest("Cannot delete a cinema with admins assigned to it.");
        }

        if (showSrv.CinemaHasActiveShowtime(id))
        {
            return BadRequest("Cannot delete a cinema that has active showtimes.");
        }

        if (fnbSrv.CinemaHasActiveOrder(id))
        {
            return BadRequest("Cannot delete a cinema that has active orders.");
        }

        // remove inventories
        db.FnbInventories.RemoveRange(db.FnbInventories.Where(i => i.CinemaId == id));

        // remove halls
        foreach (var hall in cinema.Halls.Where(h => !h.IsDeleted))
        {
            foreach (var seat in hall.Seats.Where(s => !s.IsDeleted))
            {
                if (seat.Tickets.Count > 0)
                {
                    seat.IsDeleted = true;
                }
                else
                {
                    db.Seats.Remove(seat);
                }
            }

            bool hasTickets = hall.Seats.Any(s => s.Tickets.Count > 0);
            bool hasShowtimes = db.Showtimes.Any(s => s.HallId == hall.Id);

            if (hasTickets || hasShowtimes)
            {
                hall.IsDeleted = true;
            }
            else
            {
                db.Halls.Remove(hall);
            }
        }

        // remove cinema
        bool cinemaHasTickets = cinema.Halls.Any(h => h.Seats.Any(s => s.Tickets.Count > 0));
        bool cinemaHasShowtimes = db.Showtimes.Any(s => s.Hall.CinemaId == cinema.Id);
        bool cinemaHasFnbOrders = db.FnbOrders.Any(o => o.CinemaId == cinema.Id);

        if (cinemaHasTickets || cinemaHasShowtimes || cinemaHasFnbOrders)
        {
            cinema.IsDeleted = true;
        }
        else
        {
            db.Cinemas.Remove(cinema);
        }
        db.SaveChanges();

        TempData["Message"] = "Cinema deleted successfully!";
        return Ok();
    }

    // ======================REMOTE METHODS======================
    public bool IsSlugUnique(string slug)
    {
        return !db.Cinemas.Any(c => c.Slug == slug && !c.IsDeleted);
    }

    public bool IsSlugUniqueEdit(string slug, int id)
    {
        return !db.Cinemas.Any(c => c.Id != id && c.Slug == slug && !c.IsDeleted);
    }
}