using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VacanteMunicipioYAclaraciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "aclaraciones",
                schema: "reclutamiento",
                table: "vacantes",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "codigo_municipio",
                schema: "reclutamiento",
                table: "vacantes",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aclaraciones",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "codigo_municipio",
                schema: "reclutamiento",
                table: "vacantes");
        }
    }
}
