using LoginExample.Data;
using LoginExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace LoginExample.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }







        public async Task<IActionResult> linq1()
        {
            var articles = await _context.Articles
                .Where(a => a.Status == ArticlesStatus.Published && EF.Functions.Like(a.Title, "%ASP.NET%"))
                .Select(a => new
                {
                    a.Id, a.Title, a.CreatedAt
                })
                .Take(10)
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            return Json(articles);
        }

        public async Task<IActionResult> linq2()
        {
            var result = await _context.Articles
                .Where(a => a.Status == ArticlesStatus.Published)
                .GroupBy(a => a.AuthorId)
                .Where(g => g.Count() >= 2)
                .Select(g => new
                {
                    AuthorId = g.Key,
                    ArticleCount = g.Count()
                })
                .OrderByDescending(x => x.ArticleCount)
                .ToListAsync();
            return Json(result);
        }

        public async Task<IActionResult> linq3()
        {
            var query = _context.Users
                .GroupJoin(
                    _context.Articles,
                    user => user.Id,
                    article => article.AuthorId,
                    (user, articles) => new
                    {
                        user.Id,
                        user.UserName,
                        ArticleCount = articles.Count()
                    }
                )
                .OrderByDescending(x => x.ArticleCount);
                
            //下中斷點可以取得SQL指令碼發現效能問題
            string sql = query.ToQueryString();

            var result = await query.ToListAsync();
            return Json(result);
        }

        public async Task<IActionResult> linq4()
        {
            var query = _context.Users
                .Where(u => _context.Articles.Any(a =>
                    a.AuthorId == u.Id &&
                    a.Status == ArticlesStatus.Published))
                .Select(u => new
                {
                    u.Id, u.UserName,
                });
            string sql = query.ToQueryString();
            var result = await query.ToListAsync();

            return Json(result);
        }

        public async Task<IActionResult> linq5()
        {
            var query = _context.Users
                .Where(u => !_context.Articles.Any(a => a.AuthorId != u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                });

            string sql = query.ToQueryString();
            var result = await query.ToListAsync();

            return Json(result);            
        }

        public async Task<IActionResult> linq6()
        {
            var query = _context.Users
                .Where(u => !_context.Articles.Any(
                    a => a.Status == ArticlesStatus.Published &&
                    a.AuthorId == u.Id));
            string sql = query.ToQueryString();
            var result = await query.ToListAsync();
            return Json(result);
        }

        public async Task<IActionResult> linq7()
        {
            string[] authorIds =
                {
                    "user-001",
                    "user-002",
                    "user-003"
                };
            var query = _context.Articles
                .Where(a => authorIds.Contains(a.AuthorId));

            string sql = query.ToQueryString();
            var result = await query.ToListAsync();
            return Json(result);

        }

        public async Task<IActionResult> linq8()
        {
            string[] authorIds = { "user-001", "user-002" };
            var query = _context.Articles
                .Where(a => a.Status == ArticlesStatus.Published && 
                !authorIds.Contains(a.AuthorId));

            string sql = query.ToQueryString();
            var result = await query.ToListAsync();
            return Json(result);
        }

        public async Task<IActionResult> linq9()
        {
            var u = _context.Users
                .Where(u => u.EmailConfirmed == true)
                .Select(u => u.Id);

            var a = _context.Articles
                .Where(a => u.Contains(a.AuthorId))
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.AuthorId
                });

            string sql = a.ToQueryString();
            var result = await a.ToListAsync();
            return Json(result);
        }
    }
}
