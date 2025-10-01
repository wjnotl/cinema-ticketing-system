using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage F&B Inventory")]
public class FnbInventoryController : Controller
{
    private readonly DB db;

    public FnbInventoryController(DB db)
    {
        this.db = db;
    }

    public IActionResult Manage(ManageFnbInventoryVM vm)
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

        Dictionary<string, Expression<Func<FnbInventory, object>>> sortOptions = new()
        {
            { "Variant Id", i => i.FnbItemVariantId },
            { "Item Id", i => i.FnbItemVariant.FnbItemId },
            { "Stock Count", i => i.Quantity },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "variant_id", Text = "Search By Variant Id" },
            new() { Value = "item_id", Text = "Search By Item Id" }
        ];

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.FnbInventories
            .Include(i => i.FnbItemVariant)
            .Where(i => i.CinemaId == vm.CinemaId && !i.Cinema.IsDeleted && !i.FnbItemVariant.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "variant_id":
                    results = results.Where(i => i.FnbItemVariantId.ToString().Contains(search));
                    break;
                case "item_id":
                    results = results.Where(i => i.FnbItemVariant.FnbItemId.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinStockCount != null && ModelState.IsValid("MinStockCount"))
        {
            results = results.Where(i => i.Quantity >= vm.MinStockCount);
        }

        if (vm.MaxStockCount != null && ModelState.IsValid("MaxStockCount"))
        {
            results = results.Where(i => i.Quantity <= vm.MaxStockCount);
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

    public IActionResult Add(int cinemaId)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != cinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == cinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        AddFnbInventoryVM vm = new()
        {
            CinemaId = cinemaId,
            AvailableVariants = db.FnbItemVariants
                .Where(v => !v.IsDeleted && !v.FnbInventories.Any(i => i.CinemaId == cinemaId))
                .Select(v => new SelectListItem
                {
                    Value = v.Id.ToString(),
                    Text = $"{v.Id} - {v.Name}"
                })
                .ToList()
        };

        if (vm.AvailableVariants.Count == 0)
        {
            TempData["Message"] = "All variants have already been added to this cinema's inventory.";
            return RedirectToAction("Manage", new { vm.CinemaId });
        }

        vm.VariantId = int.Parse(vm.AvailableVariants.First().Value);

        ViewBag.CinemaName = cinema.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Add(AddFnbInventoryVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var cinema = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted);
        if (cinema == null)
        {
            return NotFound();
        }

        var availableVariants = db.FnbItemVariants.Where(v => !v.IsDeleted && !v.FnbInventories.Any(i => i.CinemaId == vm.CinemaId));

        if (!availableVariants.Any())
        {
            TempData["Message"] = "All variants have already been added to this cinema's inventory.";
            return RedirectToAction("Manage", new { vm.CinemaId });
        }

        if (ModelState.IsValid("VariantId") && !availableVariants.Any(v => v.Id == vm.VariantId))
        {
            ModelState.AddModelError("VariantId", "This variant is invalid or already added to the inventory.");
        }

        if (ModelState.IsValid)
        {
            db.FnbInventories.Add(new()
            {
                CinemaId = vm.CinemaId,
                FnbItemVariantId = vm.VariantId,
                Quantity = vm.StockCount
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage", new { vm.CinemaId });
        }

        vm.AvailableVariants = availableVariants.Select(v => new SelectListItem
        {
            Value = v.Id.ToString(),
            Text = $"{v.Id} - {v.Name}"
        }).ToList();

        return View(vm);
    }

    public IActionResult Edit(int cinemaId, int variantId)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != cinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var inventory = db.FnbInventories.FirstOrDefault(i => i.CinemaId == cinemaId && i.FnbItemVariantId == variantId);
        if (inventory == null)
        {
            return NotFound();
        }

        EditFnbInventoryVM vm = new()
        {
            CinemaId = cinemaId,
            VariantId = variantId,
            StockCount = inventory.Quantity
        };

        ViewBag.CinemaName = db.Cinemas.FirstOrDefault(c => c.Id == cinemaId && !c.IsDeleted)?.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditFnbInventoryVM vm)
    {
        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != vm.CinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized();
        }

        var inventory = db.FnbInventories.FirstOrDefault(i => i.CinemaId == vm.CinemaId && i.FnbItemVariantId == vm.VariantId);
        if (inventory == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            inventory.Quantity = vm.StockCount;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { vm.CinemaId, vm.VariantId });
        }

        ViewBag.CinemaName = db.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId && !c.IsDeleted)?.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int cinemaId, int variantId)
    {
        if (!Request.IsAjax()) return NotFound();

        var account = HttpContext.GetAccount()!;

        if (account.CinemaId != null && account.CinemaId != cinemaId)
        {
            // Handle branch admin try to access other cinemas
            return Unauthorized("Unauthorized access");
        }

        var inventory = db.FnbInventories.FirstOrDefault(i => i.CinemaId == cinemaId && i.FnbItemVariantId == variantId);
        if (inventory == null)
        {
            return NotFound("Inventory item not found");
        }

        db.FnbInventories.Remove(inventory);
        db.SaveChanges();

        TempData["Message"] = "Inventory item deleted successfully";
        return Ok();
    }
}