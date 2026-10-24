namespace Selvana.Web.Common;

/// <summary>
/// Nombres de las políticas de autorización por acción de la matriz de permisos («accion:VerHojasDeVida»).
/// Core las registra con los roles de su matriz; cualquier módulo las exige por nombre sin conocer los roles.
/// </summary>
public static class Politicas
{
    public const string Prefijo = "accion:";

    public const string GestionarUsuarios = Prefijo + "GestionarUsuarios";
    public const string VerHojasDeVida = Prefijo + "VerHojasDeVida";
    public const string VerBitacora = Prefijo + "VerBitacora";

    public static string DeAccion(string accion) => Prefijo + accion;
}
