using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace Agendamento.IntegrationTests.Features;

public class TeamFinancialPostgreSqlTests
{
    private sealed class Tenant(Guid id):ITenantContext { public Guid TenantId=>id; public bool HasTenant=>true; }

    [Fact(DisplayName="PostgreSQL persiste aprovação, pagamentos e aceite público @spec:AC-094 @spec:AC-074 @spec:AC-075 @spec:AC-095")]
    public async Task PostgreSql_PersistsPaymentsAndInvitation()
    {
        var config=new ConfigurationBuilder().AddUserSecrets<Program>(optional:true).AddEnvironmentVariables().Build();
        var connection=config.GetConnectionString("PostgreSql") ?? throw new InvalidOperationException("Configure a conexão local para testar PostgreSQL.");
        var builder=new NpgsqlConnectionStringBuilder(connection) { Database="postgres",IncludeErrorDetail=false };
        var database="agendamento_verify_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{database}\"",admin)) await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database=database;
            var options=new DbContextOptionsBuilder<AgendamentoDbContext>().UseNpgsql(builder.ConnectionString).Options;
            using(var migration=new AgendamentoDbContext(options,new Tenant(Guid.Empty))) await migration.Database.MigrateAsync();
            using var factory=new TeamFinancialTestFactory(builder.ConnectionString);
            using var startup=factory.CreateClient();
            Guid tenant;
            using(var db=factory.Db(Guid.Empty)) tenant=(await db.Tenants.FirstAsync()).Id;
            var (client,_,_)=await factory.Member("owner_admin",[],tenant);
            Guid customerId;
            using(var db=factory.Db(tenant)) {
                var customer=Agendamento.Api.Domain.Customers.Customer.Create(tenant,"Cliente PG","11999999999",null,null,null);
                db.Customers.Add(customer);await db.SaveChangesAsync();customerId=customer.Id;
            }
            var quotation=await client.PostAsJsonAsync("/api/v1/quotations/",new {
                code="PG-"+Guid.NewGuid().ToString("N"),customerId,issueDate=DateTime.UtcNow,validUntil=DateTime.UtcNow.AddDays(10),
                items=new[]{new{serviceItemId=(Guid?)null,serviceName="Serviço PG",unit="un",unitPrice=100,quantity=2,discountAmount=0}},
                deposit=new{type=2,value=50,paymentNotes=(string?)null}
            });
            Assert.Equal(HttpStatusCode.Created,quotation.StatusCode);
            var quotationId=(await quotation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            var approvals=await Task.WhenAll(
                client.PatchAsJsonAsync($"/api/v1/quotations/{quotationId}/status",new{newStatus=3}),
                client.PatchAsJsonAsync($"/api/v1/quotations/{quotationId}/status",new{newStatus=3}));
            Assert.All(approvals,response=>Assert.Equal(HttpStatusCode.NoContent,response.StatusCode));
            using(var db=factory.Db(tenant)) {
                var titles=await db.ReceivableTitles.Where(t=>t.QuotationId==quotationId).ToListAsync();
                Assert.Equal(2,titles.Count);Assert.Equal(200,titles.Sum(t=>t.OriginalAmount));
                Assert.Contains(titles,t=>t.OriginalAmount==50);Assert.Contains(titles,t=>t.OriginalAmount==150);
            }
            var email=$"pg-{Guid.NewGuid():N}@example.test";
            var invited=await client.PostAsJsonAsync("/api/v1/team-members/invitations",new{email,role="Employee",permissions=new[]{Permissions.ServicesRead}});
            Assert.Equal(HttpStatusCode.Created,invited.StatusCode);
            string token;
            using(var db=factory.Db(tenant)) token=(await db.TeamInvitations.SingleAsync(i=>i.Email==email)).Token;
            using var anonymous=factory.CreateClient();
            Assert.Equal(HttpStatusCode.OK,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",new{token,password="Teste!Postgres12345",name="Teste PG"})).StatusCode);
            var created=await client.PostAsJsonAsync("/api/v1/financial/payables",new{supplierName="Fornecedor teste",description="Teste PG",amount=100,dueDate=DateTime.UtcNow.AddDays(5)});
            Assert.Equal(HttpStatusCode.Created,created.StatusCode);
            var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",new{amount=40,method=0,paymentDate=DateTime.UtcNow})).StatusCode);
            using(var db=factory.Db(tenant)) {
                var title=await db.PayableTitles.Include(t=>t.Payments).SingleAsync(t=>t.Id==id);
                Assert.Equal(60,title.BalanceDue);
                Assert.True(await db.TenantMemberships.Join(db.Users,m=>m.UserId,u=>u.Id,(m,u)=>new{m.IsActive,u.Email}).AnyAsync(x=>x.IsActive&&x.Email==email));
            }
            Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",new{token,password="Teste!Postgres12345"})).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            // Only the uniquely named database created by this test is removed.
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
