using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Selvana.Infrastructure.Common.Seguridad;

/// <summary>
/// PBKDF2-SHA256 con 600.000 iteraciones y sal de 16 bytes (recomendación OWASP). Formato: pbkdf2-sha256$iteraciones$sal$hash.
/// Lo usan la consola (ADR 0009) y el portal de la persona (ADR 0013).
/// </summary>
public static class ContrasenasPbkdf2
{
    private const string Prefijo = "pbkdf2-sha256";
    private const int Iteraciones = 600_000;
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;
    private static readonly byte[] SalFicticia = new byte[TamanoSal];

    public static string Calcular(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Derivar(contrasena, sal, Iteraciones);
        return string.Join('$', Prefijo, Iteraciones.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(sal), Convert.ToBase64String(hash));
    }

    /// <summary>Con hash null hace el mismo trabajo y devuelve false (para no revelar si la cuenta existe).</summary>
    public static bool Verificar(string? hash, string contrasena)
    {
        var partes = (hash ?? "").Split('$');
        if (partes.Length != 4 || partes[0] != Prefijo || !int.TryParse(partes[1], CultureInfo.InvariantCulture, out var iteraciones))
        {
            Derivar(contrasena, SalFicticia, Iteraciones);
            return false;
        }

        var esperado = Convert.FromBase64String(partes[3]);
        var calculado = Derivar(contrasena, Convert.FromBase64String(partes[2]), iteraciones);
        return CryptographicOperations.FixedTimeEquals(esperado, calculado);
    }

    private static byte[] Derivar(string contrasena, byte[] sal, int iteraciones) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(contrasena), sal, iteraciones, HashAlgorithmName.SHA256, TamanoHash);
}
