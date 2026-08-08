namespace Agendamento.Api.Domain.Financial;

public enum TransactionStatus
{
    Pending,
    PartiallyPaid,
    Paid,
    Overdue,
    Canceled
}
