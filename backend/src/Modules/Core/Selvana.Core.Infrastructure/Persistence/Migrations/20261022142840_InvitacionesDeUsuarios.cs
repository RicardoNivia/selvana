using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvitacionesDeUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invitacion_hash",
                schema: "core",
                table: "usuarios",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitacion_vence",
                schema: "core",
                table: "usuarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_invitacion_hash",
                schema: "core",
                table: "usuarios",
                column: "invitacion_hash",
                unique: true,
                filter: "invitacion_hash IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_usuarios_invitacion_hash",
                schema: "core",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "invitacion_hash",
                schema: "core",
                table: "usuarios");

            migrationBuilder.DropColumn(
                name: "invitacion_vence",
                schema: "core",
                table: "usuarios");
        }
    }
}
