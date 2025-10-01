using Microsoft.EntityFrameworkCore;

namespace Supershow.Services;

public class ShowtimeService
{
    private readonly DB db;

    public ShowtimeService(DB db)
    {
        this.db = db;
    }

    public bool HasActiveBooking(int id)
    {
        var showtime = db.Showtimes
            .Include(s => s.Bookings)
            .FirstOrDefault(s => s.Id == id && !s.IsDeleted);

        return showtime != null && showtime.Bookings.Any(b => b.Status != "Canceled" || b.Status != "Completed");
    }

    public static DateTime GetEndTime(DateTime startTime, int duration)
    {
        return startTime.AddMinutes(duration + 10);
    }

    public bool HallHasActiveShowtime(int hallId)
    {
        var hall = db.Halls
            .Include(h => h.Showtimes)
            .ThenInclude(s => s.Movie)
            .FirstOrDefault(h => h.Id == hallId && !h.IsDeleted);

        return hall != null && hall.Showtimes.Any(s => !s.IsDeleted && GetEndTime(s.StartTime, s.Movie.Duration) > DateTime.Now);
    }

    public bool CinemaHasActiveShowtime(int cinemaId)
    {
        var cinema = db.Cinemas
            .Include(c => c.Halls)
            .ThenInclude(h => h.Showtimes)
            .ThenInclude(s => s.Movie)
            .FirstOrDefault(c => c.Id == cinemaId && !c.IsDeleted);

        return cinema != null && cinema.Halls.Any(h => !h.IsDeleted && h.Showtimes.Any(s => !s.IsDeleted && GetEndTime(s.StartTime, s.Movie.Duration) > DateTime.Now));
    }

    public bool MovieHasActiveShowtime(int movieId)
    {
        var movie = db.Movies
            .Include(m => m.Showtimes)
            .FirstOrDefault(m => m.Id == movieId && !m.IsDeleted && m.Status != "Inactive");

        return movie != null && movie.Showtimes.Any(s => !s.IsDeleted && GetEndTime(s.StartTime, movie.Duration) > DateTime.Now);
    }
}