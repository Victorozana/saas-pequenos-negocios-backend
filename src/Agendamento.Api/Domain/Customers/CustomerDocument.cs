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
            
        return new CustomerDocument(type, value);
    }
}
