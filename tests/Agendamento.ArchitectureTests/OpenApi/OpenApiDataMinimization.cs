using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agendamento.ArchitectureTests.OpenApi;

// Inspect usage, not component names: a Request component can also be used as output.
internal static class OpenApiDataMinimization
{
    internal static IReadOnlyList<string> Inspect(JsonElement document)
    {
        var findings = new List<string>();
        var methods = new[] { "get", "post", "put", "patch", "delete", "head", "options", "trace" };
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject().Where(p => methods.Contains(p.Name)))
        {
            if (!operation.Value.TryGetProperty("responses", out var responses)) continue;
            foreach (var response in responses.EnumerateObject())
                Visit(response.Value, $"{operation.Name} {path.Name} response {response.Name}",
                    operation.Name == "post" && path.Name == "/api/v1/auth/sessions" && response.Name == "200",
                    new HashSet<string>());
        }
        Examples(document, "document", new HashSet<string>());
        return findings;

        JsonElement Resolve(string reference)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                throw new InvalidOperationException("OpenAPI reference must be local.");
            var target = document;
            foreach (var segment in reference[2..].Split('/'))
                target = target.GetProperty(Uri.UnescapeDataString(segment).Replace("~1", "/").Replace("~0", "~"));
            return target;
        }

        void Visit(JsonElement node, string location, bool session, HashSet<string> active)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray()) Visit(item, location, session, active);
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;
            if (node.TryGetProperty("$ref", out var reference))
            {
                var key = reference.GetString()!;
                if (active.Add(key))
                {
                    Visit(Resolve(key), location, session, active);
                    active.Remove(key);
                }
            }
            foreach (var entry in node.EnumerateObject())
            {
                if (entry.Name is "properties" or "headers")
                {
                    foreach (var property in entry.Value.EnumerateObject())
                    {
                        if (Forbidden(property.Name) && !(session && property.Name == "accessToken"))
                            findings.Add($"{location}/{entry.Name}/{property.Name}");
                        if (property.Name == "documentValue" && (!property.Value.TryGetProperty("pattern", out var pattern) ||
                            pattern.GetString() != Agendamento.Api.Application.Customers.GetCustomers.CustomerResponse.DocumentDisplayPattern))
                            findings.Add($"{location}: personal document requires a restricted display pattern");
                        Visit(property.Value, $"{location}/{property.Name}", session, active);
                    }
                }
                else if (entry.Name is not "$ref" and not "example" and not "examples")
                    Visit(entry.Value, $"{location}/{entry.Name}", session, active);
            }
        }

        void Examples(JsonElement node, string location, HashSet<string> active)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in node.EnumerateArray()) Examples(item, location, active);
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;
            if (node.TryGetProperty("$ref", out var reference))
            {
                var key = reference.GetString()!;
                if (active.Add(key)) { Examples(Resolve(key), location, active); active.Remove(key); }
            }
            foreach (var entry in node.EnumerateObject())
            {
                if (entry.Name == "externalValue") findings.Add($"{location}: unverifiable external example");
                else if (entry.Name is "example" or "examples")
                {
                    if (location.Split('/').Any(Forbidden)) findings.Add($"{location}: example for sensitive field");
                    ExampleValue(entry.Value, location, active);
                }
                else if (entry.Name != "$ref") Examples(entry.Value, $"{location}/{entry.Name}", active);
            }
        }

        void ExampleValue(JsonElement node, string location, HashSet<string> active)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("$ref", out var reference))
                {
                    var key = reference.GetString()!;
                    if (active.Add(key)) { ExampleValue(Resolve(key), location, active); active.Remove(key); }
                }
                foreach (var entry in node.EnumerateObject())
                {
                    if (Forbidden(entry.Name) || entry.Name == "externalValue") findings.Add($"{location}/example/{entry.Name}");
                    if (entry.Name != "$ref") ExampleValue(entry.Value, location, active);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
                foreach (var item in node.EnumerateArray()) ExampleValue(item, location, active);
            else if (node.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                var value = node.ValueKind == JsonValueKind.String ? node.GetString()! : node.GetRawText();
                // Values are never included in diagnostic output.
                if (Regex.IsMatch(value, @"(?<!\d)\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?!\d)|(?i)(Host|Password|User ID|Username)\s*=|postgres(?:ql)?://|Bearer\s+\S+|\beyJ[A-Za-z0-9_-]+\.") )
                    findings.Add($"{location}: sensitive example value");
                if (value.TrimStart().StartsWith('{') || value.TrimStart().StartsWith('['))
                {
                    try { using var embedded = JsonDocument.Parse(value); ExampleValue(embedded.RootElement, location, active); }
                    catch (JsonException) { findings.Add($"{location}: unverifiable serialized example"); }
                }
            }
        }
    }

    private static bool Forbidden(string name)
    {
        var normalized = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]", "");
        return new[] { "password", "senha", "hash", "secret", "token", "cpf", "connectionstring", "conexao", "credential", "credencial", "privatekey", "apikey", "certificatepassword", "bankaccount", "contabancaria" }
            .Any(normalized.Contains);
    }
}
