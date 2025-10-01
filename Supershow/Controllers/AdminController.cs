using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage Admins")]
public class AdminController : Controller
{
    private readonly DB db;
    private readonly SecurityService secSrv;
    private readonly EmailService emlSrv;
    private readonly IHubContext<AccountHub> accountHubContext;

    public AdminController(DB db, SecurityService secSrv, EmailService emlSrv, IHubContext<AccountHub> accountHubContext)
    {
        this.db = db;
        this.secSrv = secSrv;
        this.emlSrv = emlSrv;
        this.accountHubContext = accountHubContext;
    }

    public IActionResult Manage(ManageAdminVM vm)
    {
        Dictionary<string, Expression<Func<Account, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Email", a => a.Email },
            { "Admin Type", a => a.AccountType.Name },
            { "Creation Date", a => a.CreatedAt },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "email", Text = "Search By Email" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer").Select(x => x.Name).ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Accounts
            .Include(a => a.AccountType)
            .AsQueryable()
            .Where(a => a.AccountType.Name != "Customer" && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(a => a.Name.Contains(search));
                    break;
                case "email":
                    results = results.Where(a => a.Email.Contains(search));
                    break;
                case "id":
                    results = results.Where(a => a.Id.ToString().Contains(search));
                    break;
            }
        }

        if (vm.AdminTypes.Count > 0)
        {
            results = results.Where(a => vm.AdminTypes.Contains(a.AccountType.Name));
        }

        if (vm.CreationFrom != null && ModelState.IsValid("CreationFrom"))
        {
            results = results.Where(a => a.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null && ModelState.IsValid("CreationTo"))
        {
            results = results.Where(a => a.CreatedAt <= vm.CreationTo);
        }

        results = vm.Dir == "asc"
            ? results.OrderBy(sortOptions[vm.Sort])
            : results.OrderByDescending(sortOptions[vm.Sort]);

        vm.Results = results.ToPagedList(vm.Page, 10);

        if (Request.IsAjax())
        {
            return PartialView("_Manage", vm);
        }

        return View(vm);
    }

    public IActionResult Add()
    {
        AddAdminVM vm = new()
        {
            AvailableAdminTypes = db.AccountTypes
                .Where(at => at.Name != "Customer")
                .Select(at => new SelectListItem { Text = at.Name, Value = at.Id.ToString() })
                .ToList(),
            AvailableHQAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer" && at.IsHQStaff).Select(at => at.Id).ToList(),
            AvailableCinemas = db.Cinemas.Where(c => !c.IsDeleted).Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList(),
        };
        vm.AdminType = int.Parse(vm.AvailableAdminTypes.First().Value);
        vm.CinemaBranch = int.Parse(vm.AvailableCinemas.First().Value);

        return View(vm);
    }

