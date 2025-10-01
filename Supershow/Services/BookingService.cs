using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Services;

public class BookingService
{
    private readonly DB db;
    private readonly IHubContext<BookingHub> bookingHubContext;

    public BookingService(DB db, IHubContext<BookingHub> bookingHubContext)
    {
        this.db = db;
        this.bookingHubContext = bookingHubContext;
    }

    private void ProcessBookingCancellation(Booking booking, Dictionary<int, HashSet<int>> reloads)
    {
        foreach (var ticket in booking.Tickets)
        {
            if (!reloads.ContainsKey(booking.ShowtimeId))
                reloads[booking.ShowtimeId] = [];

            reloads[booking.ShowtimeId].Add(ticket.SeatId);
        }

        if (booking.Status == "Pending")
        {
            db.Bookings.Remove(booking);
        }
        else if (booking.Status == "Unpaid")
        {
            booking.Status = "Canceled";

            if (booking.Payment != null)
            {
                db.Payments.Remove(booking.Payment);
            }
        }
        else if (booking.Status == "Confirmed")
        {
            booking.Status = "Canceled";

            if (booking.Payment != null)
            {
                db.WalletTransactions.Add(new()
                {
                    AccountId = booking.AccountId,
                    Amount = booking.Payment.Amount,
                    Description = $"Booking #{booking.Id} (Refund)",
                    PaymentId = booking.Payment.Id
                });

                booking.Account.WalletBalance += booking.Payment.Amount;
            }
        }
    }

    private async Task BroadcastSeatReloads(Dictionary<int, HashSet<int>> reloads)
    {
        foreach (var pair in reloads)
        {
            var showtimeId = pair.Key;
            foreach (var seatId in pair.Value)
            {
                await bookingHubContext.Clients.All.SendAsync("UpdateSeat", showtimeId, null, null, seatId, false);
            }
        }
    }

    public async Task CancelBooking(string id)
    {
        var booking = db.Bookings
            .Include(b => b.Tickets)
            .Include(b => b.Payment)
            .Include(b => b.Account)
            .FirstOrDefault(b => b.Id == id);
        if (booking == null) return;

        Dictionary<int, HashSet<int>> reloads = [];
        ProcessBookingCancellation(booking, reloads);

        db.SaveChanges();
        await BroadcastSeatReloads(reloads);
    }

    public async Task BulkCancelBookings(HashSet<string> ids)
    {
        var bookings = db.Bookings
            .Include(b => b.Tickets)
            .Include(b => b.Payment)
            .Include(b => b.Account)
            .Where(b => ids.Contains(b.Id))
            .ToList();
        Dictionary<int, HashSet<int>> reloads = [];

        foreach (var booking in bookings)
            ProcessBookingCancellation(booking, reloads);

        db.SaveChanges();
        await BroadcastSeatReloads(reloads);
    }

    public async Task ConfirmBooking(string id, string paymentType, string? details)
    {
        var booking = db.Bookings
            .Include(b => b.Payment)
            .Include(b => b.Tickets)
            .FirstOrDefault(b => b.Id == id && b.Status == "Unpaid");
        if (booking == null || booking.Payment == null) return;

        booking.Payment.PaidAt = DateTime.Now;
        booking.Payment.ExpiresAt = null;
        booking.Payment.PaymentType = paymentType;
        booking.Payment.Details = details;

        booking.Status = "Confirmed";

        db.SaveChanges();

        foreach (var ticket in booking.Tickets)
        {
            await bookingHubContext.Clients.All.SendAsync("UpdateSeat", booking.ShowtimeId, null, booking.Status, ticket.SeatId, true);
        }
    }
}