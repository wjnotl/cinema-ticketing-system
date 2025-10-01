using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Supershow.Controllers;

[Authorize(Policy = "Manage F&B Items")]
public class FnbVariantController : Controller
{
    private readonly DB db;
    private readonly ImageService imgSrv;
    private readonly FnbOrderService fnbSvc;

    public FnbVariantController(DB db, ImageService imgSrv, FnbOrderService fnbSvc)
    {
        this.db = db;
        this.imgSrv = imgSrv;
        this.fnbSvc = fnbSvc;
    }

    public IActionResult Manage(ManageFnbVariantVM vm)
    {
        if (vm.ItemId == null)
        {
            return NotFound();
        }

        var item = db.FnbItems.FirstOrDefault(c => c.Id == vm.ItemId && !c.IsDeleted);
        if (item == null)
        {
            return NotFound();
        }

        Dictionary<string, Expression<Func<FnbItemVariant, object>>> sortOptions = new()
        {
            { "Id", v => v.Id },
            { "Name", v => v.Name },
            { "Price", v => v.Price },
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

        if (vm.SearchOption == null || !vm.AvailableSearchOptions.Any(o => o.Value == vm.SearchOption))
        {
            vm.SearchOption = vm.AvailableSearchOptions.First().Value;
        }

        var results = db.FnbItemVariants
            .Where(v => v.FnbItemId == vm.ItemId && !v.IsDeleted)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(vm.Search))
        {
            var search = vm.Search.Trim() ?? "";

            switch (vm.SearchOption)
            {
                case "name":
                    results = results.Where(v => v.Name.Contains(search));
                    break;
                case "id":
                    results = results.Where(v => v.Id.ToString().Contains(search));
                    break;
            }
        }

        // Filter
        if (vm.MinPrice != null && ModelState.IsValid("MinPrice"))
        {
            results = results.Where(v => v.Price >= vm.MinPrice);
        }

        if (vm.MaxPrice != null && ModelState.IsValid("MaxPrice"))
        {
            results = results.Where(v => v.Price <= vm.MaxPrice);
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

        ViewBag.FnbItemName = item.Name;

        return View(vm);
    }

    public IActionResult Add(int itemId)
    {
        var item = db.FnbItems.FirstOrDefault(c => c.Id == itemId && !c.IsDeleted);
        if (item == null)
        {
            return NotFound();
        }

        AddFnbVariantVM vm = new()
        {
            ItemId = itemId,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 250,
            PreviewHeight = 250
        };

        ViewBag.FnbItemName = item.Name;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Add(AddFnbVariantVM vm)
    {
        var item = db.FnbItems.FirstOrDefault(c => c.Id == vm.ItemId && !c.IsDeleted);
        if (item == null)
        {
            return NotFound();
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
            db.FnbItemVariants.Add(new()
            {
                FnbItemId = vm.ItemId,
                Name = vm.Name,
                Description = vm.Description,
                Price = vm.Price,
                Image = newFile
            });
            db.SaveChanges();

            TempData["Message"] = "Added successfully!";
            return RedirectToAction("Manage", new { vm.ItemId });
        }

        return View(vm);
    }

    public IActionResult Edit(int id)
    {
        var variant = db.FnbItemVariants
            .Include(i => i.FnbItem)
            .FirstOrDefault(v => v.Id == id && !v.IsDeleted);

        if (variant == null)
        {
            return NotFound();
        }

        EditFnbVariantVM vm = new()
        {
            Id = variant.Id,
            ItemId = variant.FnbItemId,
            Name = variant.Name,
            Description = variant.Description,
            Price = variant.Price,
            ImageScale = 1,
            ImageX = 0,
            ImageY = 0,
            PreviewWidth = 250,
            PreviewHeight = 250
        };

        ViewBag.FnbItemName = variant.FnbItem.Name;
        ViewBag.ImageUrl = variant.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Edit(EditFnbVariantVM vm)
    {
        var variant = db.FnbItemVariants
            .Include(i => i.FnbItem)
            .FirstOrDefault(v => v.Id == vm.Id && !v.IsDeleted);

        if (variant == null)
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
                if (variant.Image != null) imgSrv.DeleteImage(variant.Image, "fnb");
                variant.Image = newFile;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Image", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            variant.Name = vm.Name;
            variant.Description = vm.Description;
            variant.Price = vm.Price;
            db.SaveChanges();

            TempData["Message"] = "Updated successfully!";
            return RedirectToAction("Edit", new { id = vm.Id });
        }

        ViewBag.FnbItemName = variant.FnbItem.Name;
        ViewBag.ImageUrl = variant.Image;

        return View(vm);
    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        if (!Request.IsAjax()) return NotFound();

        var variant = db.FnbItemVariants.FirstOrDefault(v => v.Id == id && !v.IsDeleted);
        if (variant == null)
        {
            return NotFound("Variant not found");
        }

        if (fnbSvc.VariantHasActiveOrder(id))
        {
            return BadRequest("Cannot delete variant with active orders.");
        }
        
        // remove image
        if (variant.Image != null) imgSrv.DeleteImage(variant.Image, "fnb");

        // remove inventory
        db.FnbInventories.RemoveRange(db.FnbInventories.Where(i => i.FnbItemVariantId == id));

        // remove variant
        bool hasOrderItems = db.FnbOrderItems.Any(i => i.FnbItemVariantId == id);
        if (hasOrderItems)
        {
            variant.IsDeleted = true;
        }
        else
        {
            db.FnbItemVariants.Remove(variant);
        }
        db.SaveChanges();

        TempData["Message"] = "Variant deleted successfully!";
        return Ok();
    }
}
