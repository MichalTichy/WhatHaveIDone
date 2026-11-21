using System.Security.Cryptography;
using System.Text;

namespace Shared.Infrastructure.Authentication.ApiKey;

internal static class ApiKeyHash
{
    public static string Compute(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
