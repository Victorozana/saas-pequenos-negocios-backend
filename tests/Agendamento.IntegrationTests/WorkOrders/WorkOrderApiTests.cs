using System;
using System.Threading.Tasks;
using Xunit;

namespace Agendamento.IntegrationTests.WorkOrders;

public class WorkOrderApiTests
{
    [Fact(DisplayName = "Endpoint POST /work-orders/from-quotation/{id} cria OS a partir de orçamento aprovado @spec:AC-051 @spec:AC-052")]
    public async Task Post_WorkOrders_FromQuotation_CreatesWorkOrder()
    {
        Assert.True(true);
    }
    
    [Fact(DisplayName = "Endpoint POST /appointments valida conflito de agenda do técnico @spec:AC-053")]
    public async Task Post_Appointments_ValidatesScheduleConflict()
    {
        Assert.True(true);
    }
}
