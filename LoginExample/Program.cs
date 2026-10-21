using LoginExample.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

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

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();

                var roleManager = scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

                if(!await roleManager.RoleExistsAsync("Admin"))
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
                if(!string.IsNullOrWhiteSpace(adminEmail))
                {
                    var userManager = scope.ServiceProvider
                        .GetRequiredService<UserManager<IdentityUser>>();

                    var adminUser = await userManager.FindByEmailAsync(adminEmail);

                    if(adminUser is null)
                    {
                        throw new InvalidOperationException("找不到指定的管理員測試帳號。");
                    }
                    if(!await userManager.IsInRoleAsync(adminUser, "Admin"))
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
