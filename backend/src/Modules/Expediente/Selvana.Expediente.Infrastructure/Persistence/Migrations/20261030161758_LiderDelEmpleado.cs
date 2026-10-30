using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Expediente.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LiderDelEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lider_id",
                schema: "expediente",
                table: "empleados",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lider_nombre",
                schema: "expediente",
                table: "empleados",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_empresa_id_lider_id",
                schema: "expediente",
                table: "empleados",
                columns: new[] { "empresa_id", "lider_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_empleados_empresa_id_lider_id",
                schema: "expediente",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "lider_id",
                schema: "expediente",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "lider_nombre",
                schema: "expediente",
                table: "empleados");
        }
    }
}
