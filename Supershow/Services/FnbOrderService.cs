using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Services;

public class FnbOrderService
{
    private readonly DB db;
    private readonly IHubContext<FnbOrderHub> fnbOrderHubContext;

    public FnbOrderService(DB db, IHubContext<FnbOrderHub> fnbOrderHubContext)
    {
        this.db = db;
        this.fnbOrderHubContext = fnbOrderHubContext;
    }

    private void ProcessFnbOrderCancellation(FnbOrder order, Dictionary<int, Dictionary<int, int>> reloads)
    {
        foreach (var item in order.FnbOrderItems)
        {
            var variant = db.FnbItemVariants.FirstOrDefault(v => v.Id == item.FnbItemVariantId && !v.IsDeleted);
            if (variant == null) continue;

            var inventory = db.FnbInventories.FirstOrDefault(i => i.CinemaId == order.CinemaId && i.FnbItemVariantId == variant.Id);
            if (inventory == null) continue;

            inventory.Quantity += item.Quantity;

            if (!reloads.ContainsKey(inventory.CinemaId))
                reloads[inventory.CinemaId] = [];

            reloads[inventory.CinemaId][variant.Id] = inventory.Quantity;
        }

        if (order.Status == "Pending")
        {
            db.FnbOrders.Remove(order);
        }
        else if (order.Status == "Unpaid")
        {
            order.Status = "Canceled";

            if (order.Payment != null)
            {
                db.Payments.Remove(order.Payment);
            }
        }
        else if (order.Status == "Confirmed")
        {
            order.Status = "Canceled";
            order.PickupExpiresAt = null;

            if (order.Payment != null)
            {
                db.WalletTransactions.Add(new()
                {
                    AccountId = order.AccountId,
                    Amount = order.Payment.Amount,
                    Description = $"F&B Order #{order.Id} (Refund)",
                    PaymentId = order.Payment.Id
                });

                order.Account.WalletBalance += order.Payment.Amount;
            }
        }
    }

    private async Task BroadcastStockReloads(Dictionary<int, Dictionary<int, int>> reloads)
    {
        foreach (var pair in reloads)
        {
            var cinemaId = pair.Key;
            foreach (var kv in pair.Value)
            {
                await fnbOrderHubContext.Clients.All.SendAsync("UpdateStock", cinemaId, kv.Key, kv.Value);
            }
        }
    }

    public async Task CancelFnbOrder(string id)
    {
        var order = db.FnbOrders
            .Include(o => o.FnbOrderItems)
            .Include(o => o.Payment)
            .Include(o => o.Account)
            .FirstOrDefault(o => o.Id == id);
        if (order == null) return;

        Dictionary<int, Dictionary<int, int>> reloads = [];
        ProcessFnbOrderCancellation(order, reloads);

        db.SaveChanges();
        await BroadcastStockReloads(reloads);
    }

    public async Task BulkCancelFnbOrders(HashSet<string> ids)
    {
        var orders = db.FnbOrders
            .Include(o => o.FnbOrderItems)
            .Include(o => o.Payment)
            .Include(o => o.Account)
            .Where(o => ids.Contains(o.Id))
            .ToList();
        Dictionary<int, Dictionary<int, int>> reloads = [];

        foreach (var order in orders)
            ProcessFnbOrderCancellation(order, reloads);

        db.SaveChanges();
        await BroadcastStockReloads(reloads);
    }

    public void ConfirmFnbOrder(string id, string paymentType, string? details)
    {
        var order = db.FnbOrders
            .Include(o => o.Payment)
            .FirstOrDefault(o => o.Id == id && o.Status == "Unpaid");
        if (order == null || order.Payment == null) return;

        order.Payment.PaidAt = DateTime.Now;
        order.Payment.ExpiresAt = null;
        order.Payment.PaymentType = paymentType;
        order.Payment.Details = details;

        order.Status = "Confirmed";
        order.PickupExpiresAt = DateTime.Now.AddHours(24);

        db.SaveChanges();
    }

    public bool ItemHasActiveOrder(int itemId)
    {
        return db.FnbOrderItems.Any(oi =>
            oi.FnbItemVariant.FnbItemId == itemId &&
            !oi.FnbItemVariant.IsDeleted &&
            !oi.FnbItemVariant.FnbItem.IsDeleted &&
            oi.FnbOrder.Status != "Canceled" &&
            oi.FnbOrder.Status != "Confirmed");
    }

    public bool VariantHasActiveOrder(int variantId)
    {
        return db.FnbOrderItems
        .Any(f => f.FnbItemVariantId == variantId
               && f.FnbItemVariant.IsDeleted == false
               && f.FnbOrder.Status != "Canceled"
               && f.FnbOrder.Status != "Confirmed");
    }

    public bool CinemaHasActiveOrder(int cinemaId)
    {
        return db.FnbOrders.Any(f => f.CinemaId == cinemaId && f.Status != "Canceled" && f.Status != "Confirmed");
    }
}