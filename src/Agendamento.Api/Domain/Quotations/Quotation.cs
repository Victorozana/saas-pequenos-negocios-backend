using Agendamento.Domain.Common;
using Agendamento.Api.Domain.Customers;

namespace Agendamento.Api.Domain.Quotations;

public class Quotation : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string Code { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }
    
    public DateTime IssueDate { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    
    public QuotationStatus Status { get; private set; }
    
    public string? Notes { get; private set; }
    public string? PaymentTerms { get; private set; }
    
    private readonly List<QuotationItem> _items = new();
    public IReadOnlyCollection<QuotationItem> Items => _items.AsReadOnly();
    
    public decimal SubtotalAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    
    public DepositInfo? DepositInfo { get; private set; }

    private Quotation() { } // EF Core

    public static Quotation Create(Guid tenantId, string code, Guid customerId, DateTime issueDate, DateTime? validUntil, string? notes, string? paymentTerms)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer is required.", nameof(customerId));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be empty.", nameof(code));

        if (validUntil.HasValue && validUntil.Value < issueDate)
            throw new ArgumentException("Valid until date cannot be earlier than issue date.", nameof(validUntil));

        return new Quotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            CustomerId = customerId,
            IssueDate = issueDate,
            ValidUntil = validUntil,
            Status = QuotationStatus.Draft,
            Notes = notes,
            PaymentTerms = paymentTerms,
            SubtotalAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 0
        };
    }

    public void UpdatePendingContent(string code, Guid customerId, DateTime issueDate, DateTime? validUntil,
        string? notes, string? paymentTerms, IEnumerable<QuotationItem> items,
        DepositType? depositType, decimal depositValue, string? depositPaymentNotes)
    {
        if (Status != QuotationStatus.Pending)
            throw new InvalidOperationException("Can only edit pending quotations.");

        // Validate all replacement content before changing the persisted aggregate.
        ArgumentNullException.ThrowIfNull(items);
        var replacement = Create(TenantId, code, customerId, issueDate, validUntil, notes, paymentTerms);
        foreach (var item in items) replacement.AddItem(item);
        if (depositType.HasValue) replacement.SetDeposit(depositType.Value, depositValue, depositPaymentNotes);
        replacement.MarkAsPending();

        Code = replacement.Code;
        CustomerId = replacement.CustomerId;
        IssueDate = replacement.IssueDate;
        ValidUntil = replacement.ValidUntil;
        Notes = replacement.Notes;
        PaymentTerms = replacement.PaymentTerms;
        _items.Clear();
        _items.AddRange(replacement.Items);
        SubtotalAmount = replacement.SubtotalAmount;
        DiscountAmount = replacement.DiscountAmount;
        TotalAmount = replacement.TotalAmount;
        DepositInfo = replacement.DepositInfo;
    }

    public void AddItem(QuotationItem item)
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException("Can only add items to draft quotations.");

        ArgumentNullException.ThrowIfNull(item);
        ReplaceItems(_items.Append(item).ToList());
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException("Can only remove items from draft quotations.");

        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            ReplaceItems(_items.Where(i => i.Id != itemId).ToList());
        }
    }

    private void ReplaceItems(List<QuotationItem> items)
    {
        decimal subtotal, discount;
        try
        {
            subtotal = items.Sum(i => QuotationMoney.Subtotal(i.UnitPrice, i.Quantity));
            discount = items.Sum(i => i.DiscountAmount);
        }
        catch (OverflowException) { throw new ArgumentException("Quotation totals exceed the monetary limit.", nameof(items)); }
        QuotationMoney.Validate(subtotal, nameof(items));
        QuotationMoney.Validate(discount, nameof(items));
        var total = subtotal - discount;
        QuotationMoney.Validate(total, nameof(items));
        var deposit = DepositInfo == null ? null : DepositInfo.Create(DepositInfo.Type, DepositInfo.Value, total, DepositInfo.PaymentNotes);

        // Commit only after every calculation and the existing deposit have been validated.
        _items.Clear();
        _items.AddRange(items);
        SubtotalAmount = subtotal;
        DiscountAmount = discount;
        TotalAmount = total;
        DepositInfo = deposit;
    }

    public void SetDeposit(DepositType type, decimal value, string? paymentNotes)
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException("Can only set deposit on draft quotations.");

        DepositInfo = DepositInfo.Create(type, value, TotalAmount, paymentNotes);
    }
    
    public void RemoveDeposit()
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException("Can only remove deposit from draft quotations.");

        DepositInfo = null;
    }

    public void MarkAsPending()
    {
        if (Status != QuotationStatus.Draft)
            throw new InvalidOperationException("Can only transition to Pending from Draft.");
            
        if (!_items.Any())
            throw new InvalidOperationException("Cannot mark quotation as pending without items.");

        Status = QuotationStatus.Pending;
    }

    public void Approve()
    {
        if (Status != QuotationStatus.Pending)
            throw new InvalidOperationException("Can only approve Pending quotations.");
            
        if (ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow.Date)
            throw new InvalidOperationException("Cannot approve expired quotation.");

        Status = QuotationStatus.Approved;
    }

    public void Reject()
    {
        if (Status != QuotationStatus.Pending)
            throw new InvalidOperationException("Can only reject Pending quotations.");

        Status = QuotationStatus.Rejected;
    }

    public void Cancel()
    {
        if (Status == QuotationStatus.Approved || Status == QuotationStatus.Rejected || Status == QuotationStatus.Converted)
            throw new InvalidOperationException("Cannot cancel an already decided quotation.");

        Status = QuotationStatus.Canceled;
    }

    public void MarkAsConverted()
    {
        if (Status != QuotationStatus.Approved)
            throw new InvalidOperationException("Can only convert approved quotations.");

        Status = QuotationStatus.Converted;
    }

    public void CheckExpiration()
    {
        if (Status == QuotationStatus.Pending && ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow.Date)
        {
            Status = QuotationStatus.Expired;
        }
    }
}
