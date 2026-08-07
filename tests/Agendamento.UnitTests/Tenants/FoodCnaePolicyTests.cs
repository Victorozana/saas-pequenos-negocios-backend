using System.Collections.Generic;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
using Xunit;

namespace Agendamento.UnitTests.Tenants;

public class FoodCnaePolicyTests
{
    [Theory]
    [InlineData("56.11-2-01")]
    [InlineData("5621900")]
    [InlineData("4729699")]
    [InlineData("4711302")]
    [InlineData("1091102")]
    [InlineData("1113502")]
    public void IsEligible_ShouldReturnTrue_WhenCnaeIsFoodRelated(string cnae)
    {
        var cnaes = new List<string> { "0000000", cnae };
        var isEligible = FoodCnaePolicy.IsEligible(cnaes);
        Assert.True(isEligible);
    }

    [Fact]
    public void IsEligible_ShouldReturnFalse_WhenNoFoodCnaeIsPresent()
    {
        var cnaes = new List<string> { "6204000", "7020400" };
        var isEligible = FoodCnaePolicy.IsEligible(cnaes);
        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligible_ShouldReturnFalse_WhenCnaesListIsEmpty()
    {
        var cnaes = new List<string>();
        var isEligible = FoodCnaePolicy.IsEligible(cnaes);
        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligible_ShouldReturnFalse_WhenCnaesListIsNull()
    {
        var isEligible = FoodCnaePolicy.IsEligible(null!);
        Assert.False(isEligible);
    }
}
