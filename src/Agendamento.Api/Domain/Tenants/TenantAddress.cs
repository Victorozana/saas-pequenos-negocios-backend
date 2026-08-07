namespace Agendamento.Domain.Tenants;

public sealed record TenantAddress
{
    public string Street { get; }
    public string Number { get; }
    public string Complement { get; }
    public string Neighborhood { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }

    private TenantAddress(string street, string number, string complement, string neighborhood, string city, string state, string zipCode)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
    }

    public static TenantAddress Create(
        string street, 
        string number, 
        string complement, 
        string neighborhood, 
        string city, 
        string state, 
        string zipCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(neighborhood);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipCode);

        if (state.Length != 2)
        {
            throw new ArgumentException("Estado deve ter 2 caracteres.", nameof(state));
        }

        return new TenantAddress(street, number, complement, neighborhood, city, state.ToUpperInvariant(), zipCode);
    }
}
