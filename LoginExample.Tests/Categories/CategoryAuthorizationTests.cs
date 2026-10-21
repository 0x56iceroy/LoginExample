
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

                // Act
                var response = await client.GetAsync($"/Categories/Delete/{category.Id}");

                // Assert
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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
                Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

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
    }
}
