namespace Agendamento.Api.Domain.Customers;

public record CustomerAddress
{
    public string Street { get; init; }
    public string Number { get; init; }
    public string? Complement { get; init; }
    public string Neighborhood { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string ZipCode { get; init; }

    private CustomerAddress(string street, string number, string? complement, string neighborhood, string city, string state, string zipCode)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        ZipCode = zipCode;
    }

    public static CustomerAddress Create(string street, string number, string? complement, string neighborhood, string city, string state, string zipCode)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street cannot be empty.", nameof(street));
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Number cannot be empty.", nameof(number));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City cannot be empty.", nameof(city));
        if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("State cannot be empty.", nameof(state));
        
        return new CustomerAddress(street, number, complement, neighborhood, city, state, zipCode);
    }
}
