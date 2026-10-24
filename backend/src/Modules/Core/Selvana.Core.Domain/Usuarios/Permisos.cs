namespace Selvana.Core.Domain.Usuarios;

/// <summary>Los seis roles de Selvana. El candidato no se invita: se registra solo en el portal de empleo.</summary>
public enum Rol
{
    Administrador,
    GerenteRrhh,
    Analista,
    Lider,
    Empleado,
    Candidato,
}

public enum Accion
{
    AprobarBandeja,
    CrearVacantes,
    VerHojasDeVida,
    VerDatosSensibles,
    ConfigurarEstructura,
    GestionarUsuarios,
    ConfigurarAgentes,
    VerBitacora,
    ConsultarSusDatos,
}

/// <summary>Hasta dónde llega un permiso. Todo lo que no es <see cref="No"/> permite la acción con ese alcance.</summary>
public enum Alcance
{
    No,
    Si,
    SuEquipo,
    SuVacante,
    LoSuyo,
    Pedir,
}

/// <summary>Matriz rol × acción. Es la única fuente: la usan la autorización de la API y la pantalla de permisos.</summary>
public static class MatrizPermisos
{
    private static readonly Rol[] Orden = [Rol.Administrador, Rol.GerenteRrhh, Rol.Analista, Rol.Lider, Rol.Empleado, Rol.Candidato];

    private static readonly Dictionary<Accion, Alcance[]> Tabla = new()
    {
        [Accion.AprobarBandeja] = [Alcance.Si, Alcance.Si, Alcance.No, Alcance.SuEquipo, Alcance.No, Alcance.No],
        [Accion.CrearVacantes] = [Alcance.Si, Alcance.Si, Alcance.Si, Alcance.Pedir, Alcance.No, Alcance.No],
        [Accion.VerHojasDeVida] = [Alcance.Si, Alcance.Si, Alcance.Si, Alcance.SuVacante, Alcance.No, Alcance.LoSuyo],
        [Accion.VerDatosSensibles] = [Alcance.No, Alcance.Si, Alcance.No, Alcance.No, Alcance.No, Alcance.LoSuyo],
        [Accion.ConfigurarEstructura] = [Alcance.Si, Alcance.Si, Alcance.No, Alcance.No, Alcance.No, Alcance.No],
        [Accion.GestionarUsuarios] = [Alcance.Si, Alcance.Si, Alcance.No, Alcance.No, Alcance.No, Alcance.No],
        [Accion.ConfigurarAgentes] = [Alcance.Si, Alcance.Si, Alcance.No, Alcance.No, Alcance.No, Alcance.No],
        [Accion.VerBitacora] = [Alcance.Si, Alcance.Si, Alcance.No, Alcance.No, Alcance.No, Alcance.No],
        [Accion.ConsultarSusDatos] = [Alcance.No, Alcance.No, Alcance.No, Alcance.Si, Alcance.Si, Alcance.No],
    };

    public static IReadOnlyList<Rol> Roles => Orden;

    public static IReadOnlyList<Accion> Acciones => [.. Tabla.Keys];

    public static Alcance Para(Rol rol, Accion accion) => Tabla[accion][Array.IndexOf(Orden, rol)];

    public static bool Puede(Rol rol, Accion accion) => Para(rol, accion) != Alcance.No;

    /// <summary>Roles que pueden hacer la acción con cualquier alcance; sirve para las políticas de autorización.</summary>
    public static IReadOnlyList<Rol> RolesQuePueden(Accion accion) => [.. Orden.Where(r => Puede(r, accion))];
}
