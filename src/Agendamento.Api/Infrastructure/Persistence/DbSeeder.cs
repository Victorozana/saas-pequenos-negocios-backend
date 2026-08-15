using System;
using System.Linq;
using System.Threading.Tasks;
using Agendamento.Application.Identity;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Agendamento.Api.Domain.Customers;
using Agendamento.Api.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendamento.Api.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedDefaultUserAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgendamentoDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var defaultEmail = "victor@gmail.com";
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == defaultEmail);

        if (existingUser != null)
        {
            return;
        }

        // 1. Create Default Tenant
        var cnpj = Cnpj.Create("00000000000191");
        var address = TenantAddress.Create("Rua das Flores", "100", "Sala 1", "Centro", "São Paulo", "SP", "01001000");
        var tenant = Tenant.Create(
            cnpj,
            "Empresa do Victor LTDA",
            "Victor Agendamentos",
            "Sociedade Empresária Limitada",
            "43.99-1-00",
            BusinessCategory.Marmoraria,
            defaultEmail,
            "11999998888",
            address);

        var fiscal = TenantFiscalProfile.Create(
            tenant.Id,
            "123456789",
            "987654321",
            false,
            TaxRegime.SimplesNacional,
            defaultEmail);

        // 2. Create Default User (victor@gmail.com / Senha@123456)
        var passwordHash = passwordService.Hash("Senha@123456");
        var user = User.Create("Victor Silva", "11144477735", defaultEmail, passwordHash);
        user.ConfirmEmail(DateTimeOffset.UtcNow);

        var membership = TenantMembership.Create(tenant.Id, user.Id, "owner_admin", true);

        db.Tenants.Add(tenant);
        db.TenantFiscalProfiles.Add(fiscal);
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);

        // 3. Create Sample Initial Data (Customers & Services)
        var customer1 = Customer.Create(
            tenant.Id,
            "Padaria Central",
            "11988887777",
            "contato@padariacentral.com",
            CustomerDocument.Create("CNPJ", "12345678000195"),
            CustomerAddress.Create("Av. Paulista", "1500", "Térreo", "Bela Vista", "São Paulo", "SP", "01310100"));

        var customer2 = Customer.Create(
            tenant.Id,
            "Maria Oliveira",
            "11977776666",
            "maria.oliveira@email.com",
            CustomerDocument.Create("CPF", "12345678901"),
            CustomerAddress.Create("Rua Augusta", "500", null, "Consolação", "São Paulo", "SP", "01305000"));

        var service1 = ServiceItem.Create(
            tenant.Id,
            "Corte de Mármore",
            "Corte sob medida com acabamento de borda",
            "m²",
            150.00m);

        var service2 = ServiceItem.Create(
            tenant.Id,
            "Instalação de Bancada",
            "Instalação e selamento de bancada de cozinha",
            "unidade",
            350.00m);

        db.Customers.AddRange(customer1, customer2);
        db.ServiceItems.AddRange(service1, service2);

        await db.SaveChangesAsync();
    }
}
