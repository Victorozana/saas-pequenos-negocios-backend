using Agendamento.Api.Domain.Services;
using Xunit;

namespace Agendamento.UnitTests.Services;

public sealed class ServiceCatalogValidationTests
{
    [Fact(DisplayName = "Catálogo limites Unicode coincidem com varchar sem recusar caracteres suplementares legítimos @spec:AC-045")]
    public void UnicodeScalarBoundaries()
    {
        static string Text(int count) => string.Concat(Enumerable.Repeat("🔧", count));
        var item = ServiceItem.Create(Guid.NewGuid(), Text(200), Text(1000), Text(50), 0m);
        Assert.Equal(Text(200), item.Name);
        foreach (var (name, description, unit) in new[] { (201, 1000, 50), (200, 1001, 50), (200, 1000, 51) })
        {
            Assert.Throws<ArgumentException>(() => item.Update(Text(name), Text(description), Text(unit), 1m));
            Assert.Equal(Text(200), item.Name);
            Assert.Equal(Text(1000), item.Description);
            Assert.Equal(Text(50), item.Unit);
            Assert.Equal(0m, item.BasePrice);
        }
    }

    [Theory(DisplayName = "Catálogo aceita limites físicos e preserva campos opcionais @spec:AC-045")]
    [InlineData(null, 0)]
    [InlineData("", 0.01)]
    public void AcceptsBoundaries(string? description, decimal price)
    {
        var service = ServiceItem.Create(Guid.NewGuid(), new string('n', 200), description, new string('u', 50), price);
        Assert.Equal(description, service.Description);
        Assert.Equal(price, service.BasePrice);
        service.Update(new string('n', 200), new string('d', 1000), new string('u', 50), 9999999999999999.99m);
        Assert.Equal(1000, service.Description!.Length);
        Assert.Equal(9999999999999999.99m, service.BasePrice);
    }

    [Theory(DisplayName = "Catálogo recusa excesso antes de alterar qualquer campo @spec:AC-045")]
    [InlineData(201, 1, 1, "name")]
    [InlineData(1, 1001, 1, "description")]
    [InlineData(1, 1, 51, "unit")]
    public void RejectsExcessWithoutMutation(int nameLength, int descriptionLength, int unitLength, string parameter)
    {
        var name = new string('n', nameLength);
        var description = new string('d', descriptionLength);
        var unit = new string('u', unitLength);
        Assert.Equal(parameter, Assert.Throws<ArgumentException>(() => ServiceItem.Create(Guid.NewGuid(), name, description, unit, 1m)).ParamName);
        var service = ServiceItem.Create(Guid.NewGuid(), "Original", null, "un", 0m);
        var id = service.Id;
        var tenant = service.TenantId;
        Assert.Equal(parameter, Assert.Throws<ArgumentException>(() => service.Update(name, description, unit, 1m)).ParamName);
        Assert.Equal(id, service.Id);
        Assert.Equal(tenant, service.TenantId);
        Assert.Equal("Original", service.Name);
        Assert.Null(service.Description);
        Assert.Equal("un", service.Unit);
        Assert.Equal(0m, service.BasePrice);
        Assert.True(service.IsActive);
    }
}
