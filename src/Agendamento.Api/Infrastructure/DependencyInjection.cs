namespace Agendamento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Registre aqui EF Core/Npgsql, Redis, mensageria e implementações de ports.
        // As dependências serão adicionadas ao iniciar a implementação de cada módulo.
        if (configuration.GetValue<bool>("Database:RequirePostgreSql"))
        {
            var connectionString = configuration.GetConnectionString("PostgreSql");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is required. Set ConnectionStrings__PostgreSql.");
            }

            try
            {
                var connectionStringBuilder = new System.Data.Common.DbConnectionStringBuilder
                {
                    ConnectionString = connectionString,
                };

                if (!connectionStringBuilder.TryGetValue("Host", out var host) ||
                    string.IsNullOrWhiteSpace(host?.ToString()))
                {
                    throw new ArgumentException("The PostgreSQL connection string must contain a Host.");
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "PostgreSQL configuration is invalid. Check ConnectionStrings__PostgreSql.",
                    exception);
            }
        }

        return services;
    }
}
