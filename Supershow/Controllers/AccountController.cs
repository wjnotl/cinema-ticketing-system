using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly DeviceService devSrv;
    private readonly VerificationService verSrv;
    private readonly SecurityService secSrv;
    private readonly EmailService emlSrv;
    private readonly ImageService imgSrv;
    private readonly IHubContext<AccountHub> accountHubContext;

    public AccountController(DB db, IWebHostEnvironment en, DeviceService devSrv, VerificationService verSrv, SecurityService secSrv, EmailService emlSrv, ImageService imgSrv, IHubContext<AccountHub> accountHubContext)
    {
        this.db = db;
        this.en = en;
        this.devSrv = devSrv;
        this.verSrv = verSrv;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.imgSrv = imgSrv;
        this.accountHubContext = accountHubContext;
    }

    public IActionResult Index()
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        var vm = new AccountProfileVM
        {
            Name = acc.Name,
            Email = acc.Email,
            RemoveImage = false,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 250,
            PreviewHeight = 250
        };
        return View(vm);
    }

    [HttpPost]
    public IActionResult Index(AccountProfileVM vm)
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        if (vm.Image != null && !vm.RemoveImage)
        {
            var e = imgSrv.ValidateImage(vm.Image, 1);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid)
        {
            if (vm.RemoveImage)
            {
                // remove image
                if (acc.Image != null)
                {
                    imgSrv.DeleteImage(acc.Image, "account");
                    acc.Image = null;
                }
            }
            else if (vm.Image != null)
            {
                try
                {
                    var newFile = imgSrv.SaveImage(vm.Image, "account", 200, 200, vm.PreviewWidth, vm.PreviewHeight, vm.ImageX, vm.ImageY, vm.ImageScale);

                    // remove image
                    if (acc.Image != null) imgSrv.DeleteImage(acc.Image, "account");
                    acc.Image = newFile;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("Image", ex.Message);
                }
            }
        }

        if (ModelState.IsValid)
        {
            // update name
            acc.Name = vm.Name;
            db.SaveChanges();

            TempData["Message"] = "Account updated successfully";
            return RedirectToAction("Index");
        }

        vm.Email = acc.Email;
        return View(vm);
    }

    public IActionResult ChangePassword()
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM vm)
    {
        var acc = HttpContext.GetAccount();
        if (acc == null) return RedirectToAction("Index", "Home");

        if (!secSrv.VerifyPassword(acc.PasswordHash, vm.CurrentPassword))
        {
            ModelState.AddModelError("CurrentPassword", "Incorrect password");
        }

        if (ModelState.IsValid("NewPassword") && secSrv.VerifyPassword(acc.PasswordHash, vm.NewPassword))
        {
            ModelState.AddModelError("NewPassword", "Cannot use the same password as before.");
        }

        if (ModelState.IsValid)
        {
            await accountHubContext.Clients.All.SendAsync("LogoutAll", acc.Id);

            // Remove sessions
            var sessions = db.Sessions.Where(s => s.Device.AccountId == acc.Id);
            foreach (var session in sessions)
            {
                db.Sessions.Remove(session);
            }

            // Update password
            acc.PasswordHash = secSrv.HashPassword(vm.NewPassword);

            // Save changes
            db.SaveChanges();

            // Send email notification
            emlSrv.SendPasswordChangedEmail(acc, Url.Action("ForgotPassword", null, null, Request.Scheme, Request.Host.Value));

            TempData["Message"] = "Password reset successfully. Please login again";
            return RedirectToAction("Login", "Auth");
        }

        return View(vm);
    }

    public async Task<IActionResult> Device()
    {
        var deviceInfo = await devSrv.GetCurrentDeviceInfo();

        ViewBag.DeviceType = deviceInfo.Type;
        ViewBag.DeviceOS = deviceInfo.OS;
        ViewBag.DeviceBrowser = deviceInfo.Browser;
        ViewBag.DeviceAddress = deviceInfo.Location;

        var devices = db.Devices
                        .Where(d => d.AccountId.ToString() == User.Identity!.Name)
                        .Where(d => !(
                            d.DeviceOS == deviceInfo.OS
                            && d.DeviceType == deviceInfo.Type
                            && d.DeviceBrowser == deviceInfo.Browser
                            && d.Address == deviceInfo.Location
                        ))
                        .ToList();
        return View(devices);
    }

    [HttpPost]
    public async Task<IActionResult> LogoutDevice(int? id)
    {
        var device = db.Devices.FirstOrDefault(d => d.Id == id && d.AccountId.ToString() == User.Identity!.Name);
        if (device != null)
        {
            foreach (var session in db.Sessions.Where(s => s.DeviceId == device.Id))
            {
                await accountHubContext.Clients.All.SendAsync("Logout", session.Token);
            }

            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
            db.SaveChanges();
        }

        var deviceInfo = await devSrv.GetCurrentDeviceInfo();

        var devices = db.Devices
                        .Where(d => d.AccountId.ToString() == User.Identity!.Name)
                        .Where(d => !(
                            d.DeviceOS == deviceInfo.OS
                            && d.DeviceType == deviceInfo.Type
                            && d.DeviceBrowser == deviceInfo.Browser
                            && d.Address == deviceInfo.Location
                        ))
                        .ToList();
        return PartialView("_OtherDevice", devices);
    }

    [HttpPost]
    public async Task LogoutAllDevices()
    {
        var devices = db.Devices.Where(d => d.AccountId.ToString() == User.Identity!.Name).ToList();
        foreach (var device in devices)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
        }
        db.SaveChanges();

        await accountHubContext.Clients.All.SendAsync("LogoutAll", User.Identity!.Name);

        TempData["Message"] = "Logged out all known devices successfully";
    }

    [Authorize(Roles = "Customer")]
    public IActionResult History(HistoryVM vm)
    {
        vm.Options = new()
        {
            { "all", "All" },
            { "bookings", "Bookings" },
            { "fnborders", "F&B Orders" },
            { "pending", "Pending" },
            { "unpaid", "Unpaid" },
            { "confirmed", "Confirmed" },
            { "completed", "Completed" },
            { "canceled", "Canceled" }
        };

        if (string.IsNullOrEmpty(vm.Option) || !vm.Options.ContainsKey(vm.Option))
        {
            vm.Option = vm.Options.First().Key;
        }

        // Get query
        var bookings = vm.Option == "fnborders" ? null :
            db.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.Payment)
                .Where(b => b.AccountId == HttpContext.GetAccount()!.Id)
                .AsQueryable();
        var orders = vm.Option == "bookings" ? null :
            db.FnbOrders
                .Include(o => o.FnbOrderItems)
                .Include(o => o.Payment)
                .Where(o => o.AccountId == HttpContext.GetAccount()!.Id)
                .AsQueryable();

        if (vm.Option != "all" && vm.Option != "bookings" && vm.Option != "fnborders")
        {
            // Apply booking filters
            if (bookings != null)
            {
                bookings = bookings.Where(o => o.Status == vm.Options[vm.Option]);
            }

            // Apply order filters
            if (orders != null)
            {
                orders = orders.Where(o => o.Status == vm.Options[vm.Option]);
            }
        }

        // Get results
        vm.Results = [];
        if (bookings != null)
        {
            vm.Results.AddRange(bookings.ToList());
        }
        if (orders != null)
        {
            vm.Results.AddRange(orders.ToList());
        }

        // Sort by latest first
        vm.Results.Sort((x, y) =>
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            DateTime xDate = (x is Booking booking) ? booking.CreatedAt : ((FnbOrder)x).CreatedAt;
            DateTime yDate = (y is Booking booking1) ? booking1.CreatedAt : ((FnbOrder)y).CreatedAt;

            return yDate.CompareTo(xDate);
        });

        if (Request.IsAjax())
        {
            return PartialView("_History", vm.Results);
        }

        return View(vm);
    }

    [HttpPost]
    public string RequestChangeEmail()
    {
        // Create verification
        var verification = verSrv.CreateVerification("ChangeEmail", Request.GetBaseUrl(), HttpContext.GetAccount()!.Id);

        return verification.Token;
    }

    [HttpPost]
    public string RequestDeleteAccount()
    {
        // Create verification
        var verification = verSrv.CreateVerification("DeleteAccount", Request.GetBaseUrl(), HttpContext.GetAccount()!.Id);

        return verification.Token;
    }
}
