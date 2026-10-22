using LoginExample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Identity.Client;
using System.Data.SqlTypes;

namespace LoginExample.Tests.Infrastructure
{
    public class FailCategorySoftDeleteInterceptor : SaveChangesInterceptor
    {
        public bool WasInvoked { get; private set; }
        public bool WasTriggered { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            WasInvoked = true;

            var shouldFail = eventData.Context?
                .ChangeTracker
                .Entries<Category>()
                .Any(entry =>
                    entry.State == EntityState.Modified &&
                    entry.Entity.IsDeleted &&
                    entry.Entity.Name.StartsWith("RollbackTest_")) == true;


            if (shouldFail)
            {
                WasTriggered = true;
                throw new InvalidOperationException("Simulated Category Soft Delete failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
