# ADR 0011: Suite psicotécnica propia en lugar de un proveedor licenciado

- Estado: Aceptado (reemplaza la decisión de «banco propio + proveedor psicométrico licenciado» del Sprint 9)
- Fecha: 2026-10-22

## Contexto

El plan preveía aplicar las pruebas psicométricas a través de un proveedor licenciado. El dueño del producto prefiere una suite propia: las pruebas y sus análisis viven dentro de Selvana, el costo por prueba es casi cero, los datos se quedan en la plataforma y es un diferencial de venta. La empresa cuenta con un psicólogo en el equipo.

En Colombia la Ley 1090 de 2006 reserva la construcción, aplicación e interpretación de pruebas psicológicas a psicólogos, y una prueba solo sirve para seleccionar si es válida, confiable, tiene baremos y no perjudica a un grupo de forma injusta.

## Decisión

- Selvana tiene su **suite psicotécnica propia**, versionada. Cada prueba guarda su versión y cada resultado sabe con qué versión se obtuvo.
- **Responsable técnico:** el psicólogo del equipo aprueba cada prueba, sus ítems, sus claves y las plantillas de interpretación. La IA redacta ítems, sugiere baterías y prepara informes, pero no firma nada.
- **Estado de cada prueba:** `En validación` o `Validada`. Una prueba en validación se aplica y se muestra, pero **no cuenta en el puntaje** ni en la terna hasta que el psicólogo la valide con datos (confiabilidad, estructura y baremos colombianos).
- **Base de los instrumentos:** ítems propios y del IPIP (International Personality Item Pool, de dominio público). Nada de pruebas con derechos de autor (16PF, MMPI) ni proyectivas (Wartegg) ni clínicas.
- **La batería** se configura por cargo y cada vacante la hereda (se puede ajustar por vacante).
- **Datos sensibles:** los resultados de personalidad son datos sensibles; su detalle solo lo ve quien tiene el permiso `VerDatosSensibles` y cada apertura queda en la bitácora. El candidato da un consentimiento específico antes de empezar (Ley 1581, art. 6) y puede no responder.
- **Nadie se descarta por una prueba de forma automática:** quien no pasa el corte llega a la bandeja como propuesta que una persona aprueba.

## Fases

1. **Motor y pruebas básicas** (2 sprints): batería por cargo y por vacante, envío por enlace de un solo uso, pantalla del candidato con tiempo límite, antifraude, calificación automática y resultados en la vacante. Suite básica: atención y precisión, razonamiento numérico básico, juicio situacional de servicio al cliente y personalidad laboral (IPIP, en validación).
2. **Instrumentos completos** (2 a 3 sprints): personalidad y razonamiento con bancos calibrados, juicio situacional por cargo generado por la IA y validado por el psicólogo, informes narrativos de la IA con evidencia, edición de la suite desde la consola.
3. **Validación con datos colombianos** (3 a 6 meses, en paralelo): muestras de 300 a 500 personas por instrumento, confiabilidad, análisis factorial, baremos por sector e impacto adverso.

## Consecuencias

- El Sprint 9 se extiende; el plan puede pasar de 30 a 32 sprints si no se reordena.
- Mientras las pruebas estén en validación, los informes lo dicen y los puntajes no entran al ranking.
- Si un cliente exige un instrumento comercial, se puede conectar un proveedor como un tipo de prueba más, sin cambiar el motor.
