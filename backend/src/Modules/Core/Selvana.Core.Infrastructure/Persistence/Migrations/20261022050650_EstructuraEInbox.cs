using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EstructuraEInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "estructuras",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_estructuras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empleados = table.Column<int>(type: "integer", nullable: false),
                    estructura_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_areas", x => x.id);
                    table.ForeignKey(
                        name: "fk_areas_estructuras_estructura_id",
                        column: x => x.estructura_id,
                        principalSchema: "core",
                        principalTable: "estructuras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cargos",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empleados = table.Column<int>(type: "integer", nullable: false),
                    codigos_nomina = table.Column<List<string>>(type: "text[]", nullable: false),
                    nombres_nomina = table.Column<List<string>>(type: "text[]", nullable: false),
                    estado_perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    modelo_ia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aprobado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    aprobado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estructura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    perfil = table.Column<string>(type: "jsonb", nullable: true),
                    razones = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cargos", x => x.id);
                    table.ForeignKey(
                        name: "fk_cargos_estructuras_estructura_id",
                        column: x => x.estructura_id,
                        principalSchema: "core",
                        principalTable: "estructuras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "centros_costo",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sede = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empleados = table.Column<int>(type: "integer", nullable: false),
                    estructura_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centros_costo", x => x.id);
                    table.ForeignKey(
                        name: "fk_centros_costo_estructuras_estructura_id",
                        column: x => x.estructura_id,
                        principalSchema: "core",
                        principalTable: "estructuras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "importaciones_nomina",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fuente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    leida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_empleados = table.Column<int>(type: "integer", nullable: false),
                    errores = table.Column<List<string>>(type: "text[]", nullable: false),
                    mensaje_ia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modelo_ia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aplicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aplicada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estructura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    areas_leidas = table.Column<string>(type: "jsonb", nullable: true),
                    cargos_leidos = table.Column<string>(type: "jsonb", nullable: true),
                    centros_costo_leidos = table.Column<string>(type: "jsonb", nullable: true),
                    sedes_leidas = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importaciones_nomina", x => x.id);
                    table.ForeignKey(
                        name: "fk_importaciones_nomina_estructuras_estructura_id",
                        column: x => x.estructura_id,
                        principalSchema: "core",
                        principalTable: "estructuras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sedes",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empleados = table.Column<int>(type: "integer", nullable: false),
                    estructura_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sedes", x => x.id);
                    table.ForeignKey(
                        name: "fk_sedes_estructuras_estructura_id",
                        column: x => x.estructura_id,
                        principalSchema: "core",
                        principalTable: "estructuras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "propuestas_importacion",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origen = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    porque = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    evidencia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    incluida = table.Column<bool>(type: "boolean", nullable: false),
                    codigos = table.Column<List<string>>(type: "text[]", nullable: false),
                    valor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cantidad = table.Column<int>(type: "integer", nullable: false),
                    importacion_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_propuestas_importacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_propuestas_importacion_importaciones_nomina_importacion_id",
                        column: x => x.importacion_id,
                        principalSchema: "core",
                        principalTable: "importaciones_nomina",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_areas_estructura_id",
                schema: "core",
                table: "areas",
                column: "estructura_id");

            migrationBuilder.CreateIndex(
                name: "ix_cargos_estructura_id",
                schema: "core",
                table: "cargos",
                column: "estructura_id");

            migrationBuilder.CreateIndex(
                name: "ix_centros_costo_estructura_id",
                schema: "core",
                table: "centros_costo",
                column: "estructura_id");

            migrationBuilder.CreateIndex(
                name: "ix_importaciones_nomina_estructura_id",
                schema: "core",
                table: "importaciones_nomina",
                column: "estructura_id");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "core",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_propuestas_importacion_importacion_id",
                schema: "core",
                table: "propuestas_importacion",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_sedes_estructura_id",
                schema: "core",
                table: "sedes",
                column: "estructura_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "areas",
                schema: "core");

            migrationBuilder.DropTable(
                name: "cargos",
                schema: "core");

            migrationBuilder.DropTable(
                name: "centros_costo",
                schema: "core");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "propuestas_importacion",
                schema: "core");

            migrationBuilder.DropTable(
                name: "sedes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "importaciones_nomina",
                schema: "core");

            migrationBuilder.DropTable(
                name: "estructuras",
                schema: "core");
        }
    }
}
