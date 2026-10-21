using LoginExample.Data;
using LoginExample.ViewModels;
using LoginExample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Components.Forms;
using System.Runtime.CompilerServices;

namespace LoginExample.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CategoriesController (ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new CategoryManagementViewModel();
            model.Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryIndexViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    ArticleCount = c.Articles.Count()
                })
                .ToListAsync();

            return View(model);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult Create([Bind(Prefix = "Create")] CategoryCreateViewModel model)
        //{
        //    return View(new CategoryCreateViewModel());
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind(Prefix = "Create")] CategoryCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var managementModel = await BuildManagementViewModelAsync(model);
                return View("Index", managementModel);
            }

            bool categoryExists = await _context.Categories
                .AnyAsync(c => c.Name == model.Name);

            if (categoryExists)
            {
                ModelState.AddModelError(
                    "Create.Name", "分類名稱已存在");

                var managementModel = await BuildManagementViewModelAsync(model);
                return View("Index", managementModel);
            }

            var category = new Category
            {
                Name = model.Name
            };

            _context.Categories.Add(category);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627)
            {
                ModelState.AddModelError(
                    "Create.Name", "分類名稱已存在");

                var managementModel = await BuildManagementViewModelAsync(model);
                return View("Index", managementModel);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<CategoryManagementViewModel> BuildManagementViewModelAsync(CategoryCreateViewModel createModel)
        {
            var model = new CategoryManagementViewModel
            {
                Create = createModel
            };

            model.Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryIndexViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    ArticleCount = c.Articles.Count()
                })
                .ToListAsync();
            return model;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            if (id == 1)
            {
                return Forbid();
            }

            var category = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CategoryEditViewModel
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .FirstOrDefaultAsync();

            if (category is null)
            {
                return NotFound();
            }

            return View(category);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryEditViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (id == 1)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category is null)
            {
                return NotFound();
            }

            bool nameExists = await _context.Categories.AnyAsync(c => c.Name == model.Name && c.Id != id);

            if (nameExists)
            {
                ModelState.AddModelError(nameof(model.Name),
                    "分類名稱已存在");
                return View(model);
            }

            category.Name = model.Name;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
                when(ex.InnerException is SqlException sqlEx && sqlEx.Number is 2601 or 2627)
            {
                ModelState.AddModelError(nameof(model.Name), "分類名稱已存在");
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id == 1)
            {
                return Forbid();
            }

            var category = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new CategoryIndexViewModel
                {
                    Id = c.Id,
                    Name = c.Name,
                    ArticleCount = c.Articles.Count()
                })
                .FirstOrDefaultAsync();

            if (category is null)
            {
                return NotFound();
            }

            return View(category);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[ActionName("Delete")]
        //public async Task<IActionResult> DeleteConfirmed(int id)
        //{

        //}
    }
}
