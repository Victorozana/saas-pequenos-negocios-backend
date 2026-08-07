using Agendamento.Domain.Tenants;
using Xunit;

namespace Agendamento.UnitTests.Tenants;

public sealed class CnpjTests
{
    [Theory(DisplayName = "CNPJ inválido é rejeitado localmente @spec:AC-014")]
    [InlineData("123")] // Too short
    [InlineData("00.000.000/0000-00")] // All zeros
    [InlineData("11.111.111/1111-11")] // All ones
    [InlineData("12.345.678/0001-90")] // Invalid check digit
    [InlineData("AB.CDE.FGH/0001-90")] // Invalid check digit for alphanumeric
    [InlineData("12.345.678/ABCD-00")] // Last two chars must be digits, not letters
    public void Create_ThrowsArgumentException_ForInvalidCnpj(string invalidCnpj)
    {
        var ex = Assert.Throws<ArgumentException>(() => Cnpj.Create(invalidCnpj));
        Assert.Contains("CNPJ", ex.Message);
    }

    [Theory(DisplayName = "CNPJ numérico válido é aceito")]
    [InlineData("19.131.243/0001-97", "19131243000197")] // Valid numeric
    [InlineData("11.222.333/0001-81", "11222333000181")] // Valid numeric
    public void Create_NormalizesAndAccepts_ValidNumericCnpj(string input, string expected)
    {
        var cnpj = Cnpj.Create(input);
        Assert.Equal(expected, cnpj.Value);
    }

    [Theory(DisplayName = "CNPJ alfanumérico válido é aceito")]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")] // Valid alphanumeric CNPJ according to Receita Federal rules
    public void Create_NormalizesAndAccepts_ValidAlphanumericCnpj(string input, string expected)
    {
        var cnpj = Cnpj.Create(input);
        Assert.Equal(expected, cnpj.Value);
    }

    [Fact(DisplayName = "CNPJ é único globalmente (normalização consistente) @spec:AC-022")]
    public void Create_NormalizesCasingAndPunctuation_ForGlobalIdentity()
    {
        var cnpj1 = Cnpj.Create("12.ABC.345/01DE-35");
        var cnpj2 = Cnpj.Create("12abc34501de35");
        
        Assert.Equal("12ABC34501DE35", cnpj1.Value);
        Assert.Equal(cnpj1.Value, cnpj2.Value);
    }
}
