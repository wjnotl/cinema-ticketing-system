using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace Supershow.Controllers;

[Authorize(Roles = "Customer")]
public class WalletController : Controller
{
    private readonly DB db;

    public WalletController(DB db)
    {
        this.db = db;
    }

    public IActionResult Index(WalletVM vm)
    {
        var account = db.Accounts
            .Include(a => a.WalletTransactions)
            .ThenInclude(t => t.Payment)
            .FirstOrDefault(a => a.Id == HttpContext.GetAccount()!.Id && !a.IsDeleted);

        if (account == null)
        {
            return NotFound();
        }

        vm.Options = new()
        {
            { "all", "All" },
            { "today", "Today" },
            { "yesterday", "Yesterday" },
            { "this-week", "This Week" },
            { "this-month", "This Month" },
            { "last-month", "Last Month" }
        };
        vm.Balance = account.WalletBalance;

        if (string.IsNullOrEmpty(vm.Option) || !vm.Options.ContainsKey(vm.Option))
        {
            vm.Option = vm.Options.First().Key;
        }

        var results = account.WalletTransactions.AsQueryable();

        // Apply filters
        switch (vm.Option)
        {
            case "today":
                results = results.Where(wt => wt.CreatedAt.Date == DateTime.Today);
                break;

            case "yesterday":
                results = results.Where(wt => wt.CreatedAt.Date == DateTime.Today.AddDays(-1));
                break;

            case "this-week":
                var today = DateTime.Today;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
                var endOfWeek = startOfWeek.AddDays(7);

                results = results.Where(wt => wt.CreatedAt >= startOfWeek && wt.CreatedAt < endOfWeek);
                break;

            case "this-month":
                results = results.Where(wt => wt.CreatedAt.Year == DateTime.Now.Year && wt.CreatedAt.Month == DateTime.Now.Month);
                break;

            case "last-month":
                var lastMonth = DateTime.Now.AddMonths(-1);

                results = results.Where(wt => wt.CreatedAt.Year == lastMonth.Year && wt.CreatedAt.Month == lastMonth.Month);
                break;
        }

        vm.Results = results.OrderByDescending(wt => wt.CreatedAt).ToList();

        if (Request.IsAjax())
        {
            return PartialView("_Transactions", vm.Results);
        }

        return View(vm);
    }

    public IActionResult Reload()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Reload(ReloadVM vm)
    {
        if (ModelState.IsValid)
        {
            var baseUrl = Request.GetBaseUrl();

            var options = new SessionCreateOptions
            {
                SuccessUrl = baseUrl + "/Payment/Confirmation?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = baseUrl + "/Payment/Failed",
                LineItems = [
                    new()
                    {
                        PriceData = new() {
                            UnitAmountDecimal = vm.Amount * 100m,
                            Currency = "myr",
                            ProductData = new()
                            {
                                Name = "Reload Wallet"
                            }
                        },
                        Quantity = 1
                    },
                ],
                Mode = "payment",
                PaymentMethodTypes = ["card", "fpx", "grabpay"],
                CustomerEmail = HttpContext.GetAccount()!.Email,
                Metadata = new() {
                    { "Reload", "true"}
                }
            };

            var stripeSession = new SessionService().Create(options);
            TempData["stripeSessionId"] = stripeSession.Id;

            return Redirect(stripeSession.Url);
        }

        return View(vm);
    }
}

