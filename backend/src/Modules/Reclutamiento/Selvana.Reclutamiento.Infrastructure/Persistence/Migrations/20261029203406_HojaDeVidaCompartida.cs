using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HojaDeVidaCompartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<Guid>>(
                name: "hoja_de_vida_compartida_con",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                type: "uuid[]",
                nullable: false,
                // Las cuentas que ya existían no comparten su hoja de vida con nadie
                defaultValueSql: "'{}'::uuid[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hoja_de_vida_compartida_con",
                schema: "reclutamiento",
                table: "cuentas_candidato");
        }
    }
}
