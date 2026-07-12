namespace BrewAlert.TeamsBot;

using System.Security.Cryptography;
using System.Text;

public static class ApiKeyValidator
{
    public static bool IsValid(string expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrEmpty(provided))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }
}
