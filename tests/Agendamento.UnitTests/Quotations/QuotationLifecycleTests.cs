using System.Text.Json;
using Agendamento.Api.Domain.Quotations;
using Xunit;

namespace Agendamento.UnitTests.Quotations;

public class QuotationLifecycleTests
{
    [Fact(DisplayName = "Montagem interna Draft mantém identidade, coleção protegida e publica Pending com conteúdo completo @spec:AC-090")]
    public void DraftAssembly_PreservesIdentityAndPublishesPending()
    {
        var tenant = Guid.NewGuid();
        var customer = Guid.NewGuid();
        var quotation = Quotation.Create(tenant, "ORC-088", customer, DateTime.UtcNow.Date, null, "Observações", "À vista");
        Assert.NotEqual(Guid.Empty, quotation.Id);
        Assert.Equal(tenant, quotation.TenantId);
        Assert.Equal(customer, quotation.CustomerId);
        Assert.Equal(QuotationStatus.Draft, quotation.Status);
        Assert.Empty(quotation.Items);
        Assert.Equal(0m, quotation.TotalAmount);
        var empty = Snapshot(quotation);
        Assert.Throws<InvalidOperationException>(quotation.MarkAsPending);
        Assert.Equal(empty, Snapshot(quotation));
        var first = QuotationItem.Create(Guid.NewGuid(), "Instalação", "hora", 50m, 2m, 10m);
        var second = QuotationItem.Create(null, "Material", "un", 25m, 3m, 0m);
        quotation.AddItem(first);
        quotation.AddItem(second);
        Assert.Throws<NotSupportedException>(() => ((ICollection<QuotationItem>)quotation.Items).Clear());
        quotation.SetDeposit(DepositType.Percentage, 20m, "Entrada");
        var id = quotation.Id;
        quotation.MarkAsPending();
        Assert.Equal(id, quotation.Id);
        Assert.Equal(QuotationStatus.Pending, quotation.Status);
        Assert.Equal(new[] { first, second }, quotation.Items);
        Assert.Equal(175m, quotation.SubtotalAmount);
        Assert.Equal(10m, quotation.DiscountAmount);
        Assert.Equal(165m, quotation.TotalAmount);
        Assert.Equal(33m, quotation.DepositInfo!.RequiredAmount);
        Assert.Equal(132m, quotation.DepositInfo.RemainingBalance);
    }

    [Fact(DisplayName = "Identidades e intervalo de validade obrigatórios recusam montagem inválida @spec:AC-090")]
    public void Factory_RejectsMissingAssociationsAndInvalidValidity()
    {
        var date = DateTime.UtcNow.Date;
        Assert.Throws<ArgumentException>(() => Quotation.Create(Guid.Empty, "ORC", Guid.NewGuid(), date, null, null, null));
        Assert.Throws<ArgumentException>(() => Quotation.Create(Guid.NewGuid(), "ORC", Guid.Empty, date, null, null, null));
        Assert.Throws<ArgumentException>(() => Quotation.Create(Guid.NewGuid(), " ", Guid.NewGuid(), date, null, null, null));
        Assert.Throws<ArgumentException>(() => Quotation.Create(Guid.NewGuid(), "ORC", Guid.NewGuid(), date, date.AddDays(-1), null, null));
        Assert.Equal(date, Quotation.Create(Guid.NewGuid(), "ORC", Guid.NewGuid(), date, date, null, null).ValidUntil);
    }

    public static IEnumerable<object[]> TransitionCases()
    {
        foreach (var state in Enum.GetValues<QuotationStatus>())
        foreach (var operation in new[] { "pending", "approve", "reject", "cancel", "convert" })
            yield return new object[] { state, operation };
    }

