using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class FnbOrderController : Controller
{
    private readonly DB db;
    private readonly FnbOrderService fnbSrv;

    public FnbOrderController(DB db, FnbOrderService fnbSrv)
    {
        this.db = db;
        this.fnbSrv = fnbSrv;
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Create(int id)
    {
        var order = db.FnbOrders.FirstOrDefault(o => o.AccountId == HttpContext.GetAccount()!.Id && (o.Status == "Pending" || o.Status == "Unpaid"));
        if (order != null)
        {
            TempData["Message"] = "Finish or cancel your current F&B order first";
            return RedirectToAction("History", "Account");
        }

        var cinema = db.Cinemas
            .Include(c => c.FnbInventories)
            .FirstOrDefault(c => c.Id == id && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        if (cinema.FnbInventories.Count == 0)
        {
            TempData["Message"] = "F&B not available at this cinema";
            return RedirectToAction("Index", "Fnb");
        }

        order = new FnbOrder
        {
            Status = "Pending",
            ExpiresAt = DateTime.Now.AddMinutes(5),
            CinemaId = cinema.Id,
            AccountId = HttpContext.GetAccount()!.Id,
        };
        db.FnbOrders.Add(order);
        db.SaveChanges();

        return RedirectToAction("Menu", new { id = order.Id });
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Menu(string id, FnbMenuVM vm)
    {
        var order = db.FnbOrders
            .Include(o => o.Cinema)
            .Include(o => o.Cinema.FnbInventories)
            .ThenInclude(i => i.FnbItemVariant)
            .ThenInclude(v => v.FnbItem)
            .ThenInclude(f => f.FnbCategory)
            .Include(o => o.FnbOrderItems)
            .ThenInclude(oi => oi.FnbItemVariant)
            .FirstOrDefault(o =>
                o.Id.ToString() == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.ExpiresAt > DateTime.Now
            );
        if (order == null)
        {
            return NotFound();
        }

        if (order.Cinema.FnbInventories.Count == 0)
        {
            TempData["Message"] = "F&B not available at this cinema";
            return RedirectToAction("Index", "Fnb");
        }

        if (Request.Method == "POST")
        {
            if (order.FnbOrderItems.Count == 0)
            {
                TempData["Message"] = "Your cart is empty.";
                return RedirectToAction("Menu", new { id });
            }

            return RedirectToAction("Checkout", new { id });
        }

        var results = order.Cinema.FnbInventories.Select(i => i.FnbItemVariant.FnbItem).Distinct().AsQueryable();

        vm.FnbOrder = order;

        vm.AvailableCategories = results.Select(i => i.FnbCategory).Distinct().ToList();
        if (vm.CategoryId == 0)
        {
            // Your cart
            var selectedItemIds = order.FnbOrderItems.Select(oi => oi.FnbItemVariant.FnbItemId).Distinct().ToList();
            results = results.Where(i => selectedItemIds.Contains(i.Id));
        }
        else
        {
            if (vm.AvailableCategories.Count > 0 && !vm.AvailableCategories.Any(c => c.Id == vm.CategoryId))
            {
                vm.CategoryId = vm.AvailableCategories.First().Id;
            }
            vm.Category = db.FnbCategories.FirstOrDefault(c => c.Id == vm.CategoryId);

            results = results.Where(i => i.FnbCategoryId == vm.CategoryId);
        }

        vm.Results = results.ToList();

        if (Request.IsAjax())
        {
            return PartialView("_Menu", vm);
        }

        ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt!.Value).ToUnixTimeMilliseconds();
        ViewBag.TotalOrderPrice = order.FnbOrderItems.Sum(oi => oi.Quantity * oi.FnbItemVariant.Price);
        ViewBag.TotalOrderItems = order.FnbOrderItems.Sum(oi => oi.Quantity);

        return View(vm);
    }

    [Authorize(Roles = "Customer")]
    public IActionResult RenderFnbItem(string id, int itemId, int? categoryId)
    {
        var order = db.FnbOrders
            .Include(o => o.Cinema.FnbInventories)
            .ThenInclude(i => i.FnbItemVariant)
            .Include(o => o.FnbOrderItems)
            .FirstOrDefault(o =>
                o.Id.ToString() == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.ExpiresAt > DateTime.Now
            );
        if (order == null)
        {
            return NotFound();
        }

        var item = db.FnbItems.FirstOrDefault(i => i.Id == itemId && !i.IsDeleted);
        if (item == null)
        {
            return NotFound();
        }

        if (Request.IsAjax())
        {
            var quantity = order.FnbOrderItems.Where(oi => oi.FnbItemVariant.FnbItemId == item.Id).Sum(oi => oi.Quantity);
            var emptyCart = categoryId == 0 && order.FnbOrderItems.Count == 0;

            return PartialView("_FnbItem", (item, quantity, categoryId, emptyCart));
        }

        return NotFound();
    }

    [Authorize(Roles = "Customer")]
    public IActionResult FnbVariantMenu(string id, int itemId)
    {
        var order = db.FnbOrders
            .Include(o => o.Cinema.FnbInventories)
            .ThenInclude(i => i.FnbItemVariant)
            .ThenInclude(i => i.FnbOrderItems)
            .FirstOrDefault(o =>
                o.Id.ToString() == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.ExpiresAt > DateTime.Now
            );
        if (order == null)
        {
            return NotFound();
        }

        var variants = order.Cinema.FnbInventories
            .Where(i => i.FnbItemVariant.FnbItemId == itemId)
            .Select(i => i.FnbItemVariant)
            .ToList();

        if (Request.IsAjax())
        {
            return PartialView("_FnbVariantMenu", (order, variants));
        }

        return NotFound();
    }

    [Authorize(Roles = "Customer")]
    public IActionResult RenderSummary(string id)
    {
        var order = db.FnbOrders
            .Include(o => o.FnbOrderItems)
            .ThenInclude(oi => oi.FnbItemVariant)
            .FirstOrDefault(o =>
                o.Id.ToString() == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.ExpiresAt > DateTime.Now
            );
        if (order == null)
        {
            return NotFound();
        }

        ViewBag.TotalOrderPrice = order.FnbOrderItems.Sum(oi => oi.Quantity * oi.FnbItemVariant.Price);
        ViewBag.TotalOrderItems = order.FnbOrderItems.Sum(oi => oi.Quantity);

        if (Request.IsAjax())
        {
            return PartialView("_Summary");
        }

        return NotFound();
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Checkout(string id)
    {
        var order = db.FnbOrders
            .Include(o => o.Cinema)
            .Include(o => o.FnbOrderItems)
            .ThenInclude(oi => oi.FnbItemVariant)
            .ThenInclude(v => v.FnbItem)
            .FirstOrDefault(o =>
                o.Id.ToString() == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.ExpiresAt > DateTime.Now
            );

        if (order == null)
        {
            return NotFound();
        }

        if (order.FnbOrderItems.Count == 0)
        {
            TempData["Message"] = "Your cart is empty.";
            return RedirectToAction("Menu", new { id });
        }

        var totalOrderPrice = order.FnbOrderItems.Sum(oi => oi.Quantity * oi.FnbItemVariant.Price);

        if (Request.Method == "POST")
        {
            order.Status = "Unpaid";
            order.ExpiresAt = null;

            var payment = new Payment
            {
                ExpiresAt = DateTime.Now.AddMinutes(7),
                Amount = totalOrderPrice,
                FnbOrderId = order.Id,
                AccountId = HttpContext.GetAccount()!.Id
            };
            db.Payments.Add(payment);
            db.SaveChanges();

            return RedirectToAction("Process", "Payment", new { id = payment.Id });
        }

        ViewBag.ExpiredTimestamp = new DateTimeOffset(order.ExpiresAt!.Value).ToUnixTimeMilliseconds();
        ViewBag.TotalOrderItems = order.FnbOrderItems.Sum(oi => oi.Quantity);
        ViewBag.TotalOrderPrice = totalOrderPrice;

        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = "Cancel F&B Orders")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        var order = db.FnbOrders.FirstOrDefault(o => o.Id.ToString() == id);
        if (order == null)
        {
            return BadRequest("Order not found");
        }

        if (account.AccountType.Name == "Customer")
        {
            if (order.AccountId != account.Id)
            {
                return Unauthorized("Unauthorized access");
            }
        }
        else if (account.CinemaId != null && account.CinemaId != order.CinemaId)
        {
            return Unauthorized("Unauthorized access");
        }

        if (order.Status != "Pending" && order.Status != "Unpaid" && order.Status != "Confirmed")
        {
            return BadRequest("Order cannot be canceled");
        }

        await fnbSrv.CancelFnbOrder(order.Id);

        TempData["Message"] = "Order canceled successfully";
        return Ok();
    }

    [Authorize(Roles = "Customer")]
    public IActionResult Info(string id)
    {
        var order = db.FnbOrders
            .Include(o => o.Payment)
            .Include(o => o.Cinema)
            .Include(o => o.Cinema.FnbInventories)
            .ThenInclude(i => i.FnbItemVariant)
            .ThenInclude(v => v.FnbItem)
            .ThenInclude(f => f.FnbCategory)
            .Include(o => o.FnbOrderItems)
            .ThenInclude(oi => oi.FnbItemVariant)
            .AsSplitQuery()
            .FirstOrDefault(o =>
                o.Id == id &&
                o.AccountId == HttpContext.GetAccount()!.Id &&
                o.Status != "Pending"
            );

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    [Authorize(Policy = "Manage F&B Orders")]
    public IActionResult Manage(ManageFnbOrderVM vm)
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

        Dictionary<string, Expression<Func<FnbOrder, object>>> sortOptions = new()
        {
            { "Id", o => o.Id },
            { "Customer Id", o => o.AccountId },
            { "Status", o => o.Status },
            { "Items Count", o => o.FnbOrderItems.Sum(oi => oi.Quantity) },
            { "Created At", o => o.CreatedAt },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.Last();
            vm.Dir = "desc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "id", Text = "Search By Id" },
            new() { Value = "customer_id", Text = "Search By Customer Id" },
        ];
        vm.AvailableStatuses = ["Pending", "Unpaid", "Confirmed", "Completed", "Canceled"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.FnbOrders
            .Include(o => o.FnbOrderItems)
            .Where(o => o.CinemaId == vm.CinemaId)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "id":
                    results = results.Where(o => o.Id.ToString().Contains(search));
                    break;
                case "customer_id":
                    results = results.Where(o => o.AccountId.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Statuses.Count > 0)
        {
            results = results.Where(o => vm.Statuses.Contains(o.Status));
        }

        if (vm.MinItemsCount != null && ModelState.IsValid("MinItemsCount"))
        {
            results = results.Where(o => o.FnbOrderItems.Sum(oi => oi.Quantity) >= vm.MinItemsCount);
        }

        if (vm.MaxItemsCount != null && ModelState.IsValid("MaxItemsCount"))
        {
            results = results.Where(o => o.FnbOrderItems.Sum(oi => oi.Quantity) <= vm.MaxItemsCount);
        }

        if (vm.CreationFrom != null && ModelState.IsValid("CreationFrom"))
        {
            results = results.Where(o => o.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null && ModelState.IsValid("CreationTo"))
        {
            results = results.Where(o => o.CreatedAt <= vm.CreationTo);
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

    [Authorize(Policy = "Manage F&B Orders")]
    public IActionResult Edit(string id)
    {
        var account = HttpContext.GetAccount()!;

        var order = db.FnbOrders
            .Include(o => o.Account)
            .Include(o => o.Payment)
            .Include(o => o.Cinema)
            .Include(o => o.Cinema.FnbInventories)
            .ThenInclude(i => i.FnbItemVariant)
            .ThenInclude(v => v.FnbItem)
            .ThenInclude(f => f.FnbCategory)
            .Include(o => o.FnbOrderItems)
            .ThenInclude(oi => oi.FnbItemVariant)
            .AsSplitQuery()
            .FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (account.CinemaId != null && account.CinemaId != order.CinemaId)
        {
            return Unauthorized();
        }

        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = "Manage F&B Orders")]
    public IActionResult Complete(string id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        var order = db.FnbOrders.FirstOrDefault(o => o.Id == id);

        if (order == null)
        {
            return NotFound("Order not found");
        }

        if (account.CinemaId != null && account.CinemaId != order.CinemaId)
        {
            return Unauthorized("Unauthorized access");
        }

        if (order.Status != "Confirmed")
        {
            return BadRequest("Order cannot be completed");
        }

        order.Status = "Completed";
        db.SaveChanges();

        TempData["Message"] = "Order completed successfully";
        return Ok();
    }
}
