using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Agendamento.Api.Application.Tenancy;
using Agendamento.Api.Infrastructure.Persistence;
using Agendamento.Application.Identity;
using Agendamento.Domain.Identity;
using Agendamento.Domain.Tenants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Agendamento.IntegrationTests.Features;

public sealed class TeamFinancialHttpTests : IClassFixture<TeamFinancialTestFactory>
{
    private readonly TeamFinancialTestFactory _factory;
    public TeamFinancialHttpTests(TeamFinancialTestFactory factory) => _factory = factory;

    [Fact(DisplayName="Perfil HTTP não expõe credenciais @spec:AC-070")]
    public async Task Profile_ContainsPermissionsAndTenant()
    {
        var (client, user, tenant) = await _factory.Member("owner_admin", []);
        var response = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user,json.GetProperty("id").GetGuid());
        Assert.Equal(tenant,json.GetProperty("tenantId").GetGuid());
        Assert.NotEmpty(json.GetProperty("permissions").EnumerateArray());
        Assert.False(json.TryGetProperty("passwordHash",out _));
    }

    [Fact(DisplayName="Permissões HTTP e revogação imediata @spec:AC-071 @spec:AC-096")]
    public async Task Permissions_AndRevocation_AreEnforced()
    {
        var (employee, user, tenant)=await _factory.Member("Employee", [Permissions.CustomersRead]);
        Assert.Equal(HttpStatusCode.Forbidden,(await employee.PostAsJsonAsync("/api/v1/customers/",new {name="Teste",phone="11999999999"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await employee.GetAsync("/api/v1/financial/receivables")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await employee.GetAsync("/api/v1/services/")).StatusCode);
        using (var db=_factory.Db(tenant)) { var member=await db.TenantMemberships.SingleAsync(m=>m.UserId==user);member.SetActive(false);await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Unauthorized,(await employee.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact(DisplayName="Equipe isolada e último owner_admin protegido @spec:AC-072 @spec:AC-073")]
    public async Task Isolation_LastAdmin_AndStatus()
    {
        var (admin,user,tenant)=await _factory.Member("owner_admin",[]);
        var (_,otherUser,_)=await _factory.Member("owner_admin",[]);
        var members=await admin.GetFromJsonAsync<JsonElement>("/api/v1/team-members");
        Assert.Single(members.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,(await admin.PutAsJsonAsync($"/api/v1/team-members/{otherUser}",new {role="Employee",permissions=Array.Empty<string>()})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PutAsJsonAsync($"/api/v1/team-members/{user}",new {role="Employee",permissions=Array.Empty<string>()})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PatchAsJsonAsync($"/api/v1/team-members/{user}/status",new {isActive=false})).StatusCode);
        var (employee,employeeId,_)=await _factory.Member("Employee",[],tenant);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PatchAsJsonAsync($"/api/v1/team-members/{employeeId}/status",new {isActive=false})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await employee.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Fact(DisplayName="Convite HTTP seguro e aceite público persistido @spec:AC-074 @spec:AC-075")]
    public async Task Invitation_IsConsumedOnce_WithoutSession()
    {
        var (admin,_,tenant)=await _factory.Member("owner_admin",[]);
        var email=$"invite-{Guid.NewGuid():N}@example.test";
        var response=await admin.PostAsJsonAsync("/api/v1/team-members/invitations",new {email,role="Employee",permissions=new[]{Permissions.ServicesRead}});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("token",out _));
        string token;
        using(var db=_factory.Db(tenant)) { var invite=await db.TeamInvitations.SingleAsync(i=>i.Id==body.GetProperty("id").GetGuid());token=invite.Token;Assert.True(invite.ExpiresAtUtc>DateTimeOffset.UtcNow.AddDays(6));Assert.True(await db.NotificationMessages.AnyAsync()); }
        using var anonymous=_factory.CreateClient();
        var request=new {token,password="Teste!Convite123456",name="Funcionário"};
        Assert.Equal(HttpStatusCode.OK,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",request)).StatusCode);
        using(var db=_factory.Db(tenant)) {var member=await db.TenantMemberships.Join(db.Users,m=>m.UserId,u=>u.Id,(m,u)=>new{m,u}).SingleAsync(x=>x.u.Email==email);Assert.Equal(tenant,member.m.TenantId);Assert.True(member.m.IsActive);Assert.NotEqual(request.password,member.u.PasswordHash);}
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",new {token="invalid",password=request.password})).StatusCode);
        string expiredToken;
        using(var db=_factory.Db(tenant)) {
            var expired=TeamInvitation.Create(tenant,$"expired-{Guid.NewGuid():N}@example.test","Employee",[],expirationDays:-1);
            expiredToken=expired.Token;db.TeamInvitations.Add(expired);await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync("/api/v1/team-members/invitations/accept",new {token=expiredToken,password=request.password})).StatusCode);
    }

    [Fact(DisplayName="Aprovação gera entrada e saldo uma única vez @spec:AC-094")]
    public async Task Approval_GeneratesDepositAndBalance_Once()
    {
        var (client,_,tenant)=await _factory.Member("owner_admin",[]);
        Guid customerId;
        using(var db=_factory.Db(tenant)) {
            var customer=Agendamento.Api.Domain.Customers.Customer.Create(tenant,"Cliente de teste","11999999999",null,null,null);
            db.Customers.Add(customer);await db.SaveChangesAsync();customerId=customer.Id;
        }
        var created=await client.PostAsJsonAsync("/api/v1/quotations/",new {
            code="Q-"+Guid.NewGuid().ToString("N"),customerId,issueDate=DateTime.UtcNow,validUntil=DateTime.UtcNow.AddDays(10),
            items=new[]{new{serviceItemId=(Guid?)null,serviceName="Instalação",unit="un",unitPrice=100,quantity=2,discountAmount=0}},
            deposit=new{type=2,value=50,paymentNotes=(string?)null}
        });
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        for(var i=0;i<2;i++) Assert.Equal(HttpStatusCode.NoContent,(await client.PatchAsJsonAsync($"/api/v1/quotations/{id}/status",new {newStatus=3})).StatusCode);
        using(var db=_factory.Db(tenant)) {
            var titles=await db.ReceivableTitles.Where(t=>t.QuotationId==id).ToListAsync();
            Assert.Equal(2,titles.Count);Assert.Equal(200,titles.Sum(t=>t.OriginalAmount));Assert.Contains(titles,t=>t.OriginalAmount==50);Assert.Contains(titles,t=>t.OriginalAmount==150);
        }
        var converted=await client.PostAsync($"/api/v1/quotations/{id}/convert",null);
        Assert.Equal(HttpStatusCode.OK,converted.StatusCode);
        var orderId=(await converted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("workOrderId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,(await client.PatchAsJsonAsync($"/api/v1/work-orders/{orderId}/status",new {status=4})).StatusCode);
        using(var db=_factory.Db(tenant)) Assert.Equal(Agendamento.Api.Domain.WorkOrders.WorkOrderStatus.Completed,(await db.WorkOrders.SingleAsync(w=>w.Id==orderId)).Status);
    }

    [Fact(DisplayName="Conta manual e pagamentos parciais persistidos @spec:AC-095 @spec:AC-096")]
    public async Task Financial_Payments_Statement_AndIsolation()
    {
        var (client,_,tenant)=await _factory.Member("owner_admin",[]);
        var (other,_,_)=await _factory.Member("owner_admin",[]);
        var due=DateTime.UtcNow.AddDays(5);
        var created=await client.PostAsJsonAsync("/api/v1/financial/payables",new {supplierName="Fornecedor de teste",description="Despesa de teste",amount=100,dueDate=due});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var payment=new {amount=40,method=0,paymentDate=DateTime.UtcNow,notes=(string?)null};
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",payment)).StatusCode);
        var titles=await client.GetFromJsonAsync<JsonElement>("/api/v1/financial/payables");
        Assert.Equal(40,titles[0].GetProperty("paidAmount").GetDecimal());
        Assert.Equal(60,titles[0].GetProperty("balanceDue").GetDecimal());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/financial/payables")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound,(await other.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",payment)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",new {amount=61,method=0,paymentDate=DateTime.UtcNow})).StatusCode);
        var statement=await client.GetFromJsonAsync<JsonElement>($"/api/v1/financial/statement?startDate={DateTime.UtcNow:yyyy-MM-dd}&endDate={DateTime.UtcNow.AddDays(10):yyyy-MM-dd}");
        Assert.Equal(40,statement.GetProperty("totalPaidPayables").GetDecimal());
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync($"/api/v1/financial/payables/{id}/pay",new {amount=60,method=0,paymentDate=DateTime.UtcNow})).StatusCode);
        using var db=_factory.Db(tenant);
        var paid=await db.PayableTitles.Include(t=>t.Payments).SingleAsync(t=>t.Id==id);
        Assert.Equal(0,paid.BalanceDue);
        Assert.Equal(Agendamento.Api.Domain.Financial.TransactionStatus.Paid,paid.Status);
    }
}

public sealed class TeamFinancialTestFactory : WebApplicationFactory<Program>
{
    private readonly string? _connection;
    public TeamFinancialTestFactory() { }
    internal TeamFinancialTestFactory(string connection) => _connection = connection;
    private readonly string _database=Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _root=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(new Dictionary<string,string?> {
            ["Database:RequirePostgreSql"]="false",
            ["Authentication:SigningKey"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }));
        builder.ConfigureServices(services=> {
            services.RemoveAll<DbContextOptions<AgendamentoDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AgendamentoDbContext>>();
            services.AddDbContext<AgendamentoDbContext>((sp,options)=> {
                if (_connection == null) options.UseInMemoryDatabase(_database,_root);
                else options.UseNpgsql(_connection);
                options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
            });
            services.RemoveAll<IHostedService>();
        });
    }
    private sealed class Tenant(Guid id):ITenantContext {public Guid TenantId=>id;public bool HasTenant=>true;}
    public AgendamentoDbContext Db(Guid tenant)
    {
        var options = new DbContextOptionsBuilder<AgendamentoDbContext>();
        if (_connection == null) options.UseInMemoryDatabase(_database,_root);
        else options.UseNpgsql(_connection);
        return new(options.Options,new Tenant(tenant));
    }
    public async Task<(HttpClient Client,Guid User,Guid Tenant)> Member(string role,List<string> permissions,Guid? tenantId=null)
    {
        var client=CreateClient();
        var tenant=tenantId??Guid.NewGuid();
        var password="Teste!Local123456";
        User user;
        using(var scope=Services.CreateScope())
        using(var db=Db(tenant)) {
            user=User.Create("Usuário de teste",null,$"test-{Guid.NewGuid():N}@example.test",scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(password));
            user.ConfirmEmail(DateTimeOffset.UtcNow);db.Users.Add(user);db.TenantMemberships.Add(TenantMembership.Create(tenant,user.Id,role,false,permissions));await db.SaveChangesAsync();
        }
        var login=await client.PostAsJsonAsync("/api/v1/auth/sessions",new {email=user.Email,password});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var session=await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",session.GetProperty("accessToken").GetString());
        return (client,user.Id,tenant);
    }
}
