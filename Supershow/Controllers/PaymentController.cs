using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using System.Globalization;

namespace Supershow.Controllers;

[Authorize(Roles = "Customer")]
public class PaymentController : Controller
{
    private readonly DB db;
    private readonly BookingService bookSrv;
    private readonly FnbOrderService fnbSrv;

    public PaymentController(DB db, BookingService bookSrv, FnbOrderService fnbSrv)
    {
        this.db = db;
        this.bookSrv = bookSrv;
        this.fnbSrv = fnbSrv;
    }

    public IActionResult Process(string id)
    {
        var payment = db.Payments.FirstOrDefault(p =>
            p.Id == id &&
            p.AccountId == HttpContext.GetAccount()!.Id &&
            (p.BookingId != null || p.FnbOrderId != null) &&
            p.ExpiresAt > DateTime.Now
        );
        if (payment == null)
        {
            return NotFound();
        }

        var vm = new PaymentVM
        {
            Option = "wallet",
            Amount = payment.Amount,
        };

        ViewBag.ExpiredTimestamp = new DateTimeOffset(payment.ExpiresAt!.Value).ToUnixTimeMilliseconds();

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Process(string id, PaymentVM vm)
    {
        var account = HttpContext.GetAccount()!;

        var payment = db.Payments
            .Include(p => p.Booking!)
                .ThenInclude(b => b.Tickets)
                    .ThenInclude(t => t.Seat)
            .Include(p => p.FnbOrder!)
                .ThenInclude(o => o.FnbOrderItems)
                    .ThenInclude(i => i.FnbItemVariant)
            .FirstOrDefault(p =>
                p.Id == id &&
                p.AccountId == account.Id &&
                p.ExpiresAt > DateTime.Now
            );
        if (payment == null)
        {
            return NotFound();
        }

        vm.Amount = payment.Amount;
        ViewBag.ExpiredTimestamp = new DateTimeOffset(payment.ExpiresAt!.Value).ToUnixTimeMilliseconds();

        if (ModelState.IsValid("Option"))
        {
            if (vm.Option == "wallet")
            {
                // Check if enough balance
                if (account.WalletBalance < payment.Amount)
                {
                    ModelState.AddModelError("Option", "Insufficient balance.");
                    return View(vm);
                }

                // Deduct from wallet
                account.WalletBalance -= payment.Amount;

                string transactionTitle = "";
                if (payment.BookingId != null)
                {
                    await bookSrv.ConfirmBooking(payment.BookingId, "Custom Wallet", null);
                    transactionTitle = $"Booking #{payment.BookingId} (Payment)";
                }
                else if (payment.FnbOrderId != null)
                {
                    fnbSrv.ConfirmFnbOrder(payment.FnbOrderId, "Custom Wallet", null);
                    transactionTitle = $"F&B Order #{payment.FnbOrderId} (Payment)";
                }

                // Add transaction
                db.WalletTransactions.Add(new()
                {
                    Amount = -payment.Amount,
                    Description = transactionTitle,
                    AccountId = account.Id,
                    PaymentId = payment.Id,
                });

                db.SaveChanges();

                return RedirectToAction("Success");
            }
            else
            {
                // If choose to pay with stripe
                var baseUrl = Request.GetBaseUrl();

                var options = new SessionCreateOptions
                {
                    SuccessUrl = baseUrl + "/Payment/Confirmation?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = baseUrl + "/Payment/Failed",
                    LineItems = [],
                    Mode = "payment",
                    PaymentMethodTypes = ["card", "fpx", "grabpay"],
                    CustomerEmail = HttpContext.GetAccount()!.Email,
                    Metadata = new()
                {
                    { "PaymentId", payment.Id }
                }
                };

                if (payment.Booking != null)
                {
                    foreach (var ticket in payment.Booking.Tickets)
                    {
                        options.LineItems.Add(new()
                        {
                            PriceData = new()
                            {
                                UnitAmount = (long)(ticket.Price * 100),
                                Currency = "myr",
                                ProductData = new()
                                {
                                    Name = ticket.Seat.Name,
                                },
                            },
                            Quantity = 1,
                        });
                    }
                }
                else if (payment.FnbOrder != null)
                {
                    foreach (var item in payment.FnbOrder.FnbOrderItems)
                    {
                        options.LineItems.Add(new()
                        {
                            PriceData = new()
                            {
                                UnitAmount = (long)(item.Price / item.Quantity * 100),
                                Currency = "myr",
                                ProductData = new()
                                {
                                    Name = item.FnbItemVariant.Name,
                                },
                            },
                            Quantity = item.Quantity,
                        });
                    }
                }

                var stripeSession = new SessionService().Create(options);
                TempData["stripeSessionId"] = stripeSession.Id;

                return Redirect(stripeSession.Url);
            }
        }

        return View(vm);
    }

    public async Task<IActionResult> Confirmation(string? session_id)
    {
        if (string.IsNullOrEmpty(session_id))
        {
            return NotFound();
        }

        var session = new SessionService().Get(session_id);
        if (session == null)
        {
            return NotFound();
        }

        var paymentId = session.Metadata.GetValueOrDefault("PaymentId");
        var isReloadWallet = session.Metadata.ContainsKey("Reload");

        if (!isReloadWallet && string.IsNullOrEmpty(paymentId))
        {
            return NotFound();
        }

        var isPaid = session.PaymentStatus == "paid";

        if (isPaid)
        {
            var paymentType = "";
            var cardBrand = "";
            var cardLast4 = "";
            var fpxBank = "";

            var paymentIntent = new PaymentIntentService().Get(session.PaymentIntentId);
            var paymentMethodId = paymentIntent.PaymentMethodId;
            if (!string.IsNullOrEmpty(paymentMethodId))
            {
                var paymentMethod = new PaymentMethodService().Get(paymentMethodId);

                paymentType = paymentMethod.Type;
                cardBrand = paymentMethod.Card?.Brand;
                cardLast4 = paymentMethod.Card?.Last4;
                fpxBank = paymentMethod.Fpx?.Bank;
            }

            var payment = string.IsNullOrEmpty(paymentId) ? null :
                db.Payments
                    .Include(p => p.Booking)
                    .Include(p => p.FnbOrder)
                    .FirstOrDefault(p => p.Id == paymentId);
            if (payment == null || isReloadWallet)
            {
                var account = HttpContext.GetAccount()!;

                // Add transaction
                db.WalletTransactions.Add(new()
                {
                    Amount = paymentIntent.Amount / 100m,
                    Description = "Reload Wallet",
                    CreatedAt = DateTime.Now,
                    AccountId = account.Id,
                });

                // Add account balance
                account.WalletBalance += paymentIntent.Amount / 100m;

                if (!isReloadWallet)
                {
                    TempData["Message"] = "Payment expired. Amount credited to your wallet as reload";
                }

                db.SaveChanges();
            }
            else
            {
                // Get payment type and details
                string paymentTypeStr = "";
                string? details = null;
                if (paymentType == "fpx")
                {
                    paymentTypeStr = "Bank Transfer";
                    details = fpxBank != null ? CultureInfo.CurrentCulture.TextInfo.ToTitleCase(fpxBank.Replace("_", " ")) : null;
                }
                else if (paymentType == "card")
                {
                    paymentTypeStr = "Card";
                    details = cardBrand != null && cardLast4 != null ? $"{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cardBrand)} •••• {cardLast4}" : null;
                }
                else if (paymentType == "grabpay")
                {
                    paymentTypeStr = "Grab Pay";
                }
                else
                {
                    paymentTypeStr = "Unknown";
                }

                // Confirm payment
                if (payment.BookingId != null)
                {
                    await bookSrv.ConfirmBooking(payment.BookingId, paymentTypeStr, details);
                }
                else if (payment.FnbOrder != null)
                {
                    fnbSrv.ConfirmFnbOrder(payment.FnbOrderId!, paymentTypeStr, details);
                }
            }

            if (isReloadWallet)
            {
                TempData["Message"] = "Reload successful";
                return RedirectToAction("Index", "Wallet");
            }
        }

        return RedirectToAction(isPaid ? "Success" : "Failed");
    }

    public IActionResult Success()
    {
        return View("Status", "success");
    }

    public IActionResult Failed()
    {
        return View("Status", "failed");
    }

    // ===============REMOTE METHODS===============
    [Authorize(Roles = "Customer")]
    public bool CheckInsufficientBalance(decimal Amount, string Option)
    {
        var account = HttpContext.GetAccount();

        if (account == null) return false;

        if (Option != "wallet") return true;

        return account.WalletBalance >= Amount;
    }
}

