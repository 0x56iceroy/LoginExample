using LoginExample.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoginExample.Tests.Infrastructure
{
    public class RollbackWebApplicationFactory : AuthenticatedWebApplicationFactory
    {
        public FailCategorySoftDeleteInterceptor FailureInterceptor { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(services =>
            {
                services.ConfigureDbContext<ApplicationDbContext>(
                    options =>
                    {
                        options.AddInterceptors(FailureInterceptor);
                    });
            });
        }
    }
}


/* 
 CustomWebApplicationFactory
│
│  Testing 環境
│  使用 LoginExample_TestDb
│
└── AuthenticatedWebApplicationFactory
    │
    │  加入 TestAuthHandler
    │  可以模擬 Admin 身分
    │
    └── RollbackWebApplicationFactory
        │
        │  加入 FailCategorySoftDeleteInterceptor
        │  模擬 Soft Delete 失敗
 */