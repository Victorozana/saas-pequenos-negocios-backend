using System.Collections.Generic;

namespace Agendamento.Api.Application.Tenants.CompanyRegistry;

public record CompanyRegistryResult(
    string Cnpj,
    string CorporateName,
    string TradeName,
    string LegalNature,
    IEnumerable<string> Cnaes,
    string Status,
    RegistryAddress Address
);

public record RegistryAddress(
    string Street,
    string Number,
    string Complement,
    string Neighborhood,
    string City,
    string State,
    string ZipCode
);
