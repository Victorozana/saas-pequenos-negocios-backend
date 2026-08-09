using System.Collections.ObjectModel;

namespace Agendamento.Api.Domain.Notifications;

/// <summary>
/// A helper record to compile templates using placeholders (e.g. {{Name}})
/// </summary>
public record NotificationTemplate
{
    public string Name { get; init; }
    public string ContentTemplate { get; init; }
    public ReadOnlyDictionary<string, string> Variables { get; init; }

    public NotificationTemplate(string name, string contentTemplate, Dictionary<string, string> variables)
    {
        Name = name;
        ContentTemplate = contentTemplate;
        Variables = new ReadOnlyDictionary<string, string>(variables ?? new Dictionary<string, string>());
    }

    public string Render()
    {
        var result = ContentTemplate;
        if (string.IsNullOrWhiteSpace(result) || Variables == null || Variables.Count == 0)
            return result ?? string.Empty;

        foreach (var kvp in Variables)
        {
            var placeholder = "{{" + kvp.Key + "}}";
            result = result.Replace(placeholder, kvp.Value);
        }

        return result;
    }
}
