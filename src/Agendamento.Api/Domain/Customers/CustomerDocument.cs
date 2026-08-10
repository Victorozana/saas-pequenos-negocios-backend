namespace Agendamento.Api.Domain.Customers;

public record CustomerDocument
{
    public string Type { get; init; } // CPF, CNPJ
    public string Value { get; init; }

    private CustomerDocument(string type, string value)
    {
        Type = type;
        Value = value;
    }

    public static CustomerDocument Create(string type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Document value cannot be empty.", nameof(value));
            
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Document type cannot be empty.", nameof(type));

        var cleanValue = new string(value.Where(char.IsDigit).ToArray());
        var cleanType = type.ToUpperInvariant();

        if (cleanType == "CPF" && cleanValue.Length != 11)
            throw new ArgumentException("CPF must contain exactly 11 digits.", nameof(value));

        if (cleanType == "CNPJ" && cleanValue.Length != 14)
            throw new ArgumentException("CNPJ must contain exactly 14 digits.", nameof(value));

        return new CustomerDocument(cleanType, cleanValue);
    }
}
