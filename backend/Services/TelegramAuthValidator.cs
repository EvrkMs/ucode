using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Ucode.Backend.Models;
using Ucode.Backend.Options;

namespace Ucode.Backend.Services;

public interface ITelegramAuthValidator
{
    bool TryValidate(string initData, out TelegramAuthData authData, out string error);
}

public sealed class TelegramAuthValidator(IOptions<TelegramOptions> options, ILogger<TelegramAuthValidator> logger) : ITelegramAuthValidator
{
    private readonly TelegramOptions _options = options.Value;
    private readonly ILogger<TelegramAuthValidator> _logger = logger;

    public bool TryValidate(string initData, out TelegramAuthData authData, out string error)
    {
        authData = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            error = "Bot token is not configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(initData))
        {
            error = "initData is required.";
            return false;
        }

        var parsed = QueryHelpers.ParseQuery(initData);

        if (!parsed.TryGetValue("hash", out var hashValues) || string.IsNullOrWhiteSpace(hashValues))
        {
            error = "Missing hash.";
            return false;
        }

        var hash = hashValues.ToString();
        var dataCheckString = BuildDataCheckString(parsed);
        var secretKey = ComputeSecretKey(_options.BotToken);
        var computedHash = ComputeHash(secretKey, dataCheckString);

        if (!HashesAreEqual(hash, computedHash))
        {
            error = "Invalid Telegram signature.";
            return false;
        }

        if (!TryParseAuthDate(parsed, out var authDate, out error))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - authDate > TimeSpan.FromHours(1))
        {
            error = "Auth data is too old.";
            return false;
        }

        if (!parsed.TryGetValue("user", out var userValues) || string.IsNullOrWhiteSpace(userValues))
        {
            error = "Missing Telegram user payload.";
            return false;
        }

        var userJson = userValues.ToString();
        TelegramUser? user;

        try
        {
            user = JsonSerializer.Deserialize<TelegramUser>(userJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Telegram user payload: {Payload}", userJson);
            error = "Failed to parse Telegram user payload.";
            return false;
        }

        if (user is null || user.Id == 0)
        {
            error = "Telegram user id is missing.";
            return false;
        }

        authData = new TelegramAuthData(user, authDate);
        return true;
    }

    private static string BuildDataCheckString(Dictionary<string, StringValues> parsed)
    {
        var pieces = parsed
            .Where(pair => pair.Key != "hash")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value.ToString()}");

        return string.Join("\n", pieces);
    }

    private static byte[] ComputeSecretKey(string botToken)
    {
        // WebApp: secret_key = HMAC_SHA256("WebAppData", bot_token)
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(botToken));
    }

    private static string ComputeHash(byte[] secretKey, string dataCheckString)
    {
        using var hmac = new HMACSHA256(secretKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool HashesAreEqual(string received, string computed)
    {
        try
        {
            var receivedBytes = Convert.FromHexString(received);
            var computedBytes = Convert.FromHexString(computed);
            return receivedBytes.Length == computedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(receivedBytes, computedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseAuthDate(Dictionary<string, StringValues> parsed, out DateTimeOffset authDate, out string error)
    {
        authDate = default;
        error = string.Empty;

        if (!parsed.TryGetValue("auth_date", out var authDateValues) || string.IsNullOrWhiteSpace(authDateValues))
        {
            error = "auth_date is missing.";
            return false;
        }

        if (!long.TryParse(authDateValues.ToString(), out var authDateSeconds))
        {
            error = "auth_date is not a valid unix timestamp.";
            return false;
        }

        authDate = DateTimeOffset.FromUnixTimeSeconds(authDateSeconds);
        return true;
    }
}
