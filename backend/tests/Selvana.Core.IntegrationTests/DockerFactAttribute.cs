using System.Diagnostics;

namespace Selvana.Core.IntegrationTests;

/// <summary>Prueba que necesita Docker (Testcontainers); se omite en equipos sin Docker y corre en el pipeline.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerDisponible = new(Detectar);

    public static bool SeOmite => !DockerDisponible.Value;

    public DockerFactAttribute()
    {
        if (!DockerDisponible.Value)
        {
            Skip = "Docker no está disponible en este equipo.";
        }
    }

    private static bool Detectar()
    {
        try
        {
            using var proceso = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            return proceso is not null && proceso.WaitForExit(10_000) && proceso.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