    [HttpPost]
    public IActionResult Add(AddAdminVM vm)
    {
        var avaialbleAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer");
        var availableCinemas = db.Cinemas.Where(c => !c.IsDeleted);

        vm.AvailableAdminTypes = avaialbleAdminTypes.Select(at => new SelectListItem { Text = at.Name, Value = at.Id.ToString() }).ToList();
        vm.AvailableCinemas = availableCinemas.Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList();
        vm.AvailableHQAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer" && at.IsHQStaff).Select(at => at.Id).ToList();

        if (ModelState.IsValid("Email") && CheckEmailExist(vm.Email))
        {
            ModelState.AddModelError("Email", "Email already registered.");
        }

        if (ModelState.IsValid("AdminType") && !avaialbleAdminTypes.Any(at => at.Id == vm.AdminType))
        {
            ModelState.AddModelError("AdminType", "Invalid admin type.");
        }

        if (ModelState.IsValid("CinemaBranch") && vm.CinemaBranch != null && !availableCinemas.Any(c => c.Id == vm.CinemaBranch))
        {
            ModelState.AddModelError("CinemaBranch", "Invalid cinema branch.");
        }

        if (ModelState.IsValid && vm.CinemaBranch == null && !vm.AvailableHQAdminTypes.Contains(vm.AdminType))
        {
            ModelState.AddModelError("CinemaBranch", "This admin must be assigned to a cinema.");
        }

        string password = GeneratorService.RandomString(15);

        if (ModelState.IsValid)
        {
            Account account = new()
            {
                Name = vm.Name,
                Email = vm.Email,
                PasswordHash = secSrv.HashPassword(password),
                AccountTypeId = vm.AdminType,
                CinemaId = vm.CinemaBranch,
            };
            db.Accounts.Add(account);
            db.SaveChanges();

            emlSrv.SendAccountCreatedEmail(account, password, Url.Action("Login", "Auth", null, Request.Scheme, Request.Host.Value));

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage");
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var account = db.Accounts
            .Include(a => a.AccountType)
            .FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name != "Customer");
        if (account == null)
        {
            return NotFound();
        }

        EditAdminVM vm = new()
        {
            Id = account.Id,
            Account = account,
            AdminType = account.AccountTypeId,
            CinemaBranch = account.CinemaId,
            AvailableAdminTypes = db.AccountTypes
                .Where(at => at.Name != "Customer")
                .Select(at => new SelectListItem { Text = at.Name, Value = at.Id.ToString() })
                .ToList(),
            AvailableHQAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer" && at.IsHQStaff).Select(at => at.Id).ToList(),
            AvailableCinemas = db.Cinemas.Where(c => !c.IsDeleted).Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList(),
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditAdminVM vm)
    {
        var account = db.Accounts
            .Include(a => a.AccountType)
            .FirstOrDefault(a => a.Id == vm.Id && !a.IsDeleted && a.AccountType.Name != "Customer" && a.Email != "malaysiasupershow@gmail.com");
        if (account == null)
        {
            return NotFound();
        }

        var avaialbleAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer");
        var availableCinemas = db.Cinemas.Where(c => !c.IsDeleted);

        vm.Account = account;
        vm.AvailableAdminTypes = avaialbleAdminTypes.Select(at => new SelectListItem { Text = at.Name, Value = at.Id.ToString() }).ToList();
        vm.AvailableCinemas = availableCinemas.Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToList();
        vm.AvailableHQAdminTypes = db.AccountTypes.Where(at => at.Name != "Customer" && at.IsHQStaff).Select(at => at.Id).ToList();

        if (ModelState.IsValid("AdminType") && !avaialbleAdminTypes.Any(at => at.Id == vm.AdminType))
        {
            ModelState.AddModelError("AdminType", "Invalid admin type.");
        }

        if (ModelState.IsValid("CinemaBranch") && vm.CinemaBranch != null && !availableCinemas.Any(c => c.Id == vm.CinemaBranch))
        {
            ModelState.AddModelError("CinemaBranch", "Invalid cinema branch.");
        }

        if (ModelState.IsValid && vm.CinemaBranch == null && !vm.AvailableHQAdminTypes.Contains(account.AccountTypeId))
        {
            ModelState.AddModelError("CinemaBranch", "This admin must be assigned to a cinema.");
        }

        if (ModelState.IsValid)
        {
            account.AccountTypeId = vm.AdminType;
            if (vm.AvailableHQAdminTypes.Contains(account.AccountTypeId))
            {
                account.CinemaId = null;
            }
            else
            {
                account.CinemaId = vm.CinemaBranch;
            }
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts
           .Include(a => a.AccountType)
           .FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name != "Customer" && a.Email != "malaysiasuperme@gmail.com");
        if (account == null)
        {
            return NotFound("Admin not found");
        }

        await accountHubContext.Clients.All.SendAsync("LogoutAll", account.Id);

        // Remove sessions
        var sessions = db.Sessions.Where(s => s.Device.AccountId == account.Id);
        foreach (var session in sessions)
        {
            db.Sessions.Remove(session);
        }

        // Remove devices
        var devices = db.Devices.Where(d => d.AccountId == account.Id);
        foreach (var device in devices)
        {
            db.Devices.Remove(device);
        }

        db.Accounts.Remove(account);
        db.SaveChanges();

        TempData["Message"] = "Admin removed successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult RemoveTimeout(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name != "Customer");
        if (account == null)
        {
            return NotFound("Admin not found");
        }

        if (account.LockoutEnd == null)
        {
            return BadRequest("This Admin doesn't have timeout.");
        }

        account.LockoutEnd = null;
        db.SaveChanges();

        TempData["Message"] = "Remove timeout successfully!";
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> LogoutAllDevices(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name != "Customer");
        if (account == null)
        {
            return NotFound("Admin not found");
        }

        var devices = db.Devices.Where(d => d.AccountId == account.Id).ToList();
        foreach (var device in devices)
        {
            db.Verifications.RemoveRange(db.Verifications.Where(v => v.DeviceId == device.Id));
            db.Devices.Remove(device);
        }
        db.SaveChanges();

        await accountHubContext.Clients.All.SendAsync("LogoutAll", account.Id);

        TempData["Message"] = "Logged out all known devices successfully";
        return Ok();
    }

    // ======================REMOTE METHODS======================
    public bool CheckEmailExist(string email)
    {
        return db.Accounts.Any(a => a.Email == email && !a.IsDeleted);
    }
}