using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

public class FnbController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly FnbOrderService fnbSrv;

    public FnbController(DB db, ImageService imgSrv, FnbOrderService fnbSrv)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.fnbSrv = fnbSrv;
    }

    public IActionResult Index(int? cinemaId)
    {
        if (cinemaId == null)
        {
            var cinemas = db.Cinemas
            .Where(c => c.FnbInventories.Any() && !c.IsDeleted)
            .GroupBy(c => c.State)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Name).ToList()
            );
            return View("_SelectCinemas", cinemas);
        }

        return RedirectToAction("Create", "FnbOrder", new { id = cinemaId });
    }

    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Manage(ManageFnbItemVM vm)
    {
        Dictionary<string, Expression<Func<FnbItem, object>>> sortOptions = new()
        {
            { "Id", f => f.Id },
            { "Name", f => f.Name },
            { "Category", f => f.FnbCategory.Name },
            { "Variants Count", f => f.FnbItemVariants.Count(v => !v.IsDeleted) },
            { "Creation Date", f => f.CreatedAt },
        };

        ViewBag.Fields = sortOptions.Keys.ToList();

        if (vm.Sort == null || !sortOptions.ContainsKey(vm.Sort) || (vm.Dir != "asc" && vm.Dir != "desc"))
        {
            vm.Sort = sortOptions.Keys.First();
            vm.Dir = "asc";
        }

        vm.AvailableSearchOptions = [
            new() { Value = "name", Text = "Search By Name" },
            new() { Value = "id", Text = "Search By Id" }
        ];
        vm.AvailableCategories = db.FnbCategories.Select(f => f.Name).ToList();

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.FnbItems
            .Include(f => f.FnbCategory)
            .Include(f => f.FnbItemVariants)
            .Where(f => !f.IsDeleted)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(f => f.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(f => f.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.Categories.Count > 0)
        {
            results = results.Where(f => vm.Categories.Contains(f.FnbCategory.Name));
        }

        if (vm.MinVariantsCount != null && ModelState.IsValid("MinVariantsCount"))
        {
            results = results.Where(f => f.FnbItemVariants.Count(v => !v.IsDeleted) >= vm.MinVariantsCount);
        }

        if (vm.MaxVariantsCount != null && ModelState.IsValid("MaxVariantsCount"))
        {
            results = results.Where(f => f.FnbItemVariants.Count(v => !v.IsDeleted) <= vm.MaxVariantsCount);
        }

        if (vm.CreationFrom != null && ModelState.IsValid("CreationDateFrom"))
        {
            results = results.Where(f => f.CreatedAt >= vm.CreationFrom);
        }

        if (vm.CreationTo != null && ModelState.IsValid("CreationDateTo"))
        {
            results = results.Where(f => f.CreatedAt <= vm.CreationTo);
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

        return View(vm);
    }

    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Add()
    {
        AddFnbItemVM vm = new()
        {
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 250,
            PreviewHeight = 250,
            AvailableCategories = db.FnbCategories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };
        vm.FnbCategory = int.Parse(vm.AvailableCategories.First().Value);

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Add(AddFnbItemVM vm)
    {
        var availableCategories = db.FnbCategories;
        if (ModelState.IsValid("FnbCategory") && !availableCategories.Any(c => c.Id == vm.FnbCategory))
        {
            ModelState.AddModelError("FnbCategory", "Invalid category.");
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 1);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        string? newFile = null;
        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                newFile = imgSrv.SaveImage(vm.Image, "fnb", 500, 500, vm.PreviewWidth, vm.PreviewHeight, vm.ImageX, vm.ImageY, vm.ImageScale);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid && newFile != null)
        {
            db.FnbItems.Add(new()
            {
                Name = vm.Name,
                FnbCategoryId = vm.FnbCategory,
                Image = newFile
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage");
        }

        vm.AvailableCategories = availableCategories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList();

        return View(vm);
    }

    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Edit(int id)
    {
        var item = db.FnbItems.FirstOrDefault(i => i.Id == id && !i.IsDeleted);
        if (item == null)
        {
            return NotFound();
        }

        EditFnbItemVM vm = new()
        {
            Id = item.Id,
            CreatedAt = item.CreatedAt,
            Name = item.Name,
            FnbCategory = item.FnbCategoryId,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 250,
            PreviewHeight = 250,
            AvailableCategories = db.FnbCategories.Select(f => new SelectListItem { Value = f.Id.ToString(), Text = f.Name }).ToList()
        };

        ViewBag.ImageUrl = item.Image;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Edit(EditFnbItemVM vm)
    {
        var item = db.FnbItems.FirstOrDefault(i => i.Id == vm.Id && !i.IsDeleted);
        if (item == null)
        {
            return NotFound();
        }

        if (vm.Image != null)
        {
            var e = imgSrv.ValidateImage(vm.Image, 1);
            if (e != "") ModelState.AddModelError("Image", e);
        }

        if (ModelState.IsValid && vm.Image != null)
        {
            try
            {
                var newFile = imgSrv.SaveImage(vm.Image, "fnb", 500, 500, vm.PreviewWidth, vm.PreviewHeight, vm.ImageX, vm.ImageY, vm.ImageScale);

                // remove image
                if (item.Image != null) imgSrv.DeleteImage(item.Image, "fnb");
                item.Image = newFile;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            item.Name = vm.Name;
            item.FnbCategoryId = vm.FnbCategory;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        ViewBag.ImageUrl = item.Image;

        return View(vm);
    }

    [HttpPost]
    [Authorize(Policy = "Manage F&B Items")]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var item = db.FnbItems.FirstOrDefault(i => i.Id == id && !i.IsDeleted);
        if (item == null)
        {
            return NotFound("Item not found");
        }

        if (fnbSrv.ItemHasActiveOrder(id))
        {
            return BadRequest("Cannot delete item with active orders.");
        }

        if (item.Image != null) imgSrv.DeleteImage(item.Image, "fnb");

        // remove inventory
        db.FnbInventories.RemoveRange(db.FnbInventories.Where(i => i.FnbItemVariant.FnbItemId == item.Id));

        // delete each variant
        foreach (var variant in item.FnbItemVariants.Where(v => !v.IsDeleted))
        {
            // remove image
            if (variant.Image != null)
                imgSrv.DeleteImage(variant.Image, "fnb");

            // remove variant
            bool hasOrderItems = db.FnbOrderItems.Any(oi => oi.FnbItemVariantId == variant.Id);
            if (hasOrderItems)
            {
                variant.IsDeleted = true;
            }
            else
            {
                db.FnbItemVariants.Remove(variant);
            }
        }

        // remove item
        bool hasVariantsWithOrders = db.FnbOrderItems.Any(oi => oi.FnbItemVariant.FnbItemId == item.Id);
        if (hasVariantsWithOrders)
        {
            item.IsDeleted = true;
        }
        else
        {
            db.FnbItems.Remove(item);
        }
        db.SaveChanges();

        TempData["Message"] = "Item deleted successfully!";
        return Ok();
    }
}
