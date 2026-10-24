using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmpresasYUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "empresas",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    es_temporal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cargo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cargo_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    todas_las_empresas_usuarias = table.Column<bool>(type: "boolean", nullable: false),
                    empresas_usuarias = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invitado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invitado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ultimo_ingreso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rol_sugerido_ia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    razon_rol_ia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "empresas_usuarias",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sector = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empresas_usuarias", x => x.id);
                    table.ForeignKey(
                        name: "fk_empresas_usuarias_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_empresas_usuarias_empresa_id_nit",
                schema: "core",
                table: "empresas_usuarias",
                columns: new[] { "empresa_id", "nit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_empresa_id_correo",
                schema: "core",
                table: "usuarios",
                columns: new[] { "empresa_id", "correo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "empresas_usuarias",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "core");

            migrationBuilder.DropTable(
                name: "empresas",
                schema: "core");
        }
    }
}
