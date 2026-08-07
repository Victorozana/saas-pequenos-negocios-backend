using System.Collections.Generic;
using System.Linq;

namespace Agendamento.Api.Application.Tenants.CompanyRegistry;

public static class FoodCnaePolicy
{
    public static bool IsEligible(IEnumerable<string> cnaes)
    {
        if (cnaes == null || !cnaes.Any())
            return false;
        
        return cnaes.Any(cnae => 
            cnae.StartsWith("56") || // Serviços de alimentação (restaurantes, lanchonetes)
            cnae.StartsWith("472") || // Comércio varejista de bebidas e alimentos
            cnae.StartsWith("471") || // Supermercados
            cnae.StartsWith("10") ||  // Fabricação de produtos alimentícios
            cnae.StartsWith("11")     // Fabricação de bebidas
        );
    }
}
