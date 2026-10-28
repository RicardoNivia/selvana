using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>Las aclaraciones pasan de texto a pregunta con opciones (JSON). PostgreSQL no convierte text[] a jsonb: se recrea la columna.</remarks>
    public partial class AclaracionesConOpciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "aclaraciones", schema: "reclutamiento", table: "vacantes");
            migrationBuilder.AddColumn<string>(
                name: "aclaraciones",
                schema: "reclutamiento",
                table: "vacantes",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "aclaraciones", schema: "reclutamiento", table: "vacantes");
            migrationBuilder.AddColumn<List<string>>(
                name: "aclaraciones",
                schema: "reclutamiento",
                table: "vacantes",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }
    }
}
