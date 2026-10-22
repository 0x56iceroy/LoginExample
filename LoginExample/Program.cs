using LoginExample.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace LoginExample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            //builder.Services.AddControllersWithViews();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection not found.");

            if (builder.Environment.IsEnvironment("Testing"))
            {
                var sqlConnection = new SqlConnectionStringBuilder(connectionString);

                if (!string.Equals(sqlConnection.InitialCatalog,
                    "LoginExample_TestDb",
                    StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(sqlConnection.DataSource,@"(localdb)\MSSQLLocalDB",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Testing 資料庫設定不正確。" +
                        $" Server={sqlConnection.DataSource};" +
                        $" Database={sqlConnection.InitialCatalog}");
                }
            }

            builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            builder.Services
                .AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
            });
            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            // 只信任本機反向代理
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto;

                options.KnownProxies.Add(IPAddress.Loopback);
                options.KnownProxies.Add(IPAddress.IPv6Loopback);

                options.ForwardLimit = 1;
            });

            var app = builder.Build();


            // One-time administrator bootstrap.
            // Runs only when explicitly enabled by the deployer.
            if (Environment.GetEnvironmentVariable("LOGINEXAMPLE_BOOTSTRAP_ADMIN") == "1")
            {
                var adminEmail = builder.Configuration["SeedAdmin:Email"];

                if (string.IsNullOrWhiteSpace(adminEmail))
                {
                    throw new InvalidOperationException(
                        "SeedAdmin:Email is required.");
                }

                using var scope = app.Services.CreateScope();

                var roleManager = scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

                var userManager = scope.ServiceProvider
                    .GetRequiredService<UserManager<IdentityUser>>();

                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser is null)
                {
                    throw new InvalidOperationException(
                        "The specified administrator account does not exist.");
                }

                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole("Admin"));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }

                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    var result = await userManager.AddToRoleAsync(
                        adminUser, "Admin");

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }

                Console.WriteLine("Admin bootstrap completed.");

                return;
            }

            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();

                var roleManager = scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    var result = await roleManager.CreateAsync(
                        new IdentityRole("Admin"));
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                    }
                }

                var adminEmail = builder.Configuration["SeedAdmin:Email"];
                if (!string.IsNullOrWhiteSpace(adminEmail))
                {
                    var userManager = scope.ServiceProvider
                        .GetRequiredService<UserManager<IdentityUser>>();

                    var adminUser = await userManager.FindByEmailAsync(adminEmail);

                    if (adminUser is null)
                    {
                        throw new InvalidOperationException("找不到指定的管理員測試帳號。");
                    }
                    if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                    {
                        var result = await userManager.AddToRoleAsync(adminUser, "Admin");
                        if (!result.Succeeded)
                        {
                            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
                        }
                    }
                }
            }


            // Configure the HTTP request pipeline.
            app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();

        }
    }
}