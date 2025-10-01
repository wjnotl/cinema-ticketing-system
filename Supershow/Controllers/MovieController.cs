using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class MovieController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly ShowtimeService showSrv;

    public MovieController(DB db, ImageService imgSrv, ShowtimeService showSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.showSrv = showSrv;
    }

    public IActionResult Index()
    {
        return RedirectToAction("Listing", new { category = "NowShowing" });
    }

    [Route("Movie/Listing/{category?}")]
    public IActionResult Listing(string? category, MovieListingVM vm)
    {
        switch (category)
        {
            case "NowShowing":
            case "ComingSoon":
            case "BookEarly":
                break;
            default:
                return RedirectToAction(null, new { category = "NowShowing" });
        }

        var movies = db.Movies
            .Include(m => m.Showtimes)
            .Where(m => !m.IsDeleted && m.Status != "Inactive")
            .AsQueryable();

        if (category == "NowShowing")
        {
            movies = movies.Where(m => m.ReleaseDate <= DateTime.Now);
        }
        else if (category == "ComingSoon")
        {
            movies = movies.Where(m => m.ReleaseDate > DateTime.Now && !m.Showtimes.Any(s => !s.IsDeleted && s.StartTime.AddMinutes(m.Duration + 10) > DateTime.Now));
        }
        else if (category == "BookEarly")
        {
            movies = movies.Where(m => m.ReleaseDate > DateTime.Now && m.Showtimes.Any(s => !s.IsDeleted && s.StartTime.AddMinutes(m.Duration + 10) > DateTime.Now));
        }

        // Filter genres
        if (vm.Genres.Count > 0)
        {
            movies = movies.Where(m => m.Genres.Any(g => vm.Genres.Contains(g.Id)));
        }

        // Filter languages
        if (vm.Languages.Count > 0)
        {
            movies = movies.Where(m => vm.Languages.Contains(m.SpokenLanguageId));
        }

        // Filter classifications
        if (vm.Classifications.Count > 0)
        {
            movies = movies.Where(m => vm.Classifications.Contains(m.ClassificationId));
        }

        vm.Results = movies.ToList() ?? [];

        if (Request.IsAjax())
        {
            return PartialView("_ListingResult", vm.Results);
        }

        vm.Category = category;
        vm.AvailableGenres = db.Genres.ToList() ?? [];
        vm.AvailableLanguages = db.Languages.ToList() ?? [];
        vm.AvailableClassifications = db.Classifications.ToList() ?? [];

        return View(vm);
    }

    [Route("Movie/Info/{slug}")]
    public IActionResult Info(string slug, MovieInfoVM vm)
    {
        var movie = db.Movies
            .Include(m => m.Classification)
            .Include(m => m.SpokenLanguage)
            .Include(m => m.Genres)
            .Include(m => m.Subtitles)
            .FirstOrDefault(m => m.Slug == slug && !m.IsDeleted && m.Status != "Inactive");
        if (movie == null)
        {
            return NotFound();
        }

        var acc = HttpContext.GetAccount();

        List<Review> reviews = [];
        foreach (var review in movie.Reviews.OrderBy(r => r.CreatedAt).ToList())
        {
            if (acc?.Id == review.AccountId) continue;
            if (vm.FilterRating != "all" && review.Rating.ToString() != vm.FilterRating) continue;
            reviews.Add(review);
        }

        if (Request.IsAjax())
        {
            return PartialView("_Reviews", reviews);
        }

        vm.Movie = movie;
        vm.Reviews = reviews;
        vm.TotalReviews = db.Reviews.Count(r => r.MovieId == movie.Id);
        vm.AverageRating = db.Reviews.Where(r => r.MovieId == movie.Id).Select(r => (decimal?)r.Rating).Average() ?? 0m;
        vm.TotalTicketsSold = db.Tickets.Count(t => t.Booking.Showtime.MovieId == movie.Id);

        ViewBag.HasCommented = false;
        var ownReview = acc != null ? db.Reviews.FirstOrDefault(r => r.AccountId == acc.Id && r.MovieId == movie.Id) : null;
        if (ownReview != null)
        {
            vm.NewReview = new()
            {
                Id = movie.Id,
                Rating = ownReview.Rating,
                Comment = ownReview.Comment
            };
            ViewBag.HasCommented = true;
        }
    
        ViewBag.HasWatchedMovie = acc != null && db.Bookings.Any(b => b.AccountId == acc.Id && b.Status == "Completed" && b.Showtime.StartTime <= DateTime.Now && b.Showtime.MovieId == movie.Id);

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public IActionResult AddReview(ReviewInputVM vm)
    {
        var movie = db.Movies.FirstOrDefault(m => m.Id == vm.Id && !m.IsDeleted && m.Status != "Inactive");
        if (movie == null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (db.Reviews.Any(r => r.AccountId == HttpContext.GetAccount()!.Id && r.MovieId == vm.Id))
        {
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid)
        {
            db.Reviews.Add(new()
            {
                AccountId = HttpContext.GetAccount()!.Id,
                MovieId = vm.Id,
                Rating = vm.Rating,
                Comment = vm.Comment,
            });
            db.SaveChanges();
        }

        var acc = HttpContext.GetAccount();
        ViewBag.HasWatchedMovie = acc != null && db.Bookings.Any(b => b.AccountId == acc.Id && b.Status == "Completed" && b.Showtime.StartTime <= DateTime.Now && b.Showtime.MovieId == movie.Id);

        ViewBag.AccountImageUrl = acc?.Image == null ? "/img/pfp.png" : $"/uploads/account/{acc.Image}";
        ViewBag.Authenticated = acc != null;
        ViewBag.HasCommented = false;
        var ownReview = acc != null ? db.Reviews.FirstOrDefault(r => r.AccountId == acc.Id && r.MovieId == movie.Id) : null;
        if (ownReview != null)
        {
            vm = new()
            {
                Rating = ownReview.Rating,
                Comment = ownReview.Comment
            };
            ViewBag.HasCommented = true;
        }

        vm.Id = movie.Id;

        if (Request.IsAjax())
        {
            return PartialView("_AddReview", vm);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public IActionResult DeleteReview(int Id)
    {
        if (!Request.IsAjax()) return NotFound();

        var review = db.Reviews.FirstOrDefault(r => r.AccountId == HttpContext.GetAccount()!.Id && r.MovieId == Id);
        if (review == null)
        {
            return NotFound("Own review not found");
        }

        db.Reviews.Remove(review);
        db.SaveChanges();

        TempData["Message"] = "You have deleted your review successfully!";
        return Ok();
    }

    [Route("Movie/Showtimes/{slug}")]
    public IActionResult Showtimes(string slug, MovieShowtimeVM vm)
    {
        var movie = db.Movies
            .Include(m => m.SpokenLanguage)
            .Include(m => m.Genres)
            .FirstOrDefault(m => m.Slug == slug && !m.IsDeleted && m.Status != "Inactive");
        if (movie == null)
        {
            return NotFound();
        }

        vm.Movie = movie;
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
                s.MovieId == movie.Id &&
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
        if (vm.Experiences.Count > 0)
        {
            results = results.Where(s => vm.Experiences.Contains(s.Hall.ExperienceId));
        }

        vm.Results = results
            .GroupBy(s => s.Hall.Experience)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(s => s.StartTime).ToList()
            );

        if (Request.IsAjax())
        {
            return PartialView("_Showtimes", (vm.Cinema, vm.Results));
        }

        return View(vm);
    }

    [Authorize(Policy = "Manage Movies")]
    public IActionResult Manage(ManageMovieVM vm)
    {
        Dictionary<string, Expression<Func<Movie, object>>> sortOptions = new()
        {
            { "Id", m => m.Id },
            { "Title", m => m.Title },
            { "Slug", m => m.Slug },
            { "Status", m => m.Status },
            { "Release Date", m => m.ReleaseDate },
            { "Creation Date", m => m.CreatedAt },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() {Value = "title", Text = "Search By Title"},
            new() {Value = "slug", Text = "Search By Slug"},
            new() {Value = "id", Text = "Search By Id"}
        ];
        vm.AvailableStatuses = ["Active", "Inactive"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Movies.Where(m => !m.IsDeleted).AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "title":
                    results = results.Where(m => m.Title.Contains(search));
                    break;
                case "slug":
                    results = results.Where(m => m.Slug.Contains(search));
                    break;
                case "id":
                    results = results.Where(m => m.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Statuses.Count > 0)
        {
            results = results.Where(m => vm.Statuses.Contains(m.Status));
        }

        if (vm.ReleaseFrom != null && ModelState.IsValid("ReleaseFrom"))
        {
            results = results.Where(m => m.ReleaseDate >= vm.ReleaseFrom);
        }

        if (vm.ReleaseTo != null && ModelState.IsValid("ReleaseTo"))
        {
            results = results.Where(m => m.ReleaseDate <= vm.ReleaseTo);
        }

        if (vm.CreationFrom != null && ModelState.IsValid("CreationFrom"))
        {
            results = results.Where(m => m.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null && ModelState.IsValid("CreationTo"))
        {
            results = results.Where(m => m.CreatedAt <= vm.CreationTo);
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

    [Authorize(Policy = "Manage Movies")]
    public IActionResult Add()
    {
        AddMovieVM vm = new()
        {
            ReleaseDate = DateTime.Today.AddDays(1),
            PosterImageScale = 1,
            PosterImageX = 0,
            PosterImageY = 0,
            PosterPreviewWidth = 220,
            PosterPreviewHeight = 330,
            BannerImageScale = 1,
            BannerImageX = 0,
            BannerImageY = 0,
            BannerPreviewWidth = 810,
            BannerPreviewHeight = 270,
            AvailableStatuses = [
                new() { Value = "Active", Text = "Active" },
                new() { Value = "Inactive", Text = "Inactive"}
            ],
            AvailableSpokenLanguages = db.Languages.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name }).ToList(),
            AvailableClassifications = db.Classifications.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList(),
            AvailableSubtitles = db.Subtitles.ToList(),
            AvailableGenres = db.Genres.ToList(),
        };
        vm.Status = vm.AvailableStatuses.First().Value;
        vm.SpokenLanguage = int.Parse(vm.AvailableSpokenLanguages.First().Value);
        vm.Classification = int.Parse(vm.AvailableClassifications.First().Value);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Movies")]
    public IActionResult Add(AddMovieVM vm)
    {
        List<string> AvailableStatuses = ["Active", "Inactive"];
        var AvailableSpokenLanguages = db.Languages;
        var AvailableClassifications = db.Classifications;
        var AvailableSubtitles = db.Subtitles;
        var AvailableGenres = db.Genres;

        if (ModelState.IsValid("Slug") && !IsSlugUnique(vm.Slug))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Status") && !AvailableStatuses.Contains(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid status.");
        }

        if (ModelState.IsValid("SpokenLanguage") && !AvailableSpokenLanguages.Any(l => l.Id == vm.SpokenLanguage))
        {
            ModelState.AddModelError("SpokenLanguage", "Invalid spoken language.");
        }

        if (ModelState.IsValid("Classification") && !AvailableClassifications.Any(c => c.Id == vm.Classification))
        {
            ModelState.AddModelError("Classification", "Invalid classification.");
        }

        if (ModelState.IsValid("Subtitles") && !vm.Subtitles.All(id => AvailableSubtitles.Any(s => s.Id == id)))
        {
            ModelState.AddModelError("Subtitles", "Invalid subtitles.");
        }

        if (ModelState.IsValid("Genres") && vm.Genres.Count == 0)
        {
            ModelState.AddModelError("Genres", "Must select at least one genre.");
        }

        if (ModelState.IsValid("Genres") && !vm.Genres.All(id => AvailableGenres.Any(g => g.Id == id)))
        {
            ModelState.AddModelError("Genres", "Invalid genres.");
        }

        if (vm.PosterImage != null)
        {
            var e = imgSrv.ValidateImage(vm.PosterImage, 2);
            if (e != "") ModelState.AddModelError("PosterImage", e);
        }

        if (vm.BannerImage != null)
        {
            var e = imgSrv.ValidateImage(vm.BannerImage, 2);
            if (e != "") ModelState.AddModelError("BannerImage", e);
        }

        string? newPosterFile = null;
        if (ModelState.IsValid && vm.PosterImage != null)
        {
            try
            {
                newPosterFile = imgSrv.SaveImage(vm.PosterImage, "poster", 750, 1125, vm.PosterPreviewWidth, vm.PosterPreviewHeight, vm.PosterImageX, vm.PosterImageY, vm.PosterImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("PosterImage", ex.Message);
            }
        }

        string? newBannerFile = null;
        if (ModelState.IsValid && vm.BannerImage != null)
        {
            try
            {
                newBannerFile = imgSrv.SaveImage(vm.BannerImage, "banner", 2025, 675, vm.BannerPreviewWidth, vm.BannerPreviewHeight, vm.BannerImageX, vm.BannerImageY, vm.BannerImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("BannerImage", ex.Message);
            }
        }

        if (ModelState.IsValid && newPosterFile != null)
        {
            db.Movies.Add(new()
            {
                Title = vm.Title,
                Slug = vm.Slug,
                ReleaseDate = vm.ReleaseDate,
                Duration = vm.Duration,
                Director = vm.Director,
                Cast = vm.Cast,
                Synopsis = vm.Synopsis,
                Trailer = vm.Trailer,
                Price = vm.Price,
                Status = vm.Status,
                SpokenLanguageId = vm.SpokenLanguage,
                ClassificationId = vm.Classification,
                Poster = newPosterFile,
                Banner = newBannerFile,
                Subtitles = vm.Subtitles.Select(s => AvailableSubtitles.First(sub => sub.Id == s)).ToList(),
                Genres = vm.Genres.Select(g => AvailableGenres.First(genre => genre.Id == g)).ToList(),
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage");
        }

        vm.AvailableStatuses = AvailableStatuses.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
        vm.AvailableSpokenLanguages = AvailableSpokenLanguages.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name }).ToList();
        vm.AvailableClassifications = AvailableClassifications.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        vm.AvailableSubtitles = AvailableSubtitles.ToList();
        vm.AvailableGenres = AvailableGenres.ToList();

        return View(vm);
    }

    [Authorize(Policy = "Manage Movies")]
    public IActionResult Edit(int id)
    {
        var movie = db.Movies
        .Include(m => m.Subtitles)
        .Include(m => m.Genres)
        .FirstOrDefault(m => m.Id == id && !m.IsDeleted);
        if (movie == null)
        {
            return NotFound();
        }

        EditMovieVM vm = new()
        {
            HasActiveShowtimes = showSrv.HallHasActiveShowtime(movie.Id),
            Id = id,
            CreatedAt = movie.CreatedAt,
            Title = movie.Title,
            Slug = movie.Slug,
            ReleaseDate = movie.ReleaseDate,
            Duration = movie.Duration,
            Director = movie.Director,
            Cast = movie.Cast,
            Synopsis = movie.Synopsis,
            Trailer = movie.Trailer,
            Price = movie.Price,
            Status = movie.Status,
            SpokenLanguage = movie.SpokenLanguageId,
            Classification = movie.ClassificationId,
            Subtitles = movie.Subtitles.Select(s => s.Id).ToList(),
            Genres = movie.Genres.Select(g => g.Id).ToList(),
            PosterImageScale = 1,
            PosterImageX = 0,
            PosterImageY = 0,
            PosterPreviewWidth = 220,
            PosterPreviewHeight = 330,
            RemoveBannerImage = false,
            BannerImageScale = 1,
            BannerImageX = 0,
            BannerImageY = 0,
            BannerPreviewWidth = 810,
            BannerPreviewHeight = 270,
            AvailableStatuses = [
                new() { Value = "Active", Text = "Active" },
                new() { Value = "Inactive", Text = "Inactive"}
            ],
            AvailableSpokenLanguages = db.Languages.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name }).ToList(),
            AvailableClassifications = db.Classifications.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList(),
            AvailableSubtitles = db.Subtitles.ToList(),
            AvailableGenres = db.Genres.ToList(),
        };

        ViewBag.PosterImageUrl = movie.Poster;
        ViewBag.BannerImageUrl = movie.Banner;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Movies")]
    public IActionResult Edit(EditMovieVM vm)
    {
        var movie = db.Movies
        .Include(m => m.Subtitles)
        .Include(m => m.Genres)
        .FirstOrDefault(m => m.Id == vm.Id && !m.IsDeleted);
        if (movie == null)
        {
            return NotFound();
        }

        List<string> AvailableStatuses = ["Active", "Inactive"];
        var AvailableSpokenLanguages = db.Languages;
        var AvailableClassifications = db.Classifications;
        var AvailableSubtitles = db.Subtitles;
        var AvailableGenres = db.Genres;

        vm.HasActiveShowtimes = showSrv.HallHasActiveShowtime(movie.Id);

        if (ModelState.IsValid("Slug") && !IsSlugUniqueEdit(vm.Slug, vm.Id))
        {
            ModelState.AddModelError("Slug", "Slug has been taken.");
        }

        if (ModelState.IsValid("Status") && !AvailableStatuses.Contains(vm.Status))
        {
            ModelState.AddModelError("Status", "Invalid status.");
        }

        if (ModelState.IsValid("SpokenLanguage") && !AvailableSpokenLanguages.Any(l => l.Id == vm.SpokenLanguage))
        {
            ModelState.AddModelError("SpokenLanguage", "Invalid spoken language.");
        }

        if (ModelState.IsValid("Classification") && !AvailableClassifications.Any(c => c.Id == vm.Classification))
        {
            ModelState.AddModelError("Classification", "Invalid classification.");
        }

        if (ModelState.IsValid("Subtitles") && !vm.Subtitles.All(id => AvailableSubtitles.Any(s => s.Id == id)))
        {
            ModelState.AddModelError("Subtitles", "Invalid subtitles.");
        }

        if (ModelState.IsValid("Genres") && vm.Genres.Count == 0)
        {
            ModelState.AddModelError("Genres", "Must select at least one genre.");
        }

        if (ModelState.IsValid("Genres") && !vm.Genres.All(id => AvailableGenres.Any(g => g.Id == id)))
        {
            ModelState.AddModelError("Genres", "Invalid genres.");
        }

        if (ModelState.IsValid && vm.PosterImage != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.PosterImage, "poster", 750, 1125, vm.PosterPreviewWidth, vm.PosterPreviewHeight, vm.PosterImageX, vm.PosterImageY, vm.PosterImageScale);

                // remove image
                if (movie.Poster != null) imgSrv.DeleteImage(movie.Poster, "poster");
                movie.Poster = newFile;
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("PosterImage", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            if (vm.RemoveBannerImage)
            {
                // remove image
                if (movie.Banner != null)
                {
                    imgSrv.DeleteImage(movie.Banner, "banner");
                    movie.Banner = null;
                    db.SaveChanges();
                }
            }
            else if (vm.BannerImage != null)
            {
                try
                {
                    var newFile = imgSrv.SaveImage(vm.BannerImage, "banner", 2025, 675, vm.BannerPreviewWidth, vm.BannerPreviewHeight, vm.BannerImageX, vm.BannerImageY, vm.BannerImageScale);

                    // remove image
                    if (movie.Banner != null) imgSrv.DeleteImage(movie.Banner, "banner");
                    movie.Banner = newFile;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("BannerImage", ex.Message);
                }
            }
        }

        if (ModelState.IsValid)
        {
            movie.Title = vm.Title;
            movie.Slug = vm.Slug;
            movie.ReleaseDate = vm.ReleaseDate;
            if (!vm.HasActiveShowtimes)
            {
                movie.Duration = vm.Duration;
            }
            movie.Director = vm.Director;
            movie.Cast = vm.Cast;
            movie.Synopsis = vm.Synopsis;
            movie.Trailer = vm.Trailer;
            movie.Price = vm.Price;
            movie.Status = vm.Status;
            movie.SpokenLanguageId = vm.SpokenLanguage;
            movie.ClassificationId = vm.Classification;
            movie.Subtitles.Clear();
            foreach (var subtitle in vm.Subtitles)
            {
                movie.Subtitles.Add(AvailableSubtitles.First(s => s.Id == subtitle));
            }
            movie.Genres.Clear();
            foreach (var genre in vm.Genres)
            {
                movie.Genres.Add(AvailableGenres.First(g => g.Id == genre));
            }
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = movie.Id });
        }

        vm.AvailableStatuses = AvailableStatuses.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
        vm.AvailableSpokenLanguages = AvailableSpokenLanguages.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name }).ToList();
        vm.AvailableClassifications = AvailableClassifications.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name }).ToList();
        vm.AvailableSubtitles = AvailableSubtitles.ToList();
        vm.AvailableGenres = AvailableGenres.ToList();

        ViewBag.PosterImageUrl = movie.Poster;
        ViewBag.BannerImageUrl = movie.Banner;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage Movies")]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var movie = db.Movies.FirstOrDefault(m => m.Id == id && !m.IsDeleted);
        if (movie == null)
        {
            return NotFound("Movie not found");
        }

        if (showSrv.MovieHasActiveShowtime(id))
        {
            return BadRequest("Cannot delete movie with active showtimes.");
        }

        // Remove reviews
        db.Reviews.RemoveRange(db.Reviews.Where(r => r.MovieId == id));

        // Remove images
        if (movie.Poster != null) imgSrv.DeleteImage(movie.Poster, "poster");
        if (movie.Banner != null) imgSrv.DeleteImage(movie.Banner, "banner");

        // Remove movie
        bool hasShowtimes = db.Showtimes.Any(st => st.MovieId == id && !st.IsDeleted);
        if (hasShowtimes)
        {
            movie.IsDeleted = true;
        }
        else
        {
            db.Movies.Remove(movie);
        }
        db.SaveChanges();

        TempData["Message"] = "Movie deleted successfully!";
        return Ok();
    }

    // ======================REMOTE METHODS======================
    public bool IsSlugUnique(string slug)
    {
        return !db.Movies.Any(m => m.Slug == slug && !m.IsDeleted);
    }

    public bool IsSlugUniqueEdit(string slug, int id)
    {
        return !db.Movies.Any(m => m.Id != id && m.Slug == slug && !m.IsDeleted);
    }
}

