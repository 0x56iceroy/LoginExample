using Microsoft.AspNetCore.Identity;
using LoginExample.Data;
using LoginExample.Models;
using LoginExample.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;

namespace LoginExample.Tests.Categories
{
    public class CategoryAuthorizationTests
    {
        [Fact]
        public async Task Delete_AnonymousUser_ShouldRedirectToLogin()
        {
            // Arrange
            using var factory = new CustomWebApplicationFactory();

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            // Act
            var response = await client.GetAsync("/Categories/Delete/2");

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(
                "Login",
                response.Headers.Location?.ToString() ?? "");
        }

        [Fact]
        public async Task Delete_NormalUser_ShouldReturnForbidden()
        {
            using var factory = new AuthenticatedWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Act
            var response = await client.GetAsync("/Categories/Delete/2");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        }

        [Fact]
        public async Task Delete_Admin_ShouldReachController()
        {
            // Arrange
            using var factory = new AuthenticatedWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

            // Act
            var response = await client.GetAsync("/Categories/Delete/2147483647");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_Admin_ExistingCategory_ShouldReturnOK()
        {
            // Arrange
            using var factory = new AuthenticatedWebApplicationFactory();
            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var category = new Category
            {
                Name = $"TestCategory_{Guid.NewGuid():N}"
            };

            db.Categories.Add(category);
            await db.SaveChangesAsync();

            try
            {
                using var client = factory.CreateClient();

                client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

                var response = await client.GetAsync($"/Categories/Delete/{category.Id}");

                var responseBody = await response.Content.ReadAsStringAsync();

                Assert.True(
                    response.StatusCode == HttpStatusCode.OK,
                    $"HTTP Status: {(int)response.StatusCode}\n" +
                    $"Response Body: {responseBody}");
            }
            finally
            {
                // Terdown:無論測試成功或失敗，都清理資料
                db.Categories.Remove(category);
                await db.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task Create_NormalUser_Category_ShouldReturnForbidden()
        {
            // Arrange
            using var factory = new AuthenticatedWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var categoryName = $"TestCategory_{Guid.NewGuid():N}";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Create.Name"] = categoryName
            });

            // Act
            var response = await client.PostAsync("/Categories/Create", content);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            bool categoryExists = await db.Categories
                .AsNoTracking()
                .AnyAsync(c => c.Name == categoryName);

            Assert.False(categoryExists);
        }

        [Fact]
        public async Task Category_Admin_ShouldCreateCategory()
        {
            // Arrange
            using var factory = new AuthenticatedWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });

            client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

            // Act
            var getResponse = await client.GetAsync("/Categories");
            var html = await getResponse.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            Assert.Contains(
                "name=\"__RequestVerificationToken\"", html);

            var tokenInput = Regex.Match(html,
                @"<input\b(?=[^>]*\bname=""__RequestVerificationToken"")[^>]*>",
                RegexOptions.IgnoreCase);

            Assert.True(tokenInput.Success);

            var tokenMatch = Regex.Match(tokenInput.Value, @"\bvalue=""([^""]+)""");

            Assert.True(tokenMatch.Success);

            string token = tokenMatch.Groups[1].Value;

            Assert.False(string.IsNullOrWhiteSpace(token));

