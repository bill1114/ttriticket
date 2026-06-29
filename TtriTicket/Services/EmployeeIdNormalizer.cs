using System.Globalization;

namespace TtriTicket.Services;

public static class EmployeeIdNormalizer
{
    /// <summary>
    /// 正規化職編。
    /// 596 與 D596 為不同人員：含英文字母者完整保留，絕不轉成數字。
    /// </summary>
    public static string Normalize(string employeeId)
    {
        var trimmed = employeeId.Trim().TrimStart('\'', '"', '\ufeff');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var slashIndex = trimmed.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
        {
            trimmed = trimmed[(slashIndex + 1)..].Trim();
        }

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        // 僅「完全不含字母」的職編才做數字正規化（1524.0 → 1524）
        if (IsPureNumericId(trimmed, out var numericValue))
        {
            return numericValue.ToString(CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    /// <summary>
    /// 比對是否為同一人。596 ≠ D596；D596 與 d596 視為同一人。
    /// </summary>
    public static bool Equals(string? left, string? right)
    {
        var normalizedLeft = Normalize(left ?? string.Empty);
        var normalizedRight = Normalize(right ?? string.Empty);

        if (string.IsNullOrEmpty(normalizedLeft) || string.IsNullOrEmpty(normalizedRight))
        {
            return string.IsNullOrEmpty(normalizedLeft) && string.IsNullOrEmpty(normalizedRight);
        }

        return string.Equals(
            normalizedLeft,
            normalizedRight,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsLetter(string employeeId) =>
        Normalize(employeeId).Any(char.IsLetter);

    private static bool IsPureNumericId(string value, out long numericValue)
    {
        numericValue = 0;

        // 596 與 D596 不可混淆：只要含字母就不是純數字職編
        if (value.Any(char.IsLetter))
        {
            return false;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericValue))
        {
            return true;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        numericValue = (long)Math.Round(number, MidpointRounding.AwayFromZero);
        return true;
    }
}
