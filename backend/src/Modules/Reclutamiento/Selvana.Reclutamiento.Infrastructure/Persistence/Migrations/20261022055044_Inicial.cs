using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reclutamiento");

            migrationBuilder.CreateTable(
                name: "archivos_hojas_vida",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_contenido = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano = table.Column<int>(type: "integer", nullable: false),
                    contenido_cifrado = table.Column<byte[]>(type: "bytea", nullable: false),
                    subido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archivos_hojas_vida", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "borradores_postulacion",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modelo_ia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    leido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    datos_leidos = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_borradores_postulacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidatos",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    datos = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidatos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "reclutamiento",
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
                name: "outbox_messages",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vacantes",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empresa_usuaria_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    frase = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cupos = table.Column<int>(type: "integer", nullable: false),
                    jornada = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    salario_minimo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    salario_maximo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    justificacion_salario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requisitos = table.Column<List<string>>(type: "text[]", nullable: false),
                    competencias = table.Column<List<string>>(type: "text[]", nullable: false),
                    preguntas_filtro = table.Column<List<string>>(type: "text[]", nullable: false),
                    supuestos = table.Column<List<string>>(type: "text[]", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cerrada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modelo_ia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mensaje_ia = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    editada_por_persona = table.Column<bool>(type: "boolean", nullable: false),
                    razones = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vacantes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "postulaciones",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    archivo_hoja_de_vida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campos_corregidos = table.Column<List<string>>(type: "text[]", nullable: false),
                    recibida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autorizacion = table.Column<string>(type: "jsonb", nullable: false),
                    respuestas = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_postulaciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_postulaciones_candidatos_candidato_id",
                        column: x => x.candidato_id,
                        principalSchema: "reclutamiento",
                        principalTable: "candidatos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidatos_empresa_id_documento",
                schema: "reclutamiento",
                table: "candidatos",
                columns: new[] { "empresa_id", "documento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "reclutamiento",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "reclutamiento",
                table: "outbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_postulaciones_candidato_id",
                schema: "reclutamiento",
                table: "postulaciones",
                column: "candidato_id");

            migrationBuilder.CreateIndex(
                name: "ix_postulaciones_numero",
                schema: "reclutamiento",
                table: "postulaciones",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_postulaciones_vacante_id_recibida_en",
                schema: "reclutamiento",
                table: "postulaciones",
                columns: new[] { "vacante_id", "recibida_en" });

            migrationBuilder.CreateIndex(
                name: "ix_vacantes_codigo",
                schema: "reclutamiento",
                table: "vacantes",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vacantes_empresa_id",
                schema: "reclutamiento",
                table: "vacantes",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_vacantes_estado_publicada_en",
                schema: "reclutamiento",
                table: "vacantes",
                columns: new[] { "estado", "publicada_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivos_hojas_vida",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "borradores_postulacion",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "postulaciones",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "vacantes",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "candidatos",
                schema: "reclutamiento");
        }
    }
}
