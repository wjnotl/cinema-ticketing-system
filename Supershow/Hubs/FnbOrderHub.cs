using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Hubs;

[Authorize(Roles = "Customer")]
public class FnbOrderHub : Hub
{
    private readonly DB db;

    public FnbOrderHub(DB db)
    {
        this.db = db;
    }

    public async Task AddToCart(string orderId, int itemId, int variantId, bool isIncrement)
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

        var order = db.FnbOrders
            .Include(o => o.FnbOrderItems)
            .FirstOrDefault(o => o.Id == orderId && o.AccountId == account.Id);
        if (order == null)
        {
            await Clients.Caller.SendAsync("Error", "Order not found");
            await Clients.Caller.SendAsync("ReloadItems", null);
            return;
        }

        var itemExists = db.FnbInventories.Any(i => i.CinemaId == order.CinemaId && i.FnbItemVariant.FnbItemId == itemId);
        if (!itemExists)
        {
            await Clients.Caller.SendAsync("Error", "Item not found");
            await Clients.Caller.SendAsync("ReloadItems", null);
            return;
        }

        var inventory = db.FnbInventories
            .Include(i => i.FnbItemVariant)
            .FirstOrDefault(i => i.CinemaId == order.CinemaId && i.FnbItemVariantId == variantId);
        if (inventory == null)
        {
            await Clients.Caller.SendAsync("Error", "Variant not found");
            await Clients.Caller.SendAsync("ReloadVariants", order.CinemaId, itemId);
            return;
        }

        var orderItem = order.FnbOrderItems.FirstOrDefault(oi => oi.FnbItemVariantId == variantId);

        if (isIncrement)
        {
            // Check if there's too much in the cart already
            var totalItems = order.FnbOrderItems.Sum(oi => oi.Quantity);
            if (totalItems >= 20)
            {
                await Clients.Caller.SendAsync("Error", "Your cart can hold a maximum of 20 items");
                return;
            }

            // Check if there's enough stock left
            if (inventory.Quantity <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Not enough stock");
                return;
            }

            inventory.Quantity--;

            // If the order item doesn't exist yet, create a new one
            if (orderItem == null)
            {
                orderItem = new FnbOrderItem()
                {
                    FnbOrderId = orderId,
                    FnbItemVariantId = variantId,
                    Quantity = 1,
                    Price = inventory.FnbItemVariant.Price * 1
                };
                order.FnbOrderItems.Add(orderItem);
            }
            else
            {
                // Otherwise increment its quantity by 1
                orderItem.Quantity++;
                orderItem.Price += inventory.FnbItemVariant.Price;
            }
        }
        else
        {
            // If the order item doesn't exist
            if (orderItem == null)
            {
                await Clients.Caller.SendAsync("Error", "Item not found in your cart");
                return;
            }

            // If the quantity is less than or equal to 0
            if (orderItem.Quantity <= 0)
            {
                await Clients.Caller.SendAsync("Error", "You cannot have negative quantity");
                return;
            }

            inventory.Quantity++;

            // Decrement its quantity by 1
            orderItem.Quantity--;
            orderItem.Price -= inventory.FnbItemVariant.Price;

            // If the quantity reaches zero, remove the order item
            if (orderItem.Quantity <= 0)
            {
                orderItem.Quantity = 0;
                order.FnbOrderItems.Remove(orderItem);
            }
        }

        db.SaveChanges();

        await Clients.All.SendAsync("UpdateItem", orderId, itemId);
        await Clients.All.SendAsync("UpdateCartItemQuantity", orderId, variantId, orderItem.Quantity);
        await Clients.All.SendAsync("UpdateStock", order.CinemaId, variantId, inventory.Quantity);
    }

    /*
    // update items in menu
    await Clients.Caller.SendAsync("UpdateItem", fnbOrderId, itemId);
    // if quantity is 0 and in my cart, remove the element from menu
    // if quantity > 0 and not in my cart, try to call ajax getorderitem to add element to menu

    // update varaints in item menu
    await Clients.Caller.SendAsync("UpdateCartItemQuantity", fnbOrderId, variantId, quantity);
    await Clients.All.SendAsync("UpdateStock", cinemaId, variantId, stockCount);

    // reload variant menu
    await Clients.Caller.SendAsync("ReloadVariants", cinemaId, itemId);

    // reload menu
    await Clients.Caller.SendAsync("ReloadItems", cinemaId);
    */
}