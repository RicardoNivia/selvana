namespace Selvana.Infrastructure.Common.Ejemplo;

/// <summary>
/// Una persona ficticia de los datos de ejemplo que también tendrá cuenta en el portal (ADR 0013). La cédula y el correo son los mismos
/// de su postulación o de su expediente, para que el portal le muestre sus procesos o su empleo. <paramref name="Documentos"/> son las
/// claves de «Mis documentos» que ya tiene guardados (Cedula, CertificadoBancario…).
/// </summary>
public sealed record PersonaPortalEjemplo(string Nombre, string Documento, string Correo, string Celular, string Ciudad, IReadOnlyList<string> Documentos);

/// <summary>Nombres y datos ficticios para generar muchas personas de ejemplo de forma estable (siempre las mismas).</summary>
public static class NombresEjemplo
{
    public static readonly string[] Nombres =
    [
        "Alejandra", "Brayan", "Camila", "Daniel", "Estefanía", "Fabián", "Gabriela", "Héctor", "Isabella", "Jorge", "Karen", "Leonardo", "Mariana", "Nicolás",
        "Olga", "Pedro", "Quintín", "Rosa", "Santiago", "Tatiana", "Ulises", "Valeria", "Wilson", "Ximena", "Yuliana", "Zoraida", "Andrés", "Bibiana", "Cristian",
        "Daniela", "Edwin", "Fernanda", "Germán", "Hilda", "Iván", "Juliana", "Kelly", "Luis", "Mónica", "Nelson", "Paula", "Ricardo", "Sandra", "Tomás", "Viviana",
    ];

    public static readonly string[] Apellidos =
    [
        "Acosta", "Benítez", "Cárdenas", "Delgado", "Escobar", "Franco", "Galeano", "Henao", "Ibáñez", "Jaramillo", "Londoño", "Montoya", "Narváez", "Ospina",
        "Patiño", "Quintero", "Restrepo", "Salazar", "Toro", "Uribe", "Valencia", "Zapata", "Arango", "Bermúdez", "Cifuentes", "Duque", "Espinosa", "Gaviria",
        "Hoyos", "Lozano", "Mejía", "Nieto", "Orozco", "Pineda", "Rincón", "Sierra", "Trujillo", "Vargas",
    ];

    /// <summary>Nombre completo estable para el índice dado.</summary>
    public static string Nombre(int i) =>
        $"{Nombres[i % Nombres.Length]} {Apellidos[(i * 7 + 3) % Apellidos.Length]} {Apellidos[(i * 11 + 5) % Apellidos.Length]}";

    /// <summary>Correo ficticio a partir del nombre, sin tildes y con el índice para que no se repita.</summary>
    public static string Correo(string nombre, int i)
    {
        var partes = nombre.ToLowerInvariant().Split(' ');
        var sinTildes = string.Concat($"{partes[0]}.{partes[1]}".Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        return $"{sinTildes}{i}@ejemplo.co";
    }

    public static string Celular(int i) => $"3{(10 + i % 40):00}{(1_000_000 + i * 7_919 % 8_999_999):0000000}";
}
