using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TableGameRoom;

/// <summary>
/// 多开权限的本地解锁校验。
///
/// 安全说明（重要）：这是**离线**校验，没有服务器参与，因此不存在"绝对无法破解"的方案——
/// 攻击者拥有本机管理员权限时，总能通过调试器改内存、打补丁跳过判断，或直接改解锁标记文件。
/// 这里做的是把破解成本抬到"必须动手逆向"的程度，而不是"改个明文常量就能过"：
///
///   1. 验证码不落盘、不写进程序集字符串，只存 PBKDF2-SHA256 派生出的 32 字节哈希；
///   2. 迭代 600,000 次，单次校验约 0.2–0.5 秒，穷举 8 位字母数字空间不现实；
///   3. 哈希与盐都经过「异或掩码 + 分段打乱」，用 strings/ILSpy 直接搜不到明文；
///   4. 比对用 <see cref="CryptographicOperations.FixedTimeEquals"/>，避免逐字节比较的时序泄漏；
///   5. 解锁标记本身带 HMAC 签名，手工伪造标记文件不会被接受。
/// </summary>
internal static class MultiInstanceGate
{
    // 以下四组常量都不是明文：SEED = salt(32) || pepper(32)，HASH = PBKDF2(验证码, SEED, 600000)。
    // 常量本身经过「每段异或 + 段序打乱」，还原逻辑在 BuildSeed / BuildHash 里。

    private static readonly string[] SeedParts =
    [
        "5env4yPIwQ8=", "QDhLRV7yEWI=", "xUJOpX25tM8=", "eURYO+sXOx0=",
        "12Fte33JO+8=", "ilr3oztvn60=", "iDn422fdopI=", "6Jfft3+fP3k="
    ];

    private static readonly string[] HashParts =
    [
        "oncC3cFEff4=", "Ni/tE+2DBZk=", "RpnMJrbKU7I=", "XV+ow1Y7xe4="
    ];

    // 与生成脚本一致的掩码 / 段序
    private static readonly byte[] SeedMask = [0x9E, 0x37, 0xC5, 0x6B, 0x12, 0xE8, 0x4D, 0xA3];
    private static readonly int[] SeedOrder = [3, 7, 0, 5, 2, 6, 1, 4];
    private static readonly byte[] HashMask = [0x5A, 0xA5, 0x3C, 0xC3];
    private static readonly int[] HashOrder = [2, 0, 3, 1];

    private const int Iterations = 600_000;

    /// <summary>解锁标记文件（与存档同目录）。</summary>
    private static string FlagPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TableGameRoom");
            return Path.Combine(dir, ".unlock");
        }
    }

    /// <summary>是否已经解锁过多开权限（读标记文件 + 验签）。</summary>
    public static bool IsUnlocked()
    {
        try
        {
            var path = FlagPath;
            if (!File.Exists(path)) return false;

            var raw = File.ReadAllText(path, Encoding.UTF8).Trim();
            var parts = raw.Split('|');
            if (parts.Length != 2) return false;

            var payload = parts[0];
            var sig = parts[1];
            return FixedTimeStringEquals(sig, Sign(payload));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>校验用户输入的验证码。正确则写入解锁标记并返回 true。</summary>
    public static bool Verify(string code)
    {
        if (string.IsNullOrEmpty(code)) return false;

        // 统一去掉首尾空白，但**不做**大小写折叠：验证码区分大小写
        var candidate = code.Trim();
        if (candidate.Length is < 1 or > 64) return false;

        var seed = BuildSeed();
        var expected = BuildHash();

        byte[] actual;
        try
        {
            using var kdf = new Rfc2898DeriveBytes(
                candidate, seed, Iterations, HashAlgorithmName.SHA256);
            actual = kdf.GetBytes(32);
        }
        catch
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }

        var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
        CryptographicOperations.ZeroMemory(actual);
        CryptographicOperations.ZeroMemory(expected);

        if (ok) WriteFlag();
        return ok;
    }

    /// <summary>清理解锁状态（调试/重置用）。</summary>
    public static void Lock()
    {
        try { File.Delete(FlagPath); } catch { }
    }

    // ---------- 常量还原 ----------

    private static byte[] BuildSeed() => Rebuild(SeedParts, SeedMask, SeedOrder, 64);
    private static byte[] BuildHash() => Rebuild(HashParts, HashMask, HashOrder, 32);

    private static byte[] Rebuild(string[] parts, byte[] mask, int[] order, int length)
    {
        var segments = length / 8;
        var buffer = new byte[length];
        for (var i = 0; i < segments; i++)
        {
            // parts 里是按 order 打乱过的，还原成第 i 段要取 parts[order[i]]
            var seg = Convert.FromBase64String(parts[order[i]]);
            for (var j = 0; j < 8; j++)
                buffer[i * 8 + j] = (byte)(seg[j] ^ mask[i]);
        }
        return buffer;
    }

    // ---------- 解锁标记 ----------

    private static string Sign(string payload)
    {
        // 用还原出的种子做 HMAC 密钥，别人不知道种子就无法伪造签名
        var key = BuildSeed();
        try
        {
            using var hmac = new HMACSHA256(key);
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void WriteFlag()
    {
        try
        {
            var path = FlagPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // payload 带时间戳，签名后写入；篡改时间戳签名就不匹配
            var payload = $"multi:{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.WriteAllText(path, $"{payload}|{Sign(payload)}", Encoding.UTF8);
        }
        catch
        {
            // 写不进去也不影响本次使用（当前进程内已解锁）
        }
    }

    /// <summary>常量时间的字符串比较，避免时序侧信道。</summary>
    private static bool FixedTimeStringEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
