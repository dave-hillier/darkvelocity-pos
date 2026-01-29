using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.PaymentGateway.Api.Services;

public class KeyGenerationService
{
    private const int KeyLength = 32; // 32 bytes = 256 bits

    public (string key, string hash, string hint) GenerateApiKey(string prefix)
    {
        // Generate random bytes
        var randomBytes = RandomNumberGenerator.GetBytes(KeyLength);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);

        // Full key with prefix
        var key = $"{prefix}{randomPart}";

        // Generate SHA256 hash for storage
        var hash = ComputeHash(key);

        // Hint is last 4 characters
        var hint = key.Substring(key.Length - 4);

        return (key, hash, hint);
    }

    public string ComputeHash(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public string GenerateClientSecret(Guid paymentIntentId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(24);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 24);

        return $"pi_{paymentIntentId:N}_secret_{randomPart}";
    }

    public string GenerateWebhookSecret()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomPart = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);

        return $"whsec_{randomPart}";
    }

    public string GenerateTerminalRegistrationCode()
    {
        // Generate a simple 8-character alphanumeric code
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var randomBytes = RandomNumberGenerator.GetBytes(8);
        var result = new char[8];

        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(result);
    }

    public string GenerateReceiptNumber()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(4);
        var randomPart = BitConverter.ToUInt32(randomBytes) % 10000;

        return $"RF-{timestamp}-{randomPart:D4}";
    }
}
