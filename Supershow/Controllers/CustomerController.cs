using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage Customers")]
public class CustomerController : Controller
{
    private readonly DB db;
    private readonly IHubContext<AccountHub> accountHubContext;

    public CustomerController(DB db, IHubContext<AccountHub> accountHubContext)
    {
        this.db = db;
        this.accountHubContext = accountHubContext;
    }

    public IActionResult Manage(ManageCustomerVM vm)
    {
        Dictionary<string, Expression<Func<Account, object>>> sortOptions = new()
        {
            { "Id", a => a.Id },
            { "Name", a => a.Name },
            { "Email", a => a.Email },
            { "Status", a => a.IsBanned ? "Banned" : a.DeletionAt != null ? "ToDelete" : a.LockoutEnd != null ? "Timeout" : "Active" },
            { "Creation Date", a => a.CreatedAt }
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
        vm.AvailableStatuses = ["Active", "ToDelete", "Timeout", "Banned"];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.Accounts
            .AsQueryable()
            .Where(a =>
                a.AccountType.Name == "Customer" &&
                !a.IsDeleted
            );

        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search ?? "";

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

        if (vm.Statuses != null && vm.Statuses.Count > 0)
        {
            results = results.Where(a =>
                (vm.Statuses.Contains("Banned") && a.IsBanned) ||
                (vm.Statuses.Contains("ToDelete") && a.DeletionAt != null) ||
                (vm.Statuses.Contains("Timeout") && a.LockoutEnd != null) ||
                (vm.Statuses.Contains("Active") &&
                    !a.IsBanned &&
                    a.DeletionAt == null &&
                    !a.IsDeleted &&
                    a.LockoutEnd == null)
            );
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

    public IActionResult Edit(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound();
        }

        return View(account);
    }

    [HttpPost]
    public IActionResult Ban(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound("Customer not found");
        }

        if (account.IsBanned)
        {
            return BadRequest("This customer is already banned.");
        }

        account.IsBanned = true;
        db.SaveChanges();

        TempData["Message"] = "Banned successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult Unban(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound("Customer not found");
        }

        if (!account.IsBanned)
        {
            return BadRequest("This customer isn't banned.");
        }

        account.IsBanned = false;
        db.SaveChanges();

        TempData["Message"] = "Unbanned successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult RevokeDeletion(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound("Customer not found");
        }

        if (account.DeletionAt == null)
        {
            return BadRequest("This customer isn't scheduled for deletion.");
        }

        account.DeletionAt = null;
        db.SaveChanges();

        TempData["Message"] = "Revoke deletion successfully!";
        return Ok();
    }

    [HttpPost]
    public IActionResult RemoveTimeout(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound("Customer not found");
        }

        if (account.LockoutEnd == null)
        {
            return BadRequest("This customer doesn't have timeout.");
        }

        account.LockoutEnd = null;
        db.SaveChanges();

        TempData["Message"] = "Remove timeout successfully!";
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> LogoutAllDevices(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id && !a.IsDeleted && a.AccountType.Name == "Customer");
        if (account == null)
        {
            return NotFound("Customer not found");
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
}