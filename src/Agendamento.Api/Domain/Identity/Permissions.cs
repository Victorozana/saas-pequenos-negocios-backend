namespace Agendamento.Domain.Identity;

public static class Permissions
{
    // Clientes
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string CustomersDelete = "customers.delete";

    // Serviços
    public const string ServicesRead = "services.read";
    public const string ServicesWrite = "services.write";

    // Orçamentos e Ordens de Serviço (Vendas)
    public const string SalesRead = "sales.read";
    public const string SalesWrite = "sales.write";

    // Financeiro
    public const string FinancialRead = "financial.read";
    public const string FinancialWrite = "financial.write";

    // Agenda
    public const string AppointmentsRead = "appointments.read";
    public const string AppointmentsWrite = "appointments.write";

    // Equipe (Apenas Admins devem ter, ou quem o admin der)
    public const string TeamRead = "team.read";
    public const string TeamWrite = "team.write";

    public static readonly IReadOnlyList<string> All =
    [
        CustomersRead, CustomersWrite, CustomersDelete,
        ServicesRead, ServicesWrite,
        SalesRead, SalesWrite,
        FinancialRead, FinancialWrite,
        AppointmentsRead, AppointmentsWrite,
        TeamRead, TeamWrite
    ];
}
