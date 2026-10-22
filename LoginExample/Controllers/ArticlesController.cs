using LoginExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginExample.Models;
using LoginExample.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace LoginExample.Controllers
{
    public class ArticlesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var articles = await (
                from article in _context.Articles.AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on article.AuthorId equals user.Id into authors
                from author in authors.DefaultIfEmpty()
                where article.Status == ArticlesStatus.Published
                orderby article.CreatedAt descending
                select new ArticleListItemViewModel
                {
                    Id = article.Id,
                    Title = article.Title,
                    CreatedAt = article.CreatedAt,
                    AuthorId = article.AuthorId,
                    AuthorName = author != null
                        ? (author.UserName ?? "未知作者")
                        : "未知作者"
                }).ToListAsync();

            return View(articles);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ArticleCreateViewModel();

            await LoadCategoriesAsync(model);

            return View(model);
        }



        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ArticleCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model);
                return View(model);
            }

            bool categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == model.CategoryId && !c.IsDeleted);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(model.CategoryId),
                    "指定的文章分類不存在或已被刪除");
                await LoadCategoriesAsync(model);
                return View(model);
            }

            var authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (authorId is null)
            {
                return Challenge();
            }


            var article = new Article
            {
                Title = model.Title,
                Content = model.Content,
                AuthorId = authorId,
                CreatedAt = DateTime.UtcNow,
                CategoryId = model.CategoryId,
            };

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var article = await _context.Articles
                .AsNoTracking()
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (article is null)
            {
                return NotFound();
            }

            return View(article);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var article = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article is null || article.Id == 0)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
            {
                return Forbid();
            }

            if (article.Status != ArticlesStatus.Published)
                return BadRequest("Archived articles cannot be edit.");

            var model = new ArticleEditViewModel
            {
                Title = article.Title,
                Content = article.Content,
                CategoryId = article.CategoryId,
            };

            model.Categories = await GetCategoryOptionsAsync();

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ArticleEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Categories = await GetCategoryOptionsAsync();
                return View(model);
            }

            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Id == id);

            if (article is null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
            {
                return Forbid();
            }

            if (article.Status != ArticlesStatus.Published)
                return BadRequest("Archived articles cannot be edit.");

            bool categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == model.CategoryId && !c.IsDeleted);

            if (!categoryExists)
            {
                ModelState.AddModelError(nameof(model.CategoryId), "指定的文章分類不存在或已被刪除");
                model.Categories = await GetCategoryOptionsAsync();
                return View(model);
            }

            article.Title = model.Title;
            article.Content = model.Content;
            article.CategoryId = model.CategoryId;
            article.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = article.Id });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Archive(int id)
        {
            var article = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
            if (article is null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
                return Forbid();

            if (article.Status != ArticlesStatus.Published)
                return BadRequest("Article is already archived.");

            return View(article);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Archive")]
        public async Task<IActionResult> ArchiveConfirmed(int id)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article is null)
                return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
                return Forbid();

            if (article.Status != ArticlesStatus.Published)
                return BadRequest("Only published articles can be archived.");

            var now = DateTime.UtcNow;

            article.Status = ArticlesStatus.Archived;
            article.ArchivedAt = now;
            article.UpdatedAt = now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Archived()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var articles = await _context.Articles
                .AsNoTracking()
                .Where(a =>
                    a.AuthorId == currentUserId &&
                    a.Status == ArticlesStatus.Archived)
                .OrderByDescending(a => a.ArchivedAt)
                .ToListAsync();

            return View(articles);
        }
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article is null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
                return Forbid();

            if (article.Status != ArticlesStatus.Archived)
            {
                return BadRequest("Only archived articles can be restored.");
            }

            DateTime now = DateTime.UtcNow;

            article.Status = ArticlesStatus.Published;
            article.ArchivedAt = null;
            article.UpdatedAt = now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Archived));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _context.Articles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article is null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
                return Forbid();

            if (article.Status != ArticlesStatus.Archived)
            {
                return BadRequest("Only archived articles can be delete.");
            }

            return View(article);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var article = await _context.Articles
                .FirstOrDefaultAsync(a => a.Id == id);
            if (article is null)
                return NotFound();
            
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (article.AuthorId != currentUserId)
                return Forbid();

            if (article.Status != ArticlesStatus.Archived)
                return BadRequest("Only archived articles can be deleted.");

            _context.Articles.Remove(article);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Archived));

        }

        private async Task LoadCategoriesAsync(ArticleCreateViewModel model)
        {
            model.Categories = await GetCategoryOptionsAsync();
        }

        private async Task<List<SelectListItem>> GetCategoryOptionsAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }
    }
}
