using Agendamento.Domain.Common;

namespace Agendamento.Api.Domain.Financial;

public class PayableTitle : ITenantOwned
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; set; }
    
    public string SupplierName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal OriginalAmount { get; private set; }
    public DateTime DueDate { get; private set; }
    
    public TransactionStatus Status { get; private set; }
    
    private readonly List<PaymentEntry> _payments = new();
    public IReadOnlyCollection<PaymentEntry> Payments => _payments.AsReadOnly();
    
    public decimal PaidAmount => _payments.Sum(p => p.Amount);
    public decimal BalanceDue => OriginalAmount - PaidAmount;

    private PayableTitle() { } // EF Core

    public static PayableTitle Create(Guid tenantId, string supplierName, string description, decimal amount, DateTime dueDate)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty.", nameof(description));

        if (string.IsNullOrWhiteSpace(supplierName))
            throw new ArgumentException("SupplierName cannot be empty.", nameof(supplierName));

        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.", nameof(amount));

        return new PayableTitle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierName = supplierName,
            Description = description,
            OriginalAmount = amount,
            DueDate = dueDate.ToUniversalTime(),
            Status = TransactionStatus.Pending
        };
    }

    public void RegisterPayment(decimal amount, PaymentMethod method, DateTime date, string? notes = null)
    {
        if (Status == TransactionStatus.Canceled)
            throw new InvalidOperationException("Cannot register payment for a canceled title.");

        if (Status == TransactionStatus.Paid)
            throw new InvalidOperationException("Title is already fully paid.");

        if (amount > BalanceDue)
            throw new InvalidOperationException($"Payment amount ({amount}) exceeds the balance due ({BalanceDue}).");

        var payment = PaymentEntry.Create(amount, method, date, notes);
        _payments.Add(payment);

        UpdateStatus();
    }

    public void CheckOverdue()
    {
        if (Status == TransactionStatus.Pending || Status == TransactionStatus.PartiallyPaid)
        {
            if (DateTime.UtcNow.Date > DueDate.Date)
            {
                Status = TransactionStatus.Overdue;
            }
        }
    }

    public void Cancel()
    {
        if (Status == TransactionStatus.Paid)
            throw new InvalidOperationException("Cannot cancel a fully paid title.");

        Status = TransactionStatus.Canceled;
    }

    private void UpdateStatus()
    {
        if (BalanceDue == 0)
        {
            Status = TransactionStatus.Paid;
        }
        else if (PaidAmount > 0)
        {
            Status = TransactionStatus.PartiallyPaid;
        }
        else if (DateTime.UtcNow.Date > DueDate.Date)
        {
            Status = TransactionStatus.Overdue;
        }
        else
        {
            Status = TransactionStatus.Pending;
        }
    }
}
