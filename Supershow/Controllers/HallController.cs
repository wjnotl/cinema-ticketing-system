using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage Cinemas")]
public class HallController : Controller
{
    private readonly DB db;
    private readonly ShowtimeService showSrv;

    public HallController(DB db, ShowtimeService showSrv)
    {
        this.db = db;
        this.showSrv = showSrv;
    }

    public IActionResult Manage(ManageHallVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (vm.CinemaId == null)
        {
            return NotFound();
        }

        if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        Dictionary<string, Expression<Func<Hall, object>>> sortOptions = new()
        {
            { "Id", h => h.Id },
            { "Name", h => h.Name },
            { "Experiience", h => h.Experience.Name },
            { "Total Rows", h => h.TotalRows },
            { "Total Columns", h => h.TotalColumns },
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

        vm.AvailableExperiences = db.Experiences.Select(e => e.Name).ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Halls
            .AsQueryable()
            .Include(h => h.Experience)
            .Where(h => h.CinemaId == vm.CinemaId && !h.IsDeleted);

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(h => h.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(h => h.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Experiences.Count > 0)
        {
            results = results.Where(h => vm.Experiences.Contains(h.Experience.Name));
        }

        if (vm.MinRowsCount != null && ModelState.IsValid("MinRowsCount"))
        {
            results = results.Where(h => h.TotalRows >= vm.MinRowsCount);
        }

        if (vm.MaxRowsCount != null && ModelState.IsValid("MaxRowsCount"))
        {
            results = results.Where(h => h.TotalRows <= vm.MaxRowsCount);
        }

        if (vm.MinColumnsCount != null && ModelState.IsValid("MinColumnsCount"))
        {
            results = results.Where(h => h.TotalColumns >= vm.MinColumnsCount);
        }

        if (vm.MaxColumnsCount != null && ModelState.IsValid("MaxColumnsCount"))
        {
            results = results.Where(h => h.TotalColumns <= vm.MaxColumnsCount);
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

        AddHallVM vm = new()
        {
            AvailableExperiences = db.Experiences.Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Name }).ToList(),
        };
        vm.Experience = int.Parse(vm.AvailableExperiences.First().Value);

        ViewBag.CinemaName = cinema.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Add(AddHallVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        var AvailableExperiences = db.Experiences;

        if (ModelState.IsValid("Experience") && !AvailableExperiences.Any(e => e.Id == vm.Experience))
        {
            ModelState.AddModelError("Experience", "Invalid experience.");
        }

        if (ModelState.IsValid)
        {
            db.Halls.Add(new()
            {
                CinemaId = vm.CinemaId,
                Name = vm.Name,
                ExperienceId = vm.Experience,
                TotalRows = 5,
                TotalColumns = 5
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage", new { vm.CinemaId });
        }

        vm.AvailableExperiences = AvailableExperiences.Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Name }).ToList();

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var hall = db.Halls
            .Include(h => h.Cinema)
            .FirstOrDefault(h => h.Id == id && !h.IsDeleted);
        if (hall == null)
        {
            return NotFound();
        }

        if (showSrv.HallHasActiveShowtime(id))
        {
            TempData["Message"] = "Cannot edit hall when there are active showtimes.";
            return RedirectToAction("Manage", new { cinemaId = hall.CinemaId });
        }

        EditHallVM vm = new()
        {
            CinemaId = hall.CinemaId,
            Id = id,
            Name = hall.Name,
            Experience = hall.ExperienceId,
            AvailableExperiences = db.Experiences.Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Name }).ToList()
        };

        ViewBag.CinemaName = hall.Cinema.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditHallVM vm)
    {
        var hall = db.Halls
            .Include(h => h.Cinema)
            .FirstOrDefault(h => h.Id == vm.Id && !h.IsDeleted);
        if (hall == null)
        {
            return NotFound();
        }

        if (showSrv.HallHasActiveShowtime(hall.Id))
        {
            TempData["Message"] = "Cannot edit hall when there are active showtimes.";
            return RedirectToAction("Manage", new { cinemaId = hall.CinemaId });
        }

        var AvailableExperiences = db.Experiences;

        if (ModelState.IsValid("Experience") && !AvailableExperiences.Any(e => e.Id == vm.Experience))
        {
            ModelState.AddModelError("Experience", "Invalid experience.");
        }

        if (ModelState.IsValid)
        {
            hall.Name = vm.Name;
            hall.ExperienceId = vm.Experience;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        ViewBag.CinemaName = hall.Cinema.Name;

        return View(vm);
    }

    public IActionResult Layout(int id)
    {
        var hall = db.Halls
            .Include(h => h.Experience.SeatTypes)
            .Include(h => h.Cinema)
            .Include(h => h.Seats)
                .ThenInclude(s => s.SeatType)
            .FirstOrDefault(h => h.Id == id && !h.IsDeleted);
        if (hall == null)
        {
            return NotFound();
        }

        if (showSrv.HallHasActiveShowtime(hall.Id))
        {
            TempData["Message"] = "Cannot edit hall layout when there are active showtimes.";
            return RedirectToAction("Manage", new { cinemaId = hall.CinemaId });
        }

        EditHallLayoutVM vm = new()
        {
            Hall = hall,
            AvailableSeatTypes = hall.Experience.SeatTypes.ToList()
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Layout(int id, EditHallLayoutVM vm)
    {
        var hall = db.Halls
            .Include(h => h.Cinema)
            .Include(h => h.Seats)
                .ThenInclude(s => s.SeatType)
            .FirstOrDefault(h => h.Id == id && !h.IsDeleted);
        if (hall == null)
        {
            return NotFound("Hall not found.");
        }

        if (showSrv.HallHasActiveShowtime(hall.Id))
        {
            return BadRequest("Cannot edit layout when there are active showtimes.");
        }

        if (
            vm.AddSeatTypeId != null && ModelState.IsValid("AddSeatTypeId") &&
            vm.AddSeatRow != null && ModelState.IsValid("AddSeatRow") &&
            vm.AddSeatColumn != null && ModelState.IsValid("AddSeatColumn")
        ) // Add seat
        {
            var seatType = db.SeatTypes.FirstOrDefault(st => st.Id == vm.AddSeatTypeId);
            if (seatType == null)
            {
                return BadRequest("Seat type not found.");
            }

            var seat = hall.Seats.FirstOrDefault(s => s.Row == vm.AddSeatRow && s.ColumnsList.Contains((int)vm.AddSeatColumn) && !s.IsDeleted);
            var nextSeat = hall.Seats.FirstOrDefault(s => s.Row == vm.AddSeatRow && s.ColumnsList.Contains((int)vm.AddSeatColumn + 1) && !s.IsDeleted);

            if (seat != null)
            {
                return BadRequest("Seat already exists.");
            }

            if (seatType.ColumnSpan == 2 && nextSeat != null)
            {
                return BadRequest("Seat already exists.");
            }

            if (vm.AddSeatRow < 0 || vm.AddSeatColumn < 0 || vm.AddSeatRow >= hall.TotalRows || vm.AddSeatColumn >= hall.TotalColumns)
            {
                return BadRequest("Invalid seat position.");
            }

            if (seatType.ColumnSpan == 2 && vm.AddSeatColumn + 1 > hall.TotalColumns)
            {
                return BadRequest("Invalid seat position.");
            }

            if (seatType.ColumnSpan == 2)
            {
                hall.Seats.Add(new Seat
                {
                    HallId = id,
                    Name = $"Unknown, Unknown",
                    Row = (int)vm.AddSeatRow,
                    ColumnsList = [(int)vm.AddSeatColumn, (int)vm.AddSeatColumn + 1],
                    SeatTypeId = seatType.Id,
                    SeatType = seatType
                });
            }
            else
            {
                hall.Seats.Add(new Seat
                {
                    HallId = id,
                    Name = "Unknown",
                    Row = (int)vm.AddSeatRow,
                    ColumnsList = [(int)vm.AddSeatColumn],
                    SeatTypeId = seatType.Id,
                    SeatType = seatType
                });
            }
        }
        else if (vm.DeleteSeatId != null && ModelState.IsValid("DeleteSeatId")) // Delete seat
        {
            bool deleted = false;
            foreach (var seat in db.Seats.Include(s => s.Tickets).Where(seat => seat.HallId == id && !seat.IsDeleted))
            {
                if (seat.Id == vm.DeleteSeatId)
                {
                    if (seat.Tickets.Count > 0)
                    {
                        seat.IsDeleted = true;
                    }
                    else
                    {
                        db.Seats.Remove(seat);
                    }
                    deleted = true;
                    break;
                }
            }

            if (!deleted)
            {
                return BadRequest("Seat not found.");
            }
        }
        else if (vm.AddRow != null && ModelState.IsValid("AddRow")) // Add row
        {
            if (hall.TotalRows >= 16)
            {
                return BadRequest("Maximum number of rows reached.");
            }

            // Update hall
            hall.TotalRows += 1;

            // Update seat rows and name
            for (int i = vm.AddRow.Value; i < hall.TotalRows; i++)
            {
                foreach (var seat in db.Seats.Where(seat => seat.HallId == id && !seat.IsDeleted && seat.Row == i))
                {
                    seat.Row++;
                }
            }
        }
        else if (vm.AddColumn != null && ModelState.IsValid("AddColumn")) // Add column
        {
            if (hall.TotalColumns >= 20)
            {
                return BadRequest("Maximum number of columns reached.");
            }

            // Update hall
            hall.TotalColumns += 1;

            // Update seat columns and name
            for (int i = 0; i < hall.TotalRows; i++)
            {
                foreach (var seat in hall.Seats.Where(s => !s.IsDeleted && s.Row == i && s.ColumnsList[0] >= vm.AddColumn))
                {
                    var newColumnslist = seat.ColumnsList.ToList();

                    // Increment each column index
                    for (int k = 0; k < newColumnslist.Count; k++)
                    {
                        newColumnslist[k]++;
                    }

                    // Update Columns (through the setter)
                    seat.ColumnsList = newColumnslist;
                }
            }
        }
        else if (vm.DeleteRow != null && ModelState.IsValid("DeleteRow")) // Delete row
        {
            if (hall.Seats.Any(s => s.Row == vm.DeleteRow && !s.IsDeleted)) // Check if there are seats on the row (not deleted)
            {
                return BadRequest("Cannot delete a row that contains seats.");
            }

            // Update hall
            hall.TotalRows -= 1;

            // Update seat rows and name
            for (int i = vm.DeleteRow.Value; i < hall.TotalRows; i++)
            {
                foreach (var seat in hall.Seats.Where(s => !s.IsDeleted && s.Row == i))
                {
                    seat.Row--;
                }
            }
        }
        else if (vm.DeleteColumn != null && ModelState.IsValid("DeleteColumn")) // Delete column
        {
            if (hall.Seats.Any(s => s.ColumnsList.Contains((int)vm.DeleteColumn) && !s.IsDeleted))
            {
                return BadRequest("Cannot delete a column that contains seats.");
            }

            // Update hall
            hall.TotalColumns -= 1;

            // Update seat columns and name
            for (int i = 0; i < hall.TotalRows; i++)
            {
                foreach (var seat in hall.Seats.Where(s => !s.IsDeleted && s.Row == i && s.ColumnsList[0] >= vm.DeleteColumn))
                {
                    var newColumnslist = seat.ColumnsList.ToList();

                    // Decrement each column index
                    for (int k = 0; k < newColumnslist.Count; k++)
                    {
                        newColumnslist[k]--;
                    }

                    // Update Columns (through the setter)
                    seat.ColumnsList = newColumnslist;
                }
            }
        }

        // Remove out of range seats
        foreach (var seatI in hall.Seats.Where(s => s.Row >= hall.TotalRows || s.ColumnsList.Any(c => c >= hall.TotalColumns)).ToList())
        {
            seatI.IsDeleted = true;
        }

        // Remove duplicated seats
        for (int i = 0; i < hall.Seats.Count; i++)
        {
            var seatI = hall.Seats[i];
            if (seatI.IsDeleted) continue;

            for (int j = i + 1; j < hall.Seats.Count; j++)
            {
                var seatJ = hall.Seats[j];
                if (seatJ.IsDeleted) continue;

                // Same row?
                if (seatI.Row == seatJ.Row)
                {
                    // Check if ColumnsList overlap
                    bool overlap = seatI.ColumnsList.Any(c => seatJ.ColumnsList.Contains(c));
                    if (overlap)
                    {
                        seatJ.IsDeleted = true;
                        break;
                    }
                }
            }
        }

        // Update seat names
        for (int r = 0; r < hall.TotalRows; r++)
        {
            var rowSeats = hall.Seats
                .Where(s => !s.IsDeleted && s.Row == r)
                .OrderBy(s => s.ColumnsList.Min()) // ensure left-to-right order
                .ToList();

            int colNumber = 1;
            string rowName = ((char)('A' + r)).ToString();

            foreach (var seat in rowSeats)
            {
                if (seat.SeatType.ColumnSpan == 2)
                {
                    seat.Name = $"{rowName}{colNumber}, {rowName}{colNumber + 1}";
                    colNumber += 2;
                }
                else
                {
                    seat.Name = $"{rowName}{colNumber}";
                    colNumber++;
                }
            }
        }

        db.SaveChanges();

        vm = new()
        {
            Hall = hall,
            AvailableSeatTypes = db.SeatTypes.ToList()
        };

        if (Request.IsAjax())
        {
            return PartialView("_HallLayout", vm.Hall);
        }

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var hall = db.Halls.Include(h => h.Seats).ThenInclude(s => s.Tickets).FirstOrDefault(h => h.Id == id && !h.IsDeleted);
        if (hall == null)
        {
            return NotFound("Hall not found");
        }

        if (showSrv.HallHasActiveShowtime(hall.Id))
        {
            return BadRequest("Cannot delete hall when there are active showtimes.");
        }

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
        bool hasShowtimes = db.Showtimes.Any(s => s.HallId == id && !s.IsDeleted);

        if (hasTickets || hasShowtimes)
        {

            hall.IsDeleted = true;
        }
        else
        {
            db.Halls.Remove(hall);
        }
        db.SaveChanges();

        TempData["Message"] = "Hall deleted successfully!";
        return Ok();
    }
}
