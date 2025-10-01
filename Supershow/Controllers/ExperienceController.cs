using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class ExperienceController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    public ExperienceController(DB db, ImageService imgSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
    }

    public IActionResult Index()
    {
        var slug = db.Experiences.FirstOrDefault()?.Slug;
        if (string.IsNullOrEmpty(slug))
        {
            return NotFound();
        }

        return RedirectToAction("Showtimes", new { slug });
    }

    [Route("Experience/Info/{slug}")]
    public IActionResult Info(string slug)
    {
        var experience = db.Experiences.FirstOrDefault(x => x.Slug == slug);
        if (experience == null)
        {
            return NotFound();
        }

        return View(experience);
    }

    [Route("Experience/Showtimes/{slug}")]
    public IActionResult Showtimes(string slug, ExperienceShowtimeVM vm)
    {
        var experience = db.Experiences.FirstOrDefault(x => x.Slug == slug);
        if (experience == null)
        {
            return NotFound();
        }

        vm.Experience = experience;
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
                s.Hall.Experience.Id == experience.Id &&
                s.StartTime.AddMinutes(-20) > DateTime.Now
            )
            .AsQueryable();


        vm.AvailableDateFilters = results.Select(s => s.StartTime.Date).Distinct().OrderBy(d => d).ToList();
        if (vm.AvailableDateFilters.Count > 0 && (vm.Date == null || !vm.AvailableDateFilters.Contains(vm.Date.Value)))
        {
            vm.Date = vm.AvailableDateFilters.First();
        }

        vm.AvailableCinemas = results.Select(s => s.Hall.Cinema).Distinct()
            .GroupBy(c => c.State)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Name).ToList()
            );
        if (vm.AvailableCinemas.Count > 0 && !vm.AvailableCinemas.Any(group => group.Value.Any(c => c.Id == vm.CinemaId)))
        {
            vm.CinemaId = vm.AvailableCinemas.First().Value.First().Id;
        }
        vm.Cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted);

        // Apply filters
        results = results.Where(s => s.Hall.CinemaId == vm.CinemaId);
        results = results.Where(s => s.StartTime.Date == vm.Date);
        if (vm.Times.Count > 0)
        {
            results = results.Where(s =>
                vm.Times.Contains(1) && s.StartTime.Hour < 12 ||
                vm.Times.Contains(2) && s.StartTime.Hour >= 12 && s.StartTime.Hour < 18 ||
                vm.Times.Contains(3) && s.StartTime.Hour >= 18
            );
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

    [Authorize(Policy = "Manage Experiences")]
    public IActionResult Manage(ManageExperienceVM vm)
    {
        Dictionary<string, Expression<Func<Experience, object>>> sortOptions = new()
        {
            { "Id", e => e.Id },
            { "Name", e => e.Name },
            { "Slug", e => e.Slug },
            { "Price", e => e.Price },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "slug", Text = "Search By Slug" },
            new() { Value = "id", Text = "Search By Id" }
        ];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Experiences.AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(e => e.Name.Contains(search));
                    break;
                case "slug":
                    results = results.Where(e => e.Slug.Contains(search));
                    break;
                case "id":
                    results = results.Where(e => e.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinPrice != null && ModelState.IsValid("MinPrice"))
        {
            results = results.Where(e => e.Price >= vm.MinPrice);
        }

        if (vm.MaxPrice != null && ModelState.IsValid("MaxPrice"))
        {
            results = results.Where(e => e.Price <= vm.MaxPrice);
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

    [Authorize(Policy = "Manage Experiences")]
    public IActionResult Edit(int id)
    {
        var experience = db.Experiences.FirstOrDefault(e => e.Id == id);
        if (experience == null)
        {
            return NotFound();
        }

        EditExperienceVM vm = new()
        {
            Id = id,
            Name = experience.Name,
            Slug = experience.Slug,
            TagLine = experience.TagLine,
            Description = experience.Description,
            Includes = experience.Includes,
            Price = experience.Price,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 810,
            PreviewHeight = 270
        };

        ViewBag.PosterImageUrl = experience.Banner;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Experiences")]
    public IActionResult Edit(EditExperienceVM vm)
    {
        var experience = db.Experiences.FirstOrDefault(e => e.Id == vm.Id);
        if (experience == null)
        {
            return NotFound();
        }

        if (vm.BannerImage != null)
        {
            var e = imgSrv.ValidateImage(vm.BannerImage, 2);
            if (e != "") ModelState.AddModelError("BannerImage", e);
        }

        if (ModelState.IsValid && vm.BannerImage != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.BannerImage, "experience", 2025, 675, vm.PreviewWidth, vm.PreviewHeight, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (experience.Banner != null) imgSrv.DeleteImage(experience.Banner, "experience");
                experience.Banner = newFile;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("BannerImage", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            experience.TagLine = vm.TagLine;
            experience.Description = vm.Description;
            experience.Includes = vm.Includes;
            experience.Price = vm.Price;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        ViewBag.PosterImageUrl = experience.Banner;

        return View(vm);
    }
}