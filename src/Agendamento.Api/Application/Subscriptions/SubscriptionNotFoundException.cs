namespace Agendamento.Api.Application.Subscriptions;

public sealed class SubscriptionNotFoundException(string message) : Exception(message);
