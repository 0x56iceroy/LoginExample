using LoginExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LoginExample.Models;
using LoginExample.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace LoginExample.Controllers
{
    public class ArticlesController : Controller
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}

        private readonly ApplicationDbContext _context;

        public ArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var articles = await _context.Articles
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

            article.Title = model.Title;
            article.Content = model.Content;
            article.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = article.Id });
        }
    }
}