            var categoryName = $"TestCategory_{Guid.NewGuid():N}";

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Create.Name"] = categoryName,
                ["__RequestVerificationToken"] = token
            });

            try
            {
                // Act:提交新增分類表單
                var postResponse = await client.PostAsync("/Categories/Create", content);

                // Assert 1:確認 Controller 成功重新導向
                //Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

                // Assert 2:確認 SQL Server 真的新增分類
                using var scope = factory.Services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                bool categoryExists = await db.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Name == categoryName);

                Assert.True(categoryExists);
            }
            finally
            {
                using var cleanupScope = factory.Services.CreateScope();

                var db = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await db.Categories.Where(c => c.Name == categoryName).ExecuteDeleteAsync();
            }
        }


        [Fact]
        public async Task Delete_Admin_ShouldSoftDeleteCategoryAndMoveArticles()
        {
            // Arrange
            var category = new Category
            {
                Name = $"TestCategory_{Guid.NewGuid():N}"
            };

            var authorId = Guid.NewGuid().ToString();

            var author = new IdentityUser
            {
                Id = authorId,
                UserName = $"TestAuthor_{Guid.NewGuid():N}"
            };

            var publishedArticle = new Article
            {
                Title = "Published Test Article",
                Content = "Published content",
                AuthorId = authorId,
                Status = ArticlesStatus.Published,
                Category = category,
            };

            var archivedArticle = new Article
            {
                Title = "Archived Test Article",
                Content = "Archived content",
                AuthorId = authorId,
                Status = ArticlesStatus.Archived,
                ArchivedAt = DateTime.UtcNow,
                Category = category,
            };

            //await Task.CompletedTask;

            using var factory = new AuthenticatedWebApplicationFactory();

            try
            {
                using var scope = factory.Services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.Users.Add(author);
                db.Categories.Add(category);

                db.Articles.AddRange(publishedArticle, archivedArticle);

                await db.SaveChangesAsync();

                // 確認分類已建立，而且尚未被刪除
                Assert.True(await db.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == category.Id && !c.IsDeleted));

                // 確認 Published 文章存在於測試分類
                Assert.True(await db.Articles
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.Id == publishedArticle.Id &&
                        a.CategoryId == category.Id &&
                        a.Status == ArticlesStatus.Published));

                // 確認 Archived 文章也存在於測試分類
                Assert.True(await db.Articles
                    .AsNoTracking()
                    .AnyAsync(a => a.Id == archivedArticle.Id &&
                        a.CategoryId == category.Id &&
                        a.Status == ArticlesStatus.Archived));

                // 建立模擬 Admin 的 HTTP Client
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });
                client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

                // GET：取得包含 Anti-Forgery Token 的頁面
                var getResponse = await client.GetAsync("/Categories");

                Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
                var html = await getResponse.Content.ReadAsStringAsync();

                // 從 HTML 擷取 Token
                var tokenInput = Regex.Match(
                    html,
                    @"<input\b(?=[^>]*\bname=""__RequestVerificationToken"")[^>]*>",
                    RegexOptions.IgnoreCase);
                Assert.True(tokenInput.Success);
                
                var tokenMatch = Regex.Match(
                    tokenInput.Value,
                    @"\bvalue=""([^""]+)""");

                Assert.True(tokenMatch.Success);

                string token = tokenMatch.Groups[1].Value;

                Assert.False(string.IsNullOrWhiteSpace(token));

                // Act：模擬 Admin 提交刪除分類表單
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = token
                    });

                var postResponse = await client.PostAsync(
                    $"/Categories/Delete/{category.Id}", content);

                Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

                using var verifyScope = factory.Services.CreateScope();

                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var deletedCategory = await verifyDb.Categories
                    .AsNoTracking()
                    .SingleAsync(c => c.Id == category.Id);

                Assert.True(deletedCategory.IsDeleted);
                Assert.NotNull(deletedCategory.DeletedAt);

                // Assert：Published Article 已搬移至未分類
                var publishedAfterDelete = await verifyDb.Articles
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == publishedArticle.Id);

                // Assert：Archived Article 也已搬移至未分類
                var archivedAfterDelete = await verifyDb.Articles
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == archivedArticle.Id);

                Assert.Equal(1, archivedAfterDelete.CategoryId);
                Assert.Equal(ArticlesStatus.Archived, archivedAfterDelete.Status);
            }
            finally
            {
                using var scope = factory.Services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. 刪除測試文章
                await db.Articles
                    .Where(a => a.AuthorId == authorId)
                    .ExecuteDeleteAsync();

                // 2. 刪除測試分類
                await db.Categories
                    .Where(c => c.Name == category.Name)
                    .ExecuteDeleteAsync();

                // 3. 刪除測試使用者
                await db.Users
                    .Where(u => u.Id == authorId)
                    .ExecuteDeleteAsync();
            }
        }

        [Fact]
        public async Task Delete_Admin_ShouldRollbackWhenSoftDeleteFails()
        {
            // 新增的失敗情境測試
            // 使用 RollbackWebApplicationFactory
            // 預期攔截器觸發例外
            // Category.IsDeleted == false
            // Article.CategoryId 維持原值

            // Arrange
            var category = new Category
            {
                Name = $"RollbackTest_{Guid.NewGuid():N}"
            };

            var authorId = Guid.NewGuid().ToString();

            var author = new IdentityUser
            {
                Id = authorId,
                UserName = $"TestAuthor_{Guid.NewGuid():N}"
            };

            var publishedArticle = new Article
            {
                Title = "Published Test Article",
                Content = "Published content",
                AuthorId = authorId,
                Status = ArticlesStatus.Published,
                Category = category,
            };

            var archivedArticle = new Article
            {
                Title = "Archived Test Article",
                Content = "Archived content",
                AuthorId = authorId,
                Status = ArticlesStatus.Archived,
                ArchivedAt = DateTime.UtcNow,
                Category = category,
            };

            //await Task.CompletedTask;

            using var factory = new RollbackWebApplicationFactory();

            try
            {
                using var scope = factory.Services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                db.Users.Add(author);
                db.Categories.Add(category);

                db.Articles.AddRange(publishedArticle, archivedArticle);

                await db.SaveChangesAsync();

                // 確認分類已建立，而且尚未被刪除
                Assert.True(await db.Categories
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == category.Id && !c.IsDeleted));

                // 確認 Published 文章存在於測試分類
                Assert.True(await db.Articles
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.Id == publishedArticle.Id &&
                        a.CategoryId == category.Id &&
                        a.Status == ArticlesStatus.Published));

                // 確認 Archived 文章也存在於測試分類
                Assert.True(await db.Articles
                    .AsNoTracking()
                    .AnyAsync(a => a.Id == archivedArticle.Id &&
                        a.CategoryId == category.Id &&
                        a.Status == ArticlesStatus.Archived));

                // 建立模擬 Admin 的 HTTP Client
                using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true
                });
                client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

                // GET：取得包含 Anti-Forgery Token 的頁面
                var getResponse = await client.GetAsync("/Categories");

                Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
                var html = await getResponse.Content.ReadAsStringAsync();

                // 從 HTML 擷取 Token
                var tokenInput = Regex.Match(
                    html,
                    @"<input\b(?=[^>]*\bname=""__RequestVerificationToken"")[^>]*>",
                    RegexOptions.IgnoreCase);
                Assert.True(tokenInput.Success);

                var tokenMatch = Regex.Match(
                    tokenInput.Value,
                    @"\bvalue=""([^""]+)""");

                Assert.True(tokenMatch.Success);

                string token = tokenMatch.Groups[1].Value;

                Assert.False(string.IsNullOrWhiteSpace(token));

                // Act：模擬 Admin 提交刪除分類表單
                using var content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["__RequestVerificationToken"] = token
                    });
                                
                // Act：預期 Delete 在 SaveChangesAsync 階段失敗
                HttpResponseMessage? postResponse = null;

                var exception = await Record.ExceptionAsync(async () =>
                {
                    postResponse = await client.PostAsync(
                        $"/Categories/Delete/{category.Id}",
                        content);
                });

                // Assert：確認操作沒有正常完成
                Assert.True(
                    exception is not null ||
                    postResponse?.StatusCode == HttpStatusCode.InternalServerError,
                    $"Expected failure, but received {postResponse?.StatusCode}.");

                // Assert：確認失敗確實由測試用 Interceptor 觸發
                Assert.True(factory.FailureInterceptor.WasTriggered);

                // Assert：重新查詢資料庫，確認 Category 沒有被刪除
                using var verifyScope = factory.Services.CreateScope();

                var verifyDb = verifyScope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                var categoryAfterRollback = await verifyDb.Categories
                    .AsNoTracking()
                    .SingleAsync(c => c.Id == category.Id);

                Assert.False(categoryAfterRollback.IsDeleted);
                Assert.Null(categoryAfterRollback.DeletedAt);

                // Assert：Published Article 沒有被搬移
                var publishedAfterRollback = await verifyDb.Articles
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == publishedArticle.Id);

                Assert.Equal(
                    category.Id,
                    publishedAfterRollback.CategoryId);

                Assert.Equal(
                    ArticlesStatus.Published,
                    publishedAfterRollback.Status);

                // Assert：Archived Article 也沒有被搬移
                var archivedAfterRollback = await verifyDb.Articles
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == archivedArticle.Id);
                Assert.Equal(
                    category.Id,
                    archivedAfterRollback.CategoryId);

                Assert.Equal(
                    ArticlesStatus.Archived,
                    archivedAfterRollback.Status);
            }
            finally
            {
                using var scope = factory.Services.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 1. 刪除測試文章
                await db.Articles
                    .Where(a => a.AuthorId == authorId)
                    .ExecuteDeleteAsync();

                // 2. 刪除測試分類
                await db.Categories
                    .Where(c => c.Name == category.Name)
                    .ExecuteDeleteAsync();

                // 3. 刪除測試使用者
                await db.Users
                    .Where(u => u.Id == authorId)
                    .ExecuteDeleteAsync();
            }
        }
    }
}
