using Agendamento.Application.Identity.CreateSession;
using Agendamento.Application.Identity.VerifyEmail;

namespace Agendamento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddTransient<VerifyEmailHandler>();
        services.AddTransient<CreateSessionHandler>();

        // Tenants
        services.AddTransient<Agendamento.Api.Application.Tenants.RegisterTenant.RegisterTenantHandler>();

        // Customers
        services.AddTransient<Agendamento.Api.Application.Customers.CreateCustomer.CreateCustomerHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.UpdateCustomer.UpdateCustomerHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.GetCustomers.GetCustomersHandler>();
        services.AddTransient<Agendamento.Api.Application.Customers.GetCustomers.GetCustomerByIdHandler>();
        // Services
        services.AddTransient<Agendamento.Api.Application.Services.CreateService.CreateServiceHandler>();
        services.AddTransient<Agendamento.Api.Application.Services.GetServices.GetServicesHandler>();
        services.AddTransient<Agendamento.Api.Application.Services.GetServices.GetServiceByIdHandler>();
        services.AddTransient<Agendamento.Api.Application.Services.UpdateService.UpdateServiceHandler>();

        // Quotations
        services.AddTransient<Agendamento.Api.Application.Quotations.CreateQuotation.CreateQuotationHandler>();
        services.AddTransient<Agendamento.Api.Application.Quotations.GetQuotations.GetQuotationsHandler>();
        services.AddTransient<Agendamento.Api.Application.Quotations.GetQuotations.GetQuotationByIdHandler>();
        services.AddTransient<Agendamento.Api.Application.Quotations.UpdateQuotationStatus.UpdateQuotationStatusHandler>();
        
        // PDF Generator
        services.AddTransient<Agendamento.Api.Application.Quotations.ExportPdf.IQuotationPdfGenerator, Agendamento.Api.Infrastructure.Pdf.QuestPdfQuotationGenerator>();

        // WorkOrders
        services.AddTransient<Agendamento.Api.Application.WorkOrders.ConvertQuotationToWorkOrder.ConvertQuotationToWorkOrderHandler>();
        services.AddTransient<Agendamento.Api.Application.WorkOrders.GetWorkOrderById.GetWorkOrderByIdHandler>();
        services.AddTransient<Agendamento.Api.Application.WorkOrders.UpdateWorkOrderStatus.UpdateWorkOrderStatusHandler>();

        // Appointments
        services.AddTransient<Agendamento.Api.Application.Appointments.CreateAppointment.CreateAppointmentHandler>();
        services.AddTransient<Agendamento.Api.Application.Appointments.GetAppointments.GetAppointmentsHandler>();
        services.AddTransient<Agendamento.Api.Application.Appointments.UpdateAppointment.UpdateAppointmentHandler>();

        return services;
    }
}
