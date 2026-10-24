using Microsoft.EntityFrameworkCore;
using Selvana.Infrastructure.Common.Persistence;

namespace Selvana.Core.Infrastructure.Persistence;

/// <summary>Unidad de trabajo del módulo Core; todas sus tablas viven en el esquema "core".</summary>
public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "core";
}
