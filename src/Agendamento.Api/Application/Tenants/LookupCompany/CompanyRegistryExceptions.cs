namespace Agendamento.Api.Application.Tenants.LookupCompany;

public sealed class CompanyRegistryNotFoundException : Exception
{
    public CompanyRegistryNotFoundException() : base("Empresa não encontrada.") { }
}

public sealed class CompanyRegistryInactiveException : Exception
{
    public CompanyRegistryInactiveException() : base("A empresa não está ativa.") { }
}

public sealed class CompanyRegistryUnavailableException : Exception
{
    public CompanyRegistryUnavailableException(Exception? innerException = null)
        : base("Não foi possível consultar o CNPJ agora.", innerException) { }
}
