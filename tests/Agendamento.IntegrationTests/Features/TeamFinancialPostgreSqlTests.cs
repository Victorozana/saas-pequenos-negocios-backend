using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Agendamento.IntegrationTests.Features;

public class TeamFinancialPostgreSqlTests
{
    [Fact(DisplayName="PostgreSQL persiste aprovação, pagamentos e aceite público @spec:AC-094 @spec:AC-074 @spec:AC-075 @spec:AC-095")]
    public async Task PostgreSql_PersistsPaymentsAndInvitation()
    {
        await using var fixture = await Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests.Database.Create(seed: false);
        using var factory=new TeamFinancialTestFactory(fixture.RestrictedConnection, fixture.AdministrativeConnection);
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
        // Hold the row so both requests must reach PostgreSQL before either approval finishes.
        await using var blocker = await fixture.Admin.BeginTransactionAsync();
        await Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests.Execute(fixture.Admin,
            "SELECT 1 FROM \"Quotations\" WHERE \"Id\"=@id::uuid FOR UPDATE", quotationId.ToString());
        var approvalTasks = new[] {
            client.PatchAsJsonAsync($"/api/v1/quotations/{quotationId}/status",new{newStatus=3}),
            client.PatchAsJsonAsync($"/api/v1/quotations/{quotationId}/status",new{newStatus=3})
        };
        long waiting = 0;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            do
            {
                await Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests.Execute(fixture.Admin, "SELECT pg_stat_clear_snapshot()");
                waiting = (long)(await Agendamento.IntegrationTests.Tenancy.PostgreSqlRlsTests.Scalar(fixture.Admin,
                    "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND query LIKE '%FOR UPDATE%' AND wait_event_type='Lock'"))!;
                if (waiting == 2) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < deadline);
        }
        finally { await blocker.RollbackAsync(); }
        var approvals=await Task.WhenAll(approvalTasks);
        Assert.Equal(2L, waiting); // A single-connection pool or serial requests must fail this proof.
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
}
