using LoginExample.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoginExample.Tests.Infrastructure
{
    public class TestingEnvironmentTests
    {
        [Fact]
        public void Factory_Should_Use_Testing_Database()
        {
            // Arrange
            using var factory = new CustomWebApplicationFactory();

            // Act：啟動測試主機並取得環境設定
            var environment = factory.Services
                .GetRequiredService<IHostEnvironment>();

            using var scope = factory.Services.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var connectionString = dbContext.Database
                .GetDbConnection().ConnectionString;

            var sqlConnection =
                new SqlConnectionStringBuilder(connectionString);

            // Assert
            Assert.Equal("Testing", environment.EnvironmentName);

            Assert.Equal(
                "LoginExample_TestDb",
                sqlConnection.InitialCatalog);

            Assert.Equal(
                @"(localdb)\MSSQLLocalDB",
                sqlConnection.DataSource);
        }
    }
}
