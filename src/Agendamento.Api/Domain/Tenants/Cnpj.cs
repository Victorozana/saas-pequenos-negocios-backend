using System.Text.RegularExpressions;

namespace Agendamento.Domain.Tenants;

public sealed record Cnpj
{
    public string Value { get; }

    private Cnpj(string value)
    {
        Value = value;
    }

    public static Cnpj Create(string cnpj)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cnpj);

        var normalized = new string(cnpj.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        if (normalized.Length != 14)
        {
            throw new ArgumentException("O CNPJ deve conter 14 caracteres alfanuméricos.", nameof(cnpj));
        }

        if (!IsValid(normalized))
        {
            throw new ArgumentException("CNPJ inválido.", nameof(cnpj));
        }

        return new Cnpj(normalized);
    }

    private static bool IsValid(string cnpj)
    {
        // First 12 are alphanumeric, last 2 must be digits
        if (!char.IsDigit(cnpj[12]) || !char.IsDigit(cnpj[13]))
        {
            return false;
        }
        
        // Block all-same digits/letters (e.g., 00000000000000, AAAAAAAAAAAAAA)
        if (cnpj.Distinct().Count() == 1)
        {
            return false;
        }

        var firstDigit = CalculateCheckDigit(cnpj, 12);
        if (cnpj[12] - '0' != firstDigit) return false;

        var secondDigit = CalculateCheckDigit(cnpj, 13);
        if (cnpj[13] - '0' != secondDigit) return false;

        return true;
    }

    private static int CalculateCheckDigit(string cnpj, int length)
    {
        // Multipliers for CNPJ
        int[] multipliers = length == 12 
            ? new int[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } 
            : new int[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        int sum = 0;
        for (int i = 0; i < length; i++)
        {
            int charValue = cnpj[i];
            
            // As per Receita Federal rule for Alphanumeric CNPJ:
            // Digits 0-9 keep their value 0-9.
            // Letters A-Z get the ASCII value minus 48. (A=65 -> 65-48 = 17)
            int numericValue = charValue >= '0' && charValue <= '9' 
                ? charValue - '0' 
                : charValue - 48;
                
            sum += numericValue * multipliers[i];
        }

        int remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
