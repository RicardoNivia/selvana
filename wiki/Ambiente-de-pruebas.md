# Ambiente de pruebas en línea

Selvana corre en línea para pruebas en **https://selvana-pruebas.northcentralus.cloudapp.azure.com**, sobre una cuenta gratuita de
Azure (12 meses): una VM Linux con Docker (Caddy, API, worker de IA y web) y PostgreSQL administrado. La guía completa (recursos,
costos, secretos y cómo desplegar) está en `deploy/azure/README.md`.

## Cómo se despliega

- Cada pull request corre compilación y pruebas en GitHub Actions (`.github/workflows/ci-cd.yml`).
- Al hacer merge a `main`, si todo pasa, el mismo workflow construye las imágenes, las sube al registro de Azure y las levanta en la VM.
- También se puede desplegar a mano desde el equipo de desarrollo con `./deploy/azure/desplegar.ps1`.

## Datos de ejemplo

Al arrancar, la API siembra tres empresas ficticias con el flujo completo, sin gastar IA:

- **Talento Andino EST S.A.S.**: EST con dos empresas usuarias; vacantes en todos los estados, candidatos en cada etapa del tablero, preselección, pruebas, terna, una oferta y unos documentos por aprobar en la bandeja y un empleado creado.
- **Logística Caribe S.A.S.**: conductores de camión con postulantes rankeados.
- **Clínica del Valle IPS**: auxiliares de enfermería con pruebas completas.

Quién entra a cada una y qué datos trae: `deploy/azure/README.md`, sección «Datos de ejemplo». Las contraseñas viven en el `.env` del servidor, nunca en el repositorio.

## Costos

- Azure: dentro de los servicios gratuitos de 12 meses (VM B2ats v2, PostgreSQL B1MS, registro Standard). Hay un presupuesto de USD 5 con alerta.
- IA: los datos de ejemplo no la usan. Solo gasta cuando alguien usa una función de IA en el sitio (vacante en una frase, lectura de hojas de vida, ranking, preselección).
