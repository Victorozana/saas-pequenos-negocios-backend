namespace Agendamento.Api.Features.Tenants;

public record RegisterTenantRequest(
    string Cnpj,
    string CompanyName,
    string TradeName,
    string LegalNature,
    string PrimaryCnae,
    int CategoryId,
    string Email,
    string Phone,
    string AddressStreet,
    string AddressNumber,
    string AddressComplement,
    string AddressNeighborhood,
    string AddressCity,
    string AddressState,
    string AddressZipCode,
    string StateRegistration,
    string MunicipalRegistration,
    bool IsTaxExempt,
    string TaxRegime,
    string FiscalEmail,
    string AdminName,
    string AdminCpf,
    string AdminEmail,
    string AdminPassword,
    bool IsLegalRepresentative
);
