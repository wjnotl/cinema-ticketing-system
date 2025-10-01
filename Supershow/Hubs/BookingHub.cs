using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Hubs;

[Authorize(Roles = "Customer")]
public class BookingHub : Hub
{
    private readonly DB db;

    public BookingHub(DB db)
    {
        this.db = db;
    }

    public async Task UpdateSeat(string bookingId, int seatId, bool selected)
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            await Clients.Caller.SendAsync("Error", "No HTTP context available");
            return;
        }

        var account = httpContext.GetAccount();
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Unauthorized");
            return;
        }

        var booking = db.Bookings
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Showtime.Hall)
                .ThenInclude(h => h.Seats)
                    .ThenInclude(s => s.SeatType)
            .Include(b => b.Showtime.Hall.Experience)
            .Include(b => b.Showtime.Bookings)
                .ThenInclude(bk => bk.Tickets)
            .FirstOrDefault(b =>
                b.Id == bookingId &&
                b.Status == "Pending" &&
                b.ExpiresAt > DateTime.Now &&
                b.AccountId == account.Id
            );
        if (booking == null)
        {
            await Clients.Caller.SendAsync("Error", "Booking not found");
            await Clients.Caller.SendAsync("RequestRemapLayout", null);
            return;
        }

        var seat = booking.Showtime.Hall.Seats.FirstOrDefault(s => s.Id == seatId && !s.IsDeleted);
        if (seat == null)
        {
            await Clients.Caller.SendAsync("Error", "Seat not found");
            await Clients.Caller.SendAsync("RequestRemapLayout", booking.ShowtimeId);
            return;
        }

        if (selected)
        {
            if (booking.Tickets.Count >= 10)
            {
                await Clients.Caller.SendAsync("Error", "Maximum number of seats reached");
                return;
            }

            var seatBooking = booking.Showtime.Bookings.Where(b => b.Status != "Canceled").FirstOrDefault(b => b.Tickets.Any(t => t.SeatId == seatId));
            if (seatBooking != null)
            {
                if (seatBooking.Id != booking.Id)
                    await Clients.Caller.SendAsync("Error", "Seat already booked by someone");
                else
                    await Clients.Caller.SendAsync("Error", "You already booked this seat");

                await Clients.Caller.SendAsync("RequestRemapLayout", booking.ShowtimeId);
                return;
            }

            bool isWeekend = booking.Showtime.StartTime.DayOfWeek == DayOfWeek.Saturday || booking.Showtime.StartTime.DayOfWeek == DayOfWeek.Sunday;
            decimal ticketPrice = booking.Showtime.Movie.Price + booking.Showtime.Hall.Experience.Price;
            ticketPrice *= seat.SeatType.ColumnSpan;
            ticketPrice += isWeekend ? seat.SeatType.WeekendPrice : seat.SeatType.Price;

            // New ticket
            booking.Tickets.Add(new()
            {
                BookingId = booking.Id,
                SeatId = seat.Id,
                Price = ticketPrice
            });
        }
        else
        {
            var seatTicket = booking.Showtime.Bookings.Where(b => b.Status != "Canceled" && b.Id == bookingId).SelectMany(b => b.Tickets).FirstOrDefault(t => t.SeatId == seatId);
            if (seatTicket == null)
            {
                await Clients.Caller.SendAsync("Error", "Seat not booked");
                await Clients.Caller.SendAsync("RequestRemapLayout", booking.ShowtimeId);
                return;
            }

            booking.Tickets.Remove(seatTicket);
        }

        db.SaveChanges();
        await Clients.All.SendAsync("UpdateSeat", booking.ShowtimeId, booking.Id, booking.Status, seatId, selected);
    }
}