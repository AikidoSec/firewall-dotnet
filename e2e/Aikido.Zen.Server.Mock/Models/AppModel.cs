using System;
using System.Security.Cryptography;
using System.Text;

namespace Aikido.Zen.Server.Mock.Models;

public class AppModel
{
    public int Id { get; set; }
    public string Token { get; set; }
    public long ConfigUpdatedAt { get; set; }

    public static string GenerateToken(int appId)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var randomString = new string(Enumerable.Repeat(chars, 48)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        
        return $"AIK_RUNTIME_1_{appId}_{randomString}";
    }

    public static bool ValidateToken(string token1, string token2)
    {
        if (token1.Length != token2.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token1),
            Encoding.UTF8.GetBytes(token2));
    }
} 