using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agendamento.Api.Application.Tenants.CompanyRegistry;
using Agendamento.Api.Application.Tenants.LookupCompany;

namespace Agendamento.Api.Infrastructure.CompanyRegistry;

public sealed class CompanyRegistryGateway(HttpClient client) : ICompanyRegistryGateway
{
    public async Task<CompanyRegistryResult?> GetCompanyAsync(
        string cnpj,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await client.GetAsync(
                $"api/cnpj/v1/{Uri.EscapeDataString(cnpj)}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CompanyRegistryUnavailableException();
            }

            var payload = await response.Content.ReadFromJsonAsync<BrasilApiCompany>(cancellationToken);
            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.Cnpj) ||
                string.IsNullOrWhiteSpace(payload.CorporateName) ||
                string.IsNullOrWhiteSpace(payload.Status))
            {
                throw new CompanyRegistryUnavailableException();
            }

            var cnaes = new List<string>();
            var primaryCnae = NormalizeCode(payload.PrimaryCnae);
            if (primaryCnae.Length > 0)
            {
                cnaes.Add(primaryCnae);
            }

            cnaes.AddRange((payload.SecondaryCnaes ?? [])
                .Select(item => NormalizeCode(item.Code))
                .Where(code => code.Length > 0));

            return new CompanyRegistryResult(
                payload.Cnpj,
                payload.CorporateName,
                payload.TradeName ?? string.Empty,
                payload.LegalNature ?? string.Empty,
                cnaes,
                payload.Status,
                new RegistryAddress(
                    payload.Street ?? string.Empty,
                    payload.Number ?? string.Empty,
                    payload.Complement ?? string.Empty,
                    payload.Neighborhood ?? string.Empty,
                    payload.City ?? string.Empty,
                    payload.State ?? string.Empty,
                    payload.ZipCode ?? string.Empty));
        }
        catch (CompanyRegistryUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or NotSupportedException ||
            ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new CompanyRegistryUnavailableException(ex);
        }
    }

    private static string NormalizeCode(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
        _ => string.Empty,
    };

    private sealed record BrasilApiCompany(
        [property: JsonPropertyName("cnpj")] string? Cnpj,
        [property: JsonPropertyName("razao_social")] string? CorporateName,
        [property: JsonPropertyName("nome_fantasia")] string? TradeName,
        [property: JsonPropertyName("natureza_juridica")] string? LegalNature,
        [property: JsonPropertyName("cnae_fiscal")] JsonElement PrimaryCnae,
        [property: JsonPropertyName("cnaes_secundarios")] IReadOnlyList<BrasilApiCnae>? SecondaryCnaes,
        [property: JsonPropertyName("descricao_situacao_cadastral")] string? Status,
        [property: JsonPropertyName("logradouro")] string? Street,
        [property: JsonPropertyName("numero")] string? Number,
        [property: JsonPropertyName("complemento")] string? Complement,
        [property: JsonPropertyName("bairro")] string? Neighborhood,
        [property: JsonPropertyName("municipio")] string? City,
        [property: JsonPropertyName("uf")] string? State,
        [property: JsonPropertyName("cep")] string? ZipCode);

    private sealed record BrasilApiCnae(
        [property: JsonPropertyName("codigo")] JsonElement Code);
}
