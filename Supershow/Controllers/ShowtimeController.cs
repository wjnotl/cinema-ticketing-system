using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage Showtimes")]
public class ShowtimeController : Controller
{
    private readonly DB db;
    private readonly ShowtimeService showSrv;

    public ShowtimeController(DB db, ShowtimeService showSrv)
    {
        this.db = db;
        this.showSrv = showSrv;
    }

    public IActionResult Manage(ManageShowtimeVM vm)
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

        Dictionary<string, Expression<Func<Showtime, object>>> sortOptions = new()
        {
            { "Id", s => s.Id },
            { "Hall Id", s => s.HallId },
            { "Movie Id", s => s.MovieId },
            { "Start Time", s => s.StartTime },
            { "End Time",  s => s.StartTime.AddMinutes(s.Movie.Duration + 10) },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "id", Text = "Search By Id" },
            new() { Value = "hall_id", Text = "Search By Hall Id" },
            new() { Value = "movie_id", Text = "Search By Movie Id"}
        ];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Showtimes
            .Include(s => s.Movie)
            .AsQueryable()
            .Where(s => s.Hall.CinemaId == vm.CinemaId && !s.IsDeleted);

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "id":
                    results = results.Where(s => s.Id.ToString().Contains(search));
                    break;
                case "hall_id":
                    results = results.Where(s => s.HallId.ToString().Contains(search));
                    break;
                case "movie_id":
                    results = results.Where(s => s.MovieId.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.StartTimeFrom != null && ModelState.IsValid("StartTimeFrom"))
        {
            results = results.Where(s => s.StartTime >= vm.StartTimeFrom);
        }

        if (vm.StartTimeTo != null && ModelState.IsValid("StartTimeTo"))
        {
            results = results.Where(s => s.StartTime <= vm.StartTimeTo);
        }

        if (vm.EndTimeFrom != null && ModelState.IsValid("EndTimeFrom"))
        {
            results = results.Where(s => s.StartTime.AddMinutes(s.Movie.Duration + 10) >= vm.EndTimeFrom);
        }

        if (vm.EndTimeTo != null && ModelState.IsValid("EndTimeTo"))
        {
            results = results.Where(s => s.StartTime.AddMinutes(s.Movie.Duration + 10) <= vm.EndTimeTo);
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

    public IActionResult Add(int cinemaId)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != cinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == cinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        AddShowtimeVM vm = new()
        {
            CinemaId = cinemaId,
            StartTime = DateTime.Today.AddDays(1),
            AvailableMovies = db.Movies.Where(m => m.Status != "Inactive" && !m.IsDeleted)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Id} - {m.Title} ({m.Duration} minutes)"
                })
                .ToList(),
            AvailableHalls = db.Halls
                .Where(h => h.CinemaId == cinemaId && !h.IsDeleted)
                .Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = $"{h.Id} - {h.Name}"
                })
                .ToList(),
        };
        vm.MovieId = int.Parse(vm.AvailableMovies.First().Value);
        vm.HallId = int.Parse(vm.AvailableHalls.First().Value);

        return View(vm);
    }

    [HttpPost]
    public IActionResult Add(AddShowtimeVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var availableMovies = db.Movies.Where(m => m.Status != "Inactive" && !m.IsDeleted);
        var availableHalls = db.Halls.Where(h => h.CinemaId == vm.CinemaId && !h.IsDeleted);

        vm.AvailableMovies = availableMovies
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Id} - {m.Title} ({m.Duration} minutes)"
                })
                .ToList();
        vm.AvailableHalls = availableHalls
            .Select(h => new SelectListItem
            {
                Value = h.Id.ToString(),
                Text = $"{h.Id} - {h.Name}"
            })
            .ToList();

        if (ModelState.IsValid("MovieId") && !availableMovies.Any(m => m.Id == vm.MovieId))
        {
            ModelState.AddModelError("MovieId", "Invalid movie");
        }
        if (ModelState.IsValid("HallId") && !availableHalls.Any(h => h.Id == vm.HallId))
        {
            ModelState.AddModelError("HallId", "Invalid hall");
        }

        if (ModelState.IsValid)
        {
            var newMovie = availableMovies.FirstOrDefault(m => m.Id == vm.MovieId)!;

            int gap = 10;

            var newStart = vm.StartTime.AddMinutes(-gap);
            var newEnd = vm.StartTime.AddMinutes(newMovie.Duration + 10 + gap);

            if (db.Showtimes.Any(s =>
                !s.IsDeleted &&
                s.HallId == vm.HallId &&
                newStart < s.StartTime.AddMinutes(s.Movie.Duration + 10 + gap) &&
                s.StartTime.AddMinutes(-gap) < newEnd
            ))
            {
                ModelState.AddModelError("StartTime", "This time slot is not available.");
            }
        }

        if (ModelState.IsValid)
        {
            db.Showtimes.Add(new()
            {
                MovieId = vm.MovieId,
                HallId = vm.HallId,
                StartTime = vm.StartTime,
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage", new { vm.CinemaId });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var account = HttpContext.GetAccount()!;

        var showtime = db.Showtimes
            .Include(m => m.Movie)
            .Include(h => h.Hall)
            .FirstOrDefault(s => s.Id == id && !s.IsDeleted);

        if (showtime == null)
        {
            return NotFound();
        }

        if (account.CinemaId != null && account.CinemaId != showtime.Hall.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        if (showSrv.HasActiveBooking(id))
        {
            TempData["Message"] = "Cannot edit showtime with active bookings.";
            return RedirectToAction("Manage", new { showtime.Hall.CinemaId });
        }

        EditShowtimeVM vm = new()
        {
            CinemaId = showtime.Hall.CinemaId,
            Id = id,
            MovieId = showtime.MovieId,
            MovieTitle = showtime.Movie.Title,
            MovieDuration = showtime.Movie.Duration,
            HallId = showtime.HallId,
            HallName = showtime.Hall.Name,
            StartTime = showtime.StartTime,
            AvailableMovies = db.Movies.Where(m => m.Status != "Inactive" && !m.IsDeleted)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Id} - {m.Title} ({m.Duration} minutes)"
                })
                .ToList(),
            AvailableHalls = db.Halls
                .Where(h => h.CinemaId == showtime.Hall.CinemaId && !h.IsDeleted)
                .Select(h => new SelectListItem
                {
                    Value = h.Id.ToString(),
                    Text = $"{h.Id} - {h.Name}"
                })
                .ToList(),
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditShowtimeVM vm)
    {
        var account = HttpContext.GetAccount()!;

        var showtime = db.Showtimes
            .Include(m => m.Movie)
            .Include(h => h.Hall)
            .FirstOrDefault(s => s.Id == vm.Id && !s.IsDeleted);

        if (showtime == null)
        {
            return NotFound();
        }

        if (account.CinemaId != null && account.CinemaId != showtime.Hall.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        if (showSrv.HasActiveBooking(vm.Id))
        {
            TempData["Message"] = "Cannot edit showtime with active bookings.";
            return RedirectToAction("Manage", new { showtime.Hall.CinemaId });
        }

        var availableMovies = db.Movies.Where(m => m.Status != "Inactive" && !m.IsDeleted);
        var availableHalls = db.Halls.Where(h => h.CinemaId == showtime.Hall.CinemaId && !h.IsDeleted);

        vm.AvailableMovies = availableMovies
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = $"{m.Id} - {m.Title} ({m.Duration} minutes)"
                })
                .ToList();
        vm.AvailableHalls = availableHalls
            .Select(h => new SelectListItem
            {
                Value = h.Id.ToString(),
                Text = $"{h.Id} - {h.Name}"
            })
            .ToList();

        if (ModelState.IsValid("MovieId") && !availableMovies.Any(m => m.Id == vm.MovieId))
        {
            ModelState.AddModelError("MovieId", "Invalid movie");
        }
        if (ModelState.IsValid("HallId") && !availableHalls.Any(h => h.Id == vm.HallId))
        {
            ModelState.AddModelError("HallId", "Invalid hall");
        }

        if (ModelState.IsValid)
        {
            var newMovie = availableMovies.FirstOrDefault(m => m.Id == vm.MovieId)!;

            int gap = 10;

            var newStart = vm.StartTime.AddMinutes(-gap);
            var newEnd = vm.StartTime.AddMinutes(newMovie.Duration + 10 + gap);

            if (db.Showtimes
            .Any(s =>
                !s.IsDeleted &&
                s.Id != vm.Id &&
                s.HallId == vm.HallId &&
                newStart < s.StartTime.AddMinutes(s.Movie.Duration + 10 + gap) &&
                s.StartTime.AddMinutes(-gap) < newEnd
            ))
            {
                ModelState.AddModelError("StartTime", "This time slot is not available.");
            }
        }

        if (ModelState.IsValid)
        {
            showtime.MovieId = vm.MovieId;
            showtime.HallId = vm.HallId;
            showtime.StartTime = vm.StartTime;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        return View(vm);
    }

    public IActionResult GetEndTime(DateTime startTime, int movieId)
    {
        int? duration = db.Movies.FirstOrDefault(m => m.Id == movieId)?.Duration;
        return PartialView("_EndTime", ((DateTime?)startTime, duration));
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        var showtime = db.Showtimes
            .Include(m => m.Movie)
            .Include(h => h.Hall)
            .FirstOrDefault(s => s.Id == id && !s.IsDeleted);

        if (showtime == null)
        {
            return NotFound("Showtime not found");
        }

        if (account.CinemaId != null && account.CinemaId != showtime.Hall.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized("Unauthorized access");
        }

        if (showSrv.HasActiveBooking(id))
        {
            return BadRequest("Cannot delete showtime with active bookings");
        }

        bool hasBookings = db.Bookings.Any(b => b.ShowtimeId == id);
        if (hasBookings)
        {
            showtime.IsDeleted = true;
        }
        else
        {
            db.Showtimes.Remove(showtime);
        }
        db.SaveChanges();

        TempData["Message"] = "Showtime deleted successfully";
        return Ok();
    }

    // ===============REMOTE METHODS===============
    public bool IsAvailable(int id, int movieId, int hallId, DateTime startTime)
    {
        var movie = db.Movies.FirstOrDefault(m => m.Id == movieId && m.Status != "Inactive" && !m.IsDeleted);
        if (movie == null)
        {
            return false;
        }

        int gap = 10;

        var newStart = startTime.AddMinutes(-gap);
        var newEnd = startTime.AddMinutes(movie.Duration + 10 + gap);

        return !db.Showtimes
            .Any(s =>
                !s.IsDeleted &&
                s.Id != id &&
                s.HallId == hallId &&
                newStart < s.StartTime.AddMinutes(s.Movie.Duration + 10 + gap) &&
                s.StartTime.AddMinutes(-gap) < newEnd
            );
    }
}