    [Theory(DisplayName = "Matriz de transições conserva agregado em cada recusa e conteúdo nas transições válidas @spec:AC-048")]
    [MemberData(nameof(TransitionCases))]
    public void EveryTransition_EnforcesItsSourceAndPreservesContent(QuotationStatus source, string operation)
    {
        var quotation = InState(source);
        var before = Snapshot(quotation);
        var contentBefore = Snapshot(quotation, false);
        var allowed = operation switch
        {
            "pending" => source == QuotationStatus.Draft,
            "approve" or "reject" => source == QuotationStatus.Pending,
            "convert" => source == QuotationStatus.Approved,
            "cancel" => source is QuotationStatus.Draft or QuotationStatus.Pending or QuotationStatus.Expired or QuotationStatus.Canceled,
            _ => false
        };
        Action action = operation switch
        {
            "pending" => quotation.MarkAsPending,
            "approve" => quotation.Approve,
            "reject" => quotation.Reject,
            "cancel" => quotation.Cancel,
            "convert" => quotation.MarkAsConverted,
            _ => throw new ArgumentException(operation)
        };
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(action);
            Assert.Equal(before, Snapshot(quotation));
            return;
        }
        action();
        Assert.Equal(operation switch
        {
            "pending" => QuotationStatus.Pending,
            "approve" => QuotationStatus.Approved,
            "reject" => QuotationStatus.Rejected,
            "cancel" => QuotationStatus.Canceled,
            _ => QuotationStatus.Converted
        }, quotation.Status);
        Assert.Equal(contentBefore, Snapshot(quotation, false));
    }

    [Theory(DisplayName = "Fora do Draft montagem direta é recusada sem mutação de conteúdo, sinal ou estado @spec:AC-206")]
    [InlineData(QuotationStatus.Pending)]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Rejected)]
    [InlineData(QuotationStatus.Expired)]
    [InlineData(QuotationStatus.Canceled)]
    [InlineData(QuotationStatus.Converted)]
    public void NonDraft_RejectsAssemblyMutations(QuotationStatus status)
    {
        var quotation = InState(status);
        Action[] operations =
        [
            () => quotation.AddItem(QuotationItem.Create(null, "Extra", "un", 10m, 1m, 0m)),
            () => quotation.RemoveItem(quotation.Items.First().Id),
            () => quotation.SetDeposit(DepositType.FixedAmount, 1m, "Novo"),
            quotation.RemoveDeposit
        ];
        foreach (var operation in operations)
        {
            var before = Snapshot(quotation);
            Assert.Throws<InvalidOperationException>(operation);
            Assert.Equal(before, Snapshot(quotation));
        }
    }

    [Theory(DisplayName = "Expiração usa dia UTC estritamente anterior e aprovação recusada não expira nem modifica agregado @spec:AC-048")]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void ExpirationAndApproval_RespectValidityBoundary(int days)
    {
        var quotation = Draft(DateTime.UtcNow.Date.AddDays(days));
        quotation.MarkAsPending();
        var before = Snapshot(quotation);
        if (days < 0)
        {
            Assert.Throws<InvalidOperationException>(quotation.Approve);
            Assert.Equal(before, Snapshot(quotation));
            quotation.CheckExpiration();
            Assert.Equal(QuotationStatus.Expired, quotation.Status);
        }
        else
        {
            quotation.CheckExpiration();
            Assert.Equal(before, Snapshot(quotation));
            quotation.Approve();
            Assert.Equal(QuotationStatus.Approved, quotation.Status);
        }
    }

    [Fact(DisplayName = "Expiração não muda Draft nem decisões finais e ausência de validade permite aprovação @spec:AC-048")]
    public void Expiration_IsRestrictedToPendingWithPastValidity()
    {
        foreach (var state in Enum.GetValues<QuotationStatus>().Where(x => x != QuotationStatus.Pending))
        {
            var quotation = InState(state);
            var before = Snapshot(quotation);
            quotation.CheckExpiration();
            Assert.Equal(before, Snapshot(quotation));
        }
        var draft = Draft(DateTime.UtcNow.Date.AddDays(-1));
        var snapshot = Snapshot(draft);
        draft.CheckExpiration();
        Assert.Equal(snapshot, Snapshot(draft));
        var noValidity = Draft(null);
        noValidity.MarkAsPending();
        noValidity.CheckExpiration();
        noValidity.Approve();
        Assert.Equal(QuotationStatus.Approved, noValidity.Status);
    }

    [Fact(DisplayName = "Pagamento de entrada cria novo valor e preserva entrada original e agregado @spec:AC-046")]
    public void DepositPayment_ReturnsImmutableCopy()
    {
        var quotation = Draft(null);
        var original = quotation.DepositInfo!;
        var before = Snapshot(quotation);
        var paid = original.MarkAsPaid();
        Assert.NotSame(original, paid);
        Assert.False(original.IsPaid);
        Assert.True(paid.IsPaid);
        Assert.Equal(original, paid with { IsPaid = false });
        Assert.Equal(before, Snapshot(quotation));
        var paidAgain = paid.MarkAsPaid();
        Assert.Equal(paid, paidAgain);
        Assert.True(paid.IsPaid);
    }

    private static Quotation Draft(DateTime? validity)
    {
        var quotation = Quotation.Create(Guid.NewGuid(), "ORC-088", Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(-10), validity, "Notas", "Termos");
        quotation.AddItem(QuotationItem.Create(Guid.NewGuid(), "Serviço", "hora", 40m, 2m, 5m));
        quotation.SetDeposit(DepositType.Percentage, 20m, "Entrada");
        return quotation;
    }

    private static Quotation InState(QuotationStatus state)
    {
        var quotation = Draft(state == QuotationStatus.Expired ? DateTime.UtcNow.Date.AddDays(-1) : null);
        if (state == QuotationStatus.Draft) return quotation;
        quotation.MarkAsPending();
        switch (state)
        {
            case QuotationStatus.Approved: quotation.Approve(); break;
            case QuotationStatus.Rejected: quotation.Reject(); break;
            case QuotationStatus.Expired: quotation.CheckExpiration(); break;
            case QuotationStatus.Canceled: quotation.Cancel(); break;
            case QuotationStatus.Converted: quotation.Approve(); quotation.MarkAsConverted(); break;
        }
        Assert.Equal(state, quotation.Status);
        return quotation;
    }

    private static string Snapshot(Quotation quotation, bool includeStatus = true) => JsonSerializer.Serialize(new
    {
        quotation.Id, quotation.TenantId, quotation.CustomerId, quotation.Code,
        quotation.IssueDate, quotation.ValidUntil, quotation.Notes, quotation.PaymentTerms,
        quotation.SubtotalAmount, quotation.DiscountAmount, quotation.TotalAmount,
        quotation.DepositInfo, quotation.Items,
        Status = includeStatus ? quotation.Status : QuotationStatus.Draft
    });
}
