using LoginExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginExample.Models;
using LoginExample.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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
            var articles = await _context.Articles
                .Where(a => a.Status == ArticlesStatus.Published)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(articles);
        }

        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new ArticleCreateViewModel());
        }



        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ArticleCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
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
                CreatedAt = DateTime.UtcNow
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
                Content = article.Content
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ArticleEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
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

            article.Title = model.Title;
            article.Content = model.Content;
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

            article.Status = ArticlesStatus.Archived;
            article.ArchivedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

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
    }
}
