namespace Supershow.Services;

public class ExpiryCleanupService
{
    private readonly DB db;
    private readonly BookingService bookSrv;
    private readonly FnbOrderService fnbSrv;

    public ExpiryCleanupService(DB db, BookingService bookSrv, FnbOrderService fnbSrv)
    {
        this.db = db;
        this.bookSrv = bookSrv;
        this.fnbSrv = fnbSrv;
    }

    public async Task Cleanup()
    {
        // remove all expired verification reqeusts if more than 1 day
        db.Verifications.RemoveRange(db.Verifications.Where(u => u.ExpiresAt < DateTime.Now.AddDays(-1)));

        // Set HashSet
        HashSet<string> BookingsToCancel = [];
        HashSet<string> FnbOrdersToCancel = [];

        // Cancel all expired pending bookings and orders
        BookingsToCancel.UnionWith(db.Bookings.Where(b => b.ExpiresAt < DateTime.Now).Select(b => b.Id));
        FnbOrdersToCancel.UnionWith(db.FnbOrders.Where(o => o.ExpiresAt < DateTime.Now).Select(o => o.Id));

        // Cancel all expired unpaid bookings and orders
        BookingsToCancel.UnionWith(db.Bookings.Where(b => b.Payment != null && b.Payment.ExpiresAt < DateTime.Now).Select(b => b.Id));
        FnbOrdersToCancel.UnionWith(db.FnbOrders.Where(o => o.Payment != null && o.Payment.ExpiresAt < DateTime.Now).Select(o => o.Id));

        // Cancel all expired confirmed unclaimed orders
        FnbOrdersToCancel.UnionWith(db.FnbOrders.Where(o => o.PickupExpiresAt < DateTime.Now).Select(o => o.Id));

        // Handle all confirmed bookings
        foreach(var booking in db.Bookings.Where(b => b.Status == "Confirmed" && b.Showtime.StartTime < DateTime.Now).ToList())
        {
            booking.Status = "Completed";
        }
        db.SaveChanges();

        // Handle deletion of accounts
        var deletedUsers = db.Accounts.Where(a => a.DeletionAt < DateTime.Now && !a.IsDeleted).ToList();
        foreach (var account in deletedUsers)
        {
            account.IsDeleted = true;
            account.DeletionAt = null;

            // Cancel all bookings and orders that owned by deleted users
            BookingsToCancel.UnionWith(db.Bookings.Where(b => b.AccountId == account.Id).Select(b => b.Id));
            FnbOrdersToCancel.UnionWith(db.FnbOrders.Where(o => o.AccountId == account.Id).Select(o => o.Id));
        }

        // Process cancellation
        await bookSrv.BulkCancelBookings(BookingsToCancel);
        await fnbSrv.BulkCancelFnbOrders(FnbOrdersToCancel);
    }
}