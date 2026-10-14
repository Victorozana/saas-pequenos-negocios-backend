using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Domain.Quotations;
using Agendamento.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Agendamento.IntegrationTests.Quotations;

public sealed class QuotationAggregateGuardTests
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();

    private sealed class TenantContext(Guid id) : ITenantContext
    {
        public Guid TenantId => id;
        public bool HasTenant => id != Guid.Empty;
    }

    private static AgendamentoDbContext Context(string database, Guid tenantId)
    {
        var tenant = new TenantContext(tenantId);
        return new(new DbContextOptionsBuilder<AgendamentoDbContext>()
            .UseInMemoryDatabase(database, DatabaseRoot)
            .AddInterceptors(new TenantSaveChangesInterceptor(tenant)).Options, tenant);
    }

    private static Quotation Draft(Guid tenant, string code)
        => Quotation.Create(tenant, code, Guid.NewGuid(), DateTime.UtcNow.Date,
            DateTime.UtcNow.Date.AddDays(7), "Original notes", "Original terms");

    public static IEnumerable<object[]> RejectedWrites()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var operation in new[] { "added", "modified", "deleted" })
        foreach (var threat in operation == "added"
                     ? new[] { "absent", "foreign", "current-tamper" }
                     : new[] { "absent", "foreign", "foreign-rewritten-current", "current-tamper", "original-tamper" })
            yield return new object[] { asynchronous, operation, threat };
    }

    [Theory(DisplayName = "Guarda do agregado orçamento recusa tenant ausente externo original/current e todo batch sem gravação @spec:AC-045")]
    [MemberData(nameof(RejectedWrites))]
    public async Task RejectedAggregateWriteCannotPersistValidQueuedWrite(bool asynchronous, string operation, string threat)
    {
        var database = Guid.NewGuid().ToString();
        var local = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var originalTenant = threat is "foreign" or "foreign-rewritten-current" ? foreign : local;
        var original = Draft(originalTenant, "original");
        Dictionary<string, object?> originalValues;
        using (var seed = Context(database, originalTenant))
        {
            seed.Quotations.Add(original);
            await seed.SaveChangesAsync();
            originalValues = seed.Entry(original).Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        }

        Guid validQueuedId;
        using (var writer = Context(database, threat == "absent" ? Guid.Empty : local))
        {
            var queued = Draft(local, "valid-queued");
            validQueuedId = queued.Id;
            writer.Quotations.Add(queued);

            Quotation invalid;
            if (operation == "added")
            {
                invalid = Draft(threat == "foreign" ? foreign : local, "invalid-new");
                writer.Quotations.Add(invalid);
            }
            else
            {
                invalid = await writer.Quotations.IgnoreQueryFilters().SingleAsync(q => q.Id == original.Id);
                if (operation == "deleted") writer.Quotations.Remove(invalid);
                else writer.Entry(invalid).Property(q => q.Notes).CurrentValue = "Forbidden change";
            }

            var tenant = writer.Entry(invalid).Property(q => q.TenantId);
            if (threat == "foreign-rewritten-current") tenant.CurrentValue = local;
            if (threat == "current-tamper") tenant.CurrentValue = foreign;
            if (threat == "original-tamper") tenant.OriginalValue = foreign;

            // No items or owned deposit can reject this batch before the root guard.
            Assert.Empty(writer.ChangeTracker.Entries<QuotationItem>());
            Assert.Empty(writer.ChangeTracker.Entries<DepositInfo>());
            Assert.Equal(EntityState.Added, writer.Entry(queued).State);
            if (asynchronous)
                await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
            else
                Assert.Throws<InvalidOperationException>(() => writer.SaveChanges());
        }

        // A new context checks actual InMemory persistence, not the failed writer's tracker.
        using var reader = Context(database, local);
        var persisted = Assert.Single(await reader.Quotations.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(original.Id, persisted.Id);
        Assert.NotEqual(validQueuedId, persisted.Id);
        foreach (var property in reader.Entry(persisted).Properties)
            Assert.Equal(originalValues[property.Metadata.Name], property.CurrentValue);
        Assert.Empty(persisted.Items);
        Assert.Null(persisted.DepositInfo);
    }
}
