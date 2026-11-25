# Instrucciones para agentes

Este archivo conserva el contexto y el plan de trabajo del agente. Las instrucciones del usuario prevalecen.

## Comunicación y desarrollo

- Trabajar incrementalmente y centrarse en consola; Functions se valida más adelante.
- Compartir instrucciones de ejecución y pruebas directamente en el chat, no en README.
- Hacer commits al completar cambios coherentes y verificados, sin esperar recordatorios del usuario. No atribuir al agente la validación manual del usuario.
- Mantener README como descripción breve del proyecto. No crear PLAN.md ni CONTEXTO.md separados; actualizar este archivo.
- Aplicar Ponytail: cambio mínimo completo, sin dependencias ni abstracciones anticipadas. No instalar frameworks de pruebas por rutina.
- Usar Context7 para documentación vigente de bibliotecas, SDK, APIs, CLI y servicios; resolver library ID antes de consultar.
- Conservar convenciones y DLL de TI; observabilidad diferida al final por petición del usuario. No integrarla ahora.
- No conservar scripts de comprobación Python ni crear suites de pruebas sin una necesidad acordada. El usuario pidió eliminar tests/console-check.py; ya fue retirado.
- No incorporar servicios de memoria adicionales.

# Contexto y decisiones vigentes

## Objetivo

Extraer periódicamente datos de Alma/Analytics, Springshare/LibCal y Elogim/MariaDB, transformarlos y almacenarlos inicialmente en PostgreSQL de Neon. Aprender C# y la arquitectura durante el desarrollo incremental.

## Decisiones

- Presupuesto de servicios: cero. No provisionar Azure ni habilitar consumo facturable.
- Desarrollo: VS Code sobre WSL2, .NET 10; actualmente solo consola.
- Ejecución futura inicial: GitHub Actions con runner Linux, dentro de la cuota gratuita de la cuenta propietaria.
- Destino inicial: Neon PostgreSQL; motor futuro sin decidir (PostgreSQL Azure o SQL Server Azure).
- Arquitectura ligera: Domain, Application, Infrastructure y dos entradas, Console y Functions. Una lógica compartida, sin duplicación del ETL.
- Functions queda reservado, sin validaciones adicionales por ahora.
- El usuario confirma acceso mediante credenciales a los tres sistemas; falta verificar conexiones reales desde el runner.
- Referencia TI conservada en `docs/template-*`, DLL original en `lib/Shared.ObservabilityNet.dll`. Template original eliminado a petición del usuario. DLL todavía no integrada ni validada en Linux.
- No instalar memoria adicional, dependencias o suites de pruebas por rutina.

## Fechas solicitadas

Alma: D−2; LibCal: D; Elogim: D−1. Interpretación provisional: un día calendario específico por origen, no un rango acumulado. America/Lima y un único día calendario por fuente fueron confirmados por el usuario. Ventana nocturna confirmada desde las 22:00 America/Lima; disparo operativo ajustado a las 22:07 tras el retraso del 19/11. LibCal solo consulta hoy en Lima. Alma ya tiene filtros y columnas confirmados.

## Pendientes de producto

Precisar primer conjunto de datos, campos, identificadores únicos, filtros, formatos, volumen, paginación y límites. Para LibCal D puede ser un día incompleto: decidir qué datos se necesitan y si se recargan después. Definir carga histórica, recuperación de días omitidos y retención según capacidad de Neon. No inventar endpoints ni tablas antes de revisar cada integración.

## Reanudación de una sesión

Leer este AGENTS.md. Revisar `git status` y el código real. Continuar con el primer paso pendiente; no volver a generar la solución. Actualizar este archivo cuando cambien decisiones y al cerrar cada etapa. Nunca incluir secretos en documentación ni logs.

# Plan incremental — Pipeline ETL ALE

## Forma de trabajo

Avanzar una etapa a la vez, con cambios pequeños. Antes de cada etapa explicar qué problema resuelve y qué archivos tocará. Al terminar, proporcionar comandos exactos, resultado esperado y recorrido del código. Actualizar el estado aquí con la evidencia y el siguiente paso. Este documento no autoriza ejecutar de golpe todas las integraciones.

## Estado actual

- [x] Solución y cinco proyectos creados; template sustituido conservando referencias TI.
- [x] Ejemplo compartido ejecutado desde consola; sin acceso externo.
- [x] Guía de ejecución centrada en consola y contexto persistidos.
- [x] Recorrido básico completado; el usuario pidió avanzar directamente. Depuración diferida.
- [x] Etapa 2: consola calcula fechas y permite --date y --source; validada con comprobaciones de comportamiento; el script Python temporal fue eliminado a petición del usuario.
- [x] Alma: inspección real confirmada por el usuario (Loans 19 columnas, Renewals 18, Column0 incluida).
- [x] Extracción paginada implementada con filtro por fecha; comprobada con respuestas simuladas. El usuario ejecutó la extracción real y contrastó los datos con Analytics: confirmó coincidencia.
- [x] Persistencia PostgreSQL implementada; el usuario eligió dos tablas con columnas legibles.
- [x] El usuario validó la carga real en Neon y confirmó que sus resultados coinciden con la consola.
- [ ] Repetición real del mismo día en Neon: no confirmada explícitamente por el usuario; sí comprobada con PostgreSQL local.
- [x] Workflow inicial Alma → Neon validado; ahora se sustituye por un workflow conjunto.
- [x] Secretos configurados y verificados por nombre. El usuario confirmó ausencia de tarjeta/método de pago en la cuenta propietaria y autorizó usar cuota gratuita.
- [x] Primera ejecución remota exitosa: run 44371949917; D=2026-11-18, objetivo 2026-11-16, Loans=225 filas, Renewals=38 filas, job=39 segundos.
- [x] Programación diaria de Alma autorizada por el usuario: 22:00 America/Lima. Workflow usa cron 7 3 * * * en UTC; conserva dispatch manual.
- [x] Primera ejecución automática observada: run 44513732333, 2026-11-19 a 06:19 Lima. El workflow anterior tomó D=2026-11-19. Se corrigió el cron a UTC explícito y se protege la ventana nocturna; verificar próximo disparo.
- [x] Elogim: inspección y extracción local implementadas; pruebas con MariaDB temporal y usuario SELECT.
- [x] Elogim real: usuario confirmó conexión exitosa tras SSL Mode=Disabled y esquema: fecha_sesion DATETIME y cuatro VARCHAR, todos NOT NULL.
- [x] Persistencia Elogim implementada y probada con MariaDB→PostgreSQL temporales y datos ficticios.
- [x] El usuario corroboró Elogim en consola y Neon y confirmó coincidencia de valores.
- [ ] Repetición del día en Neon no confirmada expresamente; sí validada con servidores temporales.
- [x] Workflow manual Elogim → Neon validado: run44388911385, D=2026-11-18, objetivo2026-11-17, commit confirmado de1626 filas/5columnas, job35segundos.
- [x] El usuario autorizó horario único 22:00 America/Lima y una misma ejecución para todas las fuentes.
- [x] Workflow conjunto preparado: una fecha, un runner/build y cargas secuenciales independientes.
- [x] Workflows anteriores retirados del repo; API confirma ambos state=deleted. Solo ETL diario a Neon (id423069134) activo.
- [x] Prueba conjunta remota exitosa: run44390426509, D2026-11-18,sourceall, un job/build,47s; Loans225,Renewals38,Elogim1626 filas.
- [x] LibCal: OpenAPI interpretado y movido a docs/apis; catálogo14categorías/6campus configurado.
- [x] LibCal: cliente OAuth, inspección y extracción paginada implementados; solución Release y pruebas simuladas pasaron, revisión independiente sin hallazgos importantes en aquella etapa.
- [x] LibCal: usuario ejecutó extracción real D2026-11-18;790filas/14categorías, incluidas páginas adicionales.
- [x] LibCal: usuario compartió --inspect-libcal y confirmó las equivalencias del reporte: correo completo, estado de reserva, asistencia y cálculos locales.
- [x] LibCal: foto D completa en transacción y columnas finales en una sola tabla; pruebas PostgreSQL y HTTP simuladas pasaron.
- [x] LibCal: secretos presentes, carga real y workflow conjunto confirmados en run 44571809816.
- [x] Dos disparos nocturnos de Cloudflare cargaron las tres fuentes en Neon (runs 44979216277 y 45104131567).
- [ ] Contrastar en Neon el teléfono y participantes por categoría con LibCal.
- [ ] Observabilidad: mover al final, antes del cierre operativo.
- [ ] Functions y cambio de motor: diferidos.

## 1. Recorrido inicial — completado

El usuario conoce los fundamentos y pidió avanzar directamente. El ejemplo inicial y sus tipos/disparador fueron eliminados a petición del usuario. La consola utiliza ahora el flujo real de extracción/carga. Functions se conserva como host sin funciones, para retomar después.

## 2. Configuración y fechas reproducibles

Confirmar zona horaria, horario, significado del día por fuente y comportamiento de LibCal. Implementar fecha de referencia única por ejecución, con opción manual `--date YYYY-MM-DD` y selección `--source alma|libcal|elogim|all`. Sin fecha explícita, calcular D en la zona acordada. Validar argumentos y devolver código distinto de cero ante errores. No depender del huso horario del runner.

Colocar cálculo y coordinación en Application; parsing y salida en Console. Usar configuración tipada para los conectores cuando se definan. Documentar secretos locales excluidos de Git y equivalentes en GitHub Secrets. No incluir valores reales en ejemplos.

Validación: fechas D−2/D/D−1, cambio de mes/año, año bisiesto, zona horaria y argumentos inválidos. Establecer pruebas pequeñas con herramientas acordadas del proyecto; no instalar un framework por rutina.

Aceptación: la misma fecha manual produce las mismas fechas objetivo; todavía no consulta servicios reales.

## Observabilidad corporativa — diferida al final

No ejecutar ahora. El usuario pidió abordar primero las extracciones y dejar esta integración para la parte final del proyecto. La inspección de la DLL encontró dos extensiones AddOpenTelemetryObservability sobre IHostApplicationBuilder (ILogger y Serilog), con exportadores OTLP incondicionales. No se añadió referencia ni dependencia al proyecto. La herramienta de inspección se instaló solo en /tmp/pipeline-etl-tools, fuera del repositorio.

Inspeccionar API pública y dependencias de la DLL, contrastar registro con referencias TI y comprobar compatibilidad Linux/.NET 10. Integrar una sola configuración de logging/telemetría sin duplicar proveedores. Mantener salida local útil sin requerir un collector institucional inaccesible. Confirmar con TI cualquier requisito no deducible del template.

Registrar identificador de ejecución, fuente, fecha, duración, conteos y resultado; evitar credenciales y contenido sensible. Propagar cancelación y errores, sin declarar éxito ante una carga fallida.

Aceptación: diagnóstico local verificable y comportamiento documentado con/sin exportador configurado. Si la DLL impide ejecutar, registrar evidencia y resolver antes de sustituir convenciones corporativas.

## 4. Primer ETL completo hacia Neon

Elegir con el usuario la primera fuente y un conjunto concreto de datos. Revisar documentación vigente y una muestra anonimizada: autenticación, endpoint o consulta, filtros, fecha, paginación, cuotas, identificador estable y volumen. Las otras dos fuentes esperan.

Definir modelos y contratos específicos en Domain/Application; implementar cliente o consulta en Infrastructure. Transformar y guardar por lotes acotados. Definir esquema y migraciones PostgreSQL versionadas después de conocer los datos; persistencia específica fuera del caso de uso. Utilizar TLS y secretos externos. No crear un proveedor SQL Server todavía.

Acordar claves de unicidad y actualización para que repetir fuente/fecha no duplique datos. Elegir transacciones y avance del estado que no marquen un lote fallido como completo. Aplicar reintentos acotados solo a errores transitorios y operaciones seguras de repetir.

Validación: respuesta vacía, varias páginas/lotes, datos inválidos, autenticación fallida, timeout, error de escritura y repetición de la misma fecha. Verificar conteos e integridad con una muestra real autorizada en Neon.

Aceptación: una fuente ejecutada manualmente de extremo a extremo, repetible, con conteos verificables y errores visibles. Documentar comando exacto al implementar los argumentos.

## 5. Automatizar con GitHub Actions

Crear o elegir repositorio privado y propietario. El usuario o propietario configura accesos y secretos. No publicar referencias corporativas en un repositorio público para obtener minutos gratuitos.

Agregar CI de consola y verificaciones existentes. Agregar workflow con ejecución manual primero y programación después de confirmar horario; permitir fecha/fuente explícitas para reejecuciones. Usar runner Linux estándar, evitar solapamientos, establecer timeout y retención corta de artefactos; nunca subir datos extraídos. Medir duración, almacenamiento y cuota compartida del propietario. Verificar bloqueo del gasto facturable antes de activar el horario.

Mantener fechas reproducibles ante retrasos del scheduler; acordar cómo recuperar días omitidos, sin asumir que cron garantiza puntualidad. Reintentos no sustituyen esa recuperación.

Aceptación: ejecución manual remota equivalente a la local; credenciales no expuestas, fallos visibles y consumo compatible con presupuesto cero. Solo entonces activar horario.

## 6. Incorporar las otras dos fuentes

Repetir la etapa 4 una por una, reutilizando coordinación y persistencia donde exista una necesidad real. No forzar formatos distintos dentro de un modelo genérico. Consultas MariaDB de lectura parametrizadas, sin modificar el origen. Definir por fuente horario/fecha, unicidad, actualización y paginación.

Validación: éxito y fallo independiente por fuente, cancelación y repetición de fechas. Acordar si un fallo permite continuar las otras fuentes y representar siempre el resultado parcial como tal.

Aceptación: cada fuente tiene contrato, comando, validación y resultado documentados antes de incorporar la siguiente.

## 7. Operación y límites gratuitos

Medir volumen de Neon y duración total, revisar retención y límites actuales del plan. Documentar consulta de última ejecución, reejecución manual, recuperación de días y manejo de cambios de esquema. No almacenar estado importante solo en el disco temporal del runner. Definir alertas con herramientas disponibles, sin contratar servicios.

Aceptación: otra persona puede diagnosticar y repetir un día con la guía, sin duplicar datos y sin habilitar cobros.

## 8. Azure Functions y cambio de base de datos — diferidos

Retomar únicamente cuando se solicite. Functions invocará los mismos casos de uso, con configuración e identidad adecuadas. Validar localmente primero, luego desplegar si existe presupuesto autorizado. Desactivar la programación anterior antes de habilitar la nueva.

Si se decide cambiar de motor, adaptar proveedor, migraciones y SQL en Infrastructure; validar tipos, fechas, claves e integridad. Migrar datos requiere un procedimiento independiente; no es solo cambiar una cadena de conexión.

## Próxima sesión

Siguiente paso: contrastar en Neon la cobertura de teléfono y participantes de LibCal con reservas del origen. El disparo nocturno productivo por Cloudflare ya se verificó durante dos noches. La prueba manual conjunta del 2026-11-19 fue exitosa y aplicó las migraciones en Neon. La foto LibCal de la noche reemplaza la del mismo día; no se puede recuperar otro día por esta ruta. El usuario indicó retomar las validaciones al día siguiente.

## Operación y decisiones de ejecución

- Un workflow, runner ubuntu-24.04 y compilación Release única. Carga LibCal, Alma y Elogim como pasos independientes. Dispatch con fecha explícita y opción all/alma/elogim/libcal. Para históricos seleccionar alma o elogim.
- Cloudflare usa cron 7 3 * * * en UTC (22:07 Lima), fija D a partir del evento original, protege la ventana nocturna y llama al workflow con reference_date y source=all. GitHub productivo usa solo workflow_dispatch. Usar dispatch con fecha explícita para recuperar Alma/Elogim si el usuario lo solicita. LibCal valida de nuevo el día antes y después de consultar cada página. GitHub puede retrasar u omitir disparos.
- Los pasos de fuente continúan si otra fuente falla y el job queda fallido. El esquema se instala explícitamente con --init-db desde schema.sql. La ejecución nocturna no cambia la estructura. La carga de LibCal tiene límite de 10 min. --source all por sí solo sigue siendo planificación.
- Secretos confirmados solo por nombre: ALMA_API_KEY, ELOGIM_CONNECTION_STRING y NEON_CONNECTION_STRING. LIBCAL_CLIENT_ID y LIBCAL_CLIENT_SECRET también verificados por nombre el 2026-11-19. El usuario confirmó ausencia de tarjeta y autorizó cuota gratuita. No cambiar facturación.
- Workflows antiguos Alma422966584 y Elogim423059081 fueron eliminados; API confirma state=deleted. Intento de disable de Alma respondió403 por no estar activo; no es un bloqueo de permisos ni requiere aprobación/ampliar acceso. Solo ETL conjunto423069134 activo; evitar dos schedules. Grupo nuevo etl-neon, sin cancelar carga activa; timeout30min. Acciones fijadas por SHA, permisos contents:read, sin artefactos de datos.

## Referencia técnica por fuente

- Contrato LibCal suministrado por usuario: docs/apis/libcal-v1.1.openapi.yml. /space/bookings ignora fechas pasadas; guardia rechaza cualquier D distinta de hoy en Lima antes de consultar y después de cada respuesta (también evita cruces de medianoche). No prometer backfill histórico con esta ruta. /space/bookings/updates solo admite últimas24h y no tiene los mismos campos/parámetros, no implementarlo como fallback automático.
- LibCal query: dateD,days0 (un día; days1 amplía),lid/cid por categoría,page1..,limit100; continuar si count==limit, parar al recibir menos, max10000 con fallo explícito y guardia de página repetida. Flags form_answers/check_in_status/include_cancel/include_tentative/include_denied=1. Respuesta raíz array; OAuth object por schema, form-urlencoded client_credentials, Bearer con caché/expiry y una renovación al401. No seguir redirects de token/carga.
- LibCal categorías JSON en Console, copiado a output/publish,14categorías de6campus con nombres canónicos e identificadores usados para consultar. No contiene credenciales. --inspect-libcal consulta solo primera página hasta25/categoría y muestra unión de nombres/tipos, sin valores personales. --extract-libcal recorre todas las páginas y no escribe archivos ni base; valida cid/lid si vienen y solape del intervalo de usoD en Lima, fechas ISO con offset explícito.
- Formularios de referencia LibCal:8253 espacios grupales (q25458 teléfono,q25536 términos,q25459 correo integrante2,q25460 correo integrante3; booking_participant_2_email y booking_participant_3_email corresponden a q25459 y q25460);8254 computadoras/laptops y Kindle (q25458 teléfono,q25536 términos). LibCal devuelve check_in_status como estado(in/out/-), no inferir check-in/out timestamps. No derivar user_id desde email hasta validación universitaria; tampoco usar id de reserva como id de usuario. Las respuestas solicitadas se guardan en columnas propias. No persistir account, términos ni el JSON original.
- Alma API base https://api-na.hosted.exlibrisgroup.com; carpeta /shared/Universidad Continental 51UCCI_INST/Reports/API/ con Loans y Renewals. Analytics entrega 19 y 18 columnas, incluida Column0 (encabezado 0). Neon importa 18 y 17 columnas útiles, excluyendo Column0; no confundir COUNT(*) de filas de Renewals con SUM(renewals).
- Filtros is prompted: Loans usa "Loan Date"."Loan Date"; Renewals usa "Renewal Date"."Renewal Date". Se envía igualdad xsd:date, páginas de1000 y token inicial reutilizado hasta IsFinished. No reintentar automáticamente una continuación, pues puede avanzar el cursor; repetir el día completo si falla. Cada fila valida fecha objetivo.
- Elogim origen predeterminado elogim_continental.est_ezproxy. SELECT fecha_sesion,id_est,identificacion,bd y CASE tipo_consulta (pdf→Descarga,busqueda→Búsqueda,sesion→Acceso,resto sin cambiar). D−1 se filtra >= medianoche inicial y < medianoche siguiente con parámetros MySqlDbType.DateTime.
- Usuario confirmó fecha_sesion DATETIME y otros cuatro campos VARCHAR, todos NOT NULL; system_time_zone=-05,time_zone=SYSTEM. Tratar DATETIME como hora local Perú sin convertir a UTC; identificadores se conservan como texto, incluidos ceros iniciales.
- ELOGIM_CONNECTION_STRING con Server/Port/Database/User ID/Password; ELOGIM_TABLE opcional (default est_ezproxy). Usuario usa Dataflare con SSL Disabled; respetar SSL Mode explícito. Sin modo explícito, remoto predetermina VerifyFull. Diagnósticos seguros distinguen red, TLS no disponible, certificados y autenticación sin imprimir excepciones originales.
- PostgreSQL: etl.alma_loans, etl.alma_renewals y etl.elogim_sessions; PK por extraction_date,row_number sin asumir identificadores únicos. loaded_at UTC; fecha_sesion timestamp without time zone. Transacción de reemplazo por reporte/día, locks51001 (Alma) y51002 (Elogim), COPY en streaming. Fallo revierte solo la carga activa, no otras confirmadas; día vacío reemplaza con cero filas.
- PostgreSQL LibCal: única tabla etl.libcal_bookings; PK extraction_date,row_number sin deduplicar bookId/id. COPY y lock51003, una transacción para todas las categorías de D; fallo/cancelación/EOF incompleto revierte toda la foto. Tabla con campos seleccionados, nombres canónicos de campus/categoría del catálogo y sin JSON persistido. booking_starts_at/booking_ends_at timestamptz, loaded_at UTC. No reemplazar histórico desde API que ignora fechas pasadas.
- Reporte LibCal: 006 materializa los estados, el teléfono y los participantes calculados en instalaciones anteriores, y retira raw_payload y las columnas genéricas. user_email es el correo canónico; booking_account_email se retiró por duplicado. Recurso=item_name, nombres=firstName/lastName. Fecha, hora de inicio/fin y duración se calculan al consultar booking_starts_at/booking_ends_at. Convertir los instantes a Lima antes de agrupar por fecha u hora y obtener la duración de su diferencia. El cliente convierte los estados confirmados/cancelados/tentativos/denegados en sus rótulos, deja estados desconocidos y traduce in/out→Asistió y '-'→Sin registro de asistencia. Teléfono=q25458, texto sin espacios en los extremos (conserva ceros si llegan como texto); participantes=q25459 y q25460 solo para Espacios grupales. Vacíos o ausentes se guardan como NULL. IDs de reservas, campus y categoría, y metadatos de carga se conservan.
- PostgresDatabase usa un solo schema.sql para instalaciones nuevas. --init-db crea objetos ausentes y conserva filas; los ajustes de una base existente se aplican explícitamente. NEON_CONNECTION_STRING usa formato .NET (no URL); TLS remoto VerifyFull.

## Evidencia de validación

- Usuario contrastó Alma y Elogim en consola y Neon y confirmó coincidencia. No atribuir estos contrastes al agente.
- Alma remota exitosa: https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44371949917; D2026-11-18, objetivo2026-11-16, Loans225 y Renewals38 filas, job39s.
- Elogim remota exitosa: https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44388911385; D2026-11-18, objetivo2026-11-17,1626filas/5columnas, job35s. Logs emitidos tras commit; sin SQL independiente por el agente.
- Pruebas fuera del repo con MariaDB SELECT-only y PostgreSQL temporales: límites del día, CASE, ceros/nulos/vacíos/Unicode/microsegundos, repetición/cargas concurrentes, rollback ante errores/fecha/esquema/cancelación/NULL, día vacío y días vecinos intactos. Regresión de Alma también pasó. No conservar suite Python ni instalar frameworks por rutina.
- Primera ejecución automática 44513732333 verificada: disparo 06:19 Lima y D=2026-11-19 en logs, sin LibCal. Workflow conjunto validado con actionlint, scripts Bash temporales, revisión independiente sin hallazgos importantes y prueba remota success run44390426509. Política de fallo parcial configurada según documentación GitHub; no se provocaron fallos contra los servicios reales.

## Repositorio

Remoto autorizado: git@github.com:hubinformacion/pipeline-etl-ale.git. Rama principal: main. El usuario autorizó commit inicial y primer push. El usuario autorizó ejecuciones manuales y programación diaria conjunta 22:00 America/Lima de las fuentes implementadas; no habilitar servicios ni consumo facturable.

## Revisión LibCal y horario — 2026-11-19

La API devuelve preguntas de formulario como propiedades q<ID> en cada reserva con form_answers=1. /space/question/{id} devuelve metadatos de preguntas y /space/categories/{id}?details=1 permite comprobar formid por ubicación. La inspección anterior confirmó presencia de las claves, no sus valores ni cobertura. Conservar identificadores de preguntas documentados y validar cobertura real por categoría antes de inferir que un dato faltante es un error del conector.

Verificado por el agente: compilación Release de consola, actionlint, simulación de 22:00/23:00/06:00 y rerun, fecha/fuente manual, migración sobre estructura anterior con una fila ficticia, init repetido, preservación de fila y valores originales, COPY posterior, zona de sesión distinta y rechazo CLI de fechas pasadas/futuras. El agente no tuvo credenciales de LibCal/Neon en su entorno, ni secretos LibCal en GitHub. La primera ejecución con tres fuentes y el próximo disparo siguen pendientes.

## Ajuste de formularios y esquema final

El usuario volvió a pedir las 16 columnas de reporte, incluyendo booking_account_email, más teléfono, participantes e IDs útiles. Se retiró raw_payload de Neon a petición expresa y se descartaron account, términos y demás campos ajenos al reporte. --inspect-libcal cuenta respuestas por q<ID> y categoría en la primera página sin imprimir los valores. Referencia de formularios confirmada por el catálogo original y el OpenAPI para la estructura; falta verificar cobertura real de cada campus. El catálogo original muestra FormId=0 para computadoras de Los Olivos, señal que requiere confirmar en el API antes de esperar teléfono en ese campus.

Verificado: compilación Release, migración repetida desde esquema anterior con datos, conservación de teléfono/participante, eliminación del JSON y columnas genéricas, y carga con API ficticia hacia PostgreSQL de reservas grupales y de computadora. Carga real en Neon y confirmación de preguntas activas siguen pendientes. No se cambió el horario en esta etapa.

## Períodos de reporte

El usuario autorizó retirar los períodos derivados. 007 elimina booking_month y "booking_month#" sin afectar las filas. No había columnas equivalentes que retirar en Alma ni Elogim. Para reportes usar loan_date, renewal_date, fecha_sesion y booking_date. Agrupar por inicio de mes (incluye año) y formatear la etiqueta en el reporte. Compilación Release y migración repetida sobre PostgreSQL local con dos reservas verificadas. Aplicación en Neon y validación de la ejecución conjunta nocturna siguen pendientes.

Migración 008: elimina source_column0 de las dos tablas Alma. Analytics mantiene sus IDs originales; el importador ignora Column0 y sigue validando Column1 en adelante. Verificado localmente con filas existentes en las dos tablas y dos inicializaciones consecutivas.

Prueba manual conjunta de GitHub Actions https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44571809816: commit 254384f, D=2026-11-19, source=all, job 51 segundos. --init-db success en Neon. Logs de confirmación: LibCal D=2026-11-19, 511 filas y 14 categorías; Loans D−2=2026-11-17, 247 filas/18 columnas; Renewals misma fecha, 19 filas/17 columnas; Elogim D−1=2026-11-18, 1543 filas/5 columnas. Contraste de valores en Neon pendiente; evidencia de escritura proviene de los mensajes posteriores al commit. Workflow 423069134 activo, 03:00 UTC del 20/11 equivale a 22:00 Lima del 19/11.

## Preparación para la primera captura nocturna

Por decisión del usuario, retirar los datos de prueba antes de la captura nocturna. El alcance del vaciado autorizado son las cuatro tablas ETL y etl.load_status. Conservar el esquema, los demás objetos y etl.schema_migrations. No dejar un comando de vaciado en main ni un workflow persistente.

--init-db registra cada script por nombre en etl.schema_migrations y serializa la inicialización con lock51000. Los scripts existentes idempotentes se ejecutan una última vez al crear este registro. Desde entonces agregar archivos nuevos para cambios de esquema. etl.load_status almacena la última carga confirmada por dataset y fecha, con su conteo y hora; se escribe en la transacción de cada reemplazo. Cero es una carga confirmada sin filas; ausencia significa que no hay confirmación registrada. Un fallo se consulta en Actions y conserva cualquier carga previa confirmada.

Vaciado real confirmado por el run manual https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44573855585. Al confirmar la transacción, las cuatro tablas de datos y load_status quedaron en cero; schema_migrations conservó 8 entradas. Rama temporal de operación retirada. Workflow principal activo y cron 03:00 UTC (22:00 Lima). Próxima validación: comprobar la captura nocturna y sus cuatro registros en etl.load_status, y contrastar formularios LibCal con el origen.

El disparo previsto para el 19/11 a las 22:00 Lima se creó el 20/11 a las 05:52 Lima (run 44722812308). El job inició cuatro segundos después y la guarda rechazó la fecha antes de cargar. Se movió el cron al minuto 07 siguiendo la recomendación de GitHub para reducir retrasos, manteniendo la ventana y la protección LibCal. Pendiente observar un disparo real del cron nuevo.

Recuperación manual D=2026-11-19 confirmada por logs tras commit: Alma (run 44826269608) cargó 247 préstamos y 19 filas de renovaciones para el 17/11. Elogim (run 44826274539) cargó 1543 filas para el 18/11. LibCal del 19/11 queda sin captura por la limitación histórica de la ruta. Cron de las 22:07 Lima publicado y activo; verificar el próximo disparo y validar LibCal.

## Diagnóstico del disparador programado — 2026-11-20 y 21

El usuario solicitó vaciar los datos de prueba y validar el inicio automático sin disparar cargas manuales. Run 44828172468 confirmó cero filas en las cuatro tablas ETL y load_status, con las 8 migraciones conservadas. Pruebas de diagnóstico del 20/11 sin escrituras en Neon (hora Lima): 18:03 UTC implícito → 22:49:49 (44863080518); 18:11 America/Lima → 22:55:48 (44863555005); 18:19 Etc/UTC → 23:01:53 (44864044800). El ETL de las 22:07 llegó el 21/11 a las 05:53:06 (44896428380) y la protección detuvo todas las cargas. GitHub creó cada ejecución solo 3–4 s antes del arranque del job. La demora se observa en la creación del run; no hay un desfase fijo ni evidencia del motivo interno específico. Las pruebas temporales previas de las 17:30/17:45/17:50 no tienen un run visible. Se retiró el workflow temporal de diagnóstico ya completado. El cron productivo mantiene las 22:07 Lima. Si la captura de LibCal necesita cumplir la ventana nocturna, evaluar un disparador externo con fecha explícita; no habilitarlo ni hacer capturas manuales por iniciativa propia.

## Prueba de disparador externo con Cloudflare

Se publicaron el Worker de diagnóstico en ops/cloudflare/etl-dispatch-probe.mjs y el workflow etl-dispatch-probe.yml. El Worker envía únicamente los instantes UTC del cron y de su propio arranque, vía workflow_dispatch a main. El workflow registra las horas y diferencias sin checkout, secretos ni acceso a Neon. Verificado localmente: sintaxis JS, actionlint, solicitud simulada aceptada y errores de token ausente/HTTP403. Falta que el usuario cree el Worker Free, configure GITHUB_DISPATCH_TOKEN como secreto con token de granularidad fina de la cuenta propietaria hubinformacion (solo este repo, Actions read/write) y compruebe una ejecución real. No cambiar el cron productivo ni conectar la prueba a las cargas hasta validar el disparo.

Primera invocación programada real del Worker: Cloudflare sí entregó el evento; la opción redirect:error no existe en Workers y fetch falló antes de contactar GitHub. Los dos logs compartían requestId. Cambiado a redirect:manual y se mantiene el rechazo de respuestas no exitosas. Reproducción simulada de la restricción del runtime y comprobación de éxito y HTTP302 sin seguir la redirección. Falta desplegar la versión corregida en el Worker y verificar el primer dispatch real.

El diagnóstico Cloudflare → GitHub quedó confirmado en run 44916853990 (success). El Worker arrancó a las 10:00:33 Lima, GitHub aceptó a las 10:00:34 y no hubo cargas de datos. Retirados el workflow y el código del diagnóstico. La fuente vigente del Worker productivo es scripts/cloudflare-etl-trigger.mjs; configurarlo desde el panel con el secreto existente GITHUB_DISPATCH_TOKEN y cron 7 3 * * * UTC (22:07 Lima). Ignora otros crons, calcula reference_date en Lima del disparo previsto, bloquea el inicio antes del horario y fuera de la noche prevista, y llama etl-neon.yml en main con source=all. Prueba local simulada pasó incluyendo cruce UTC, medianoche Lima y redirección. El usuario confirmó la configuración y se retiró el cron nativo productivo en GitHub, conservando workflow_dispatch. No lanzar cargas manuales para comprobarlo.

## Comparación nocturna del 21 de noviembre

Cloudflare muestra Runs at 03:07 AM porque el cron se evalúa en UTC. Es el mismo instante que las 22:07 del día anterior en Lima. El Worker ya configurado no requiere redeploy por mover su fuente a scripts/cloudflare-etl-trigger.mjs. En GitHub se dejó un workflow de diagnóstico separado, etl-github-schedule-check.yml, para el 21/11/2026 a las 22:07 Lima con zona explícita; no lee fuentes ni escribe en Neon. Comparar la hora de los dos disparos y retirar el diagnóstico después de observar el resultado (la expresión cron carece de campo año). Una ejecución puntual por sí sola no demuestra que el retraso intermitente esté resuelto.

Resultado verificado el 23/11: Cloudflare inició el Worker las noches D=21/11 y D=22/11 a las 22:07:56.978 y 22:07:57.154 Lima; GitHub aceptó a las 22:07:58 en ambos casos. Runs 44979216277 y 45104131567 terminaron success. Logs de carga confirmada para D=21: LibCal467, Loans185, Renewals17 y Elogim1243 filas. Para D=22: LibCal237, Loans180, Renewals23 y Elogim1179 filas. Sin contraste SQL independiente de los registros. GitHub muestra estos runs como manuales por el evento workflow_dispatch, que Cloudflare invocó automáticamente. El cron nativo de comparación (run 45015787624) estaba previsto para el 21/11 a las 22:07 Lima y se ejecutó el 22/11 a las 06:07:57, con demora de 28857 s (8 h 57 s). Su resultado verde solo indica que pudo registrar la medición. Mantener Cloudflare para programar y GitHub Actions para ejecutar el ETL; se retiró el workflow de comparación y se preservó el historial.

## Prueba puntual de compensación del cron nativo — 2026-11-24

A petición del usuario, probar si un adelanto de 8 h acerca el cron nativo a las 22:07 Lima. etl-github-offset-test.yml programa el 24/11 a las 14:07 Lima (19:07 UTC), mide inicio y desviación frente a las 22:07, sin credenciales ni cargas. Cloudflare conserva la operación productiva. Revisar el 25/11 y retirar el diagnóstico tras observar el resultado. El adelanto prueba una hipótesis basada en el último retraso (8 h 57 s), no es una conversión de zona ni un plazo garantizado. Comparar con los demás retrasos antes de decidir cualquier cambio de disparador.

Resultado verificado el 25/11: run 45536799081, cron previsto el 24/11 a las 14:07 Lima, ejecución creada a las 21:59:31 y medición a las 21:59:35. Demora 7 h 52 min 35 s; 7 min 25 s antes del objetivo de las 22:07 y 25 s antes del inicio de la ventana nocturna. No hay base para asumir una compensación fija. Se retira el diagnóstico. Cloudflare conserva el cron productivo. Runs 45317336428 (D=23/11, creado 22:07:58 Lima) y 45537816984 (D=24/11, creado 22:07:59 Lima) confirmaron todas las cargas. Conteos LibCal/Loans/Renewals/Elogim: 1011/98/15/1001 y 1049/81/5/1151. Validación basada en logs de carga, sin nueva consulta SQL a Neon.

## Contrato de datos para el esquema inicial de producción

El usuario confirmó que este repositorio se limita al ETL y que la reportería será otra solución. Conservar etl.load_status, un identificador de lector en Alma Loans para un ranking, cobertura de usuarios distintos en Elogim y teléfono/participantes en LibCal para detalle. En Loans se propone user_primary_identifier y separar el uso interno; la salida actual asigna el literal Uso interno a esos casos. En Elogim se leen id_est e identificacion y no hay una columna de correo explícita; falta confirmar cuál es un ID estable y si identificacion contiene correos o valores mixtos. No fusionar ni descartar identificadores basándose en su nombre.

Para Alma Renewals, SUM(renewals) por renewal_date mide renovaciones manuales o mediante autoservicio; no incluye Auto Renewals. Ex Libris documenta que esa segunda medida de Fulfillment no se puede cruzar con Renewal Date: https://knowledge.exlibrisgroup.com/Alma/Product_Documentation/010Alma_Online_Help_(English)/080Analytics/Alma_Analytics_Subject_Areas/Fulfillment. Context7 no devolvió una fuente de Ex Libris correspondiente. No atribuir una diferencia concreta al ETL o a las autorrenovaciones sin comparar una muestra del mismo período y filtros.

LibCal GET /space/bookings admite internal_notes=true, con arreglo de notas que contiene name, created y note. El contrato también ejemplifica nickname como etiqueta de reserva. Disambiguar qué campo de la interfaz necesita el usuario y, si son notas internas, qué elegir cuando haya varias. Si se aprueba una sola nota del origen, proponer booking_note nullable y guardar solo el texto de la última nota según created, con desempate acordado si no hay hora. No activar el parámetro ni importar contenido sensible hasta resolver ese punto.

Criterio para el esquema base: conservar datos temporales originales con su semántica, calcular año/mes/día de semana/hora agrupada en consultas y no guardar copias meramente de presentación. Mantener extraction_date y loaded_at por su función operativa. Revisar uso de columnas derivadas existentes antes de excluirlas del esquema base. No cambiar Neon ni vaciar datos durante esta definición.

## Consolidación del esquema ETL

El usuario confirmó que Uso interno se conserva tal como llega de Alma; los filtros del ranking corresponden a reportería. Elogim id_est contiene el correo y se guarda en user_email de Neon, mientras identificacion contiene DNI o código de usuario y se mantiene como texto. No se renombra ninguna columna en la base origen. LibCal solicita internal_notes=true y conserva solo booking_note con el texto no vacío más reciente según created. En empate temporal usa el último elemento recibido; si no hay nota guarda NULL. La inspección muestra conteos, nunca el texto. Las capturas históricas no pueden enriquecerse con esta ruta. Se quita booking_hour y se deriva en consultas; ya no se guardaban año ni mes. Se conservan fechas de actividad y los campos LibCal ya acordados de inicio, fin y duración. Se conserva renewals por contrato de Analytics y se medirá su distribución real antes de decidir si hay redundancia.

Se reemplazó el historial de scripts de desarrollo por schema.sql, sin schema_migrations en instalaciones nuevas. etl.load_status se conserva. Las cargas programadas no corren --init-db. Verificado localmente: compilación Release, actionlint, --init-db repetido en base vacía, COPY de Elogim y LibCal sobre ese esquema, nota reciente/empate/ausente simulados, y alineación de la estructura anterior sin perder filas; los catálogos de columnas de ambas bases convergen. Ajustar la base activa explícitamente antes de habilitar los cambios de carga.

El run 45668681955 inspeccionó LibCal sin persistir notas ni mostrar su contenido: internal_notes llegó como Array y se encontraron 29 reservas con nota en las primeras páginas de 14 categorías. En Neon, la consulta directa de Alma contó 60 filas históricas con renewals=1, suma 60, sin nulos ni valores mayores que uno. La muestra no garantiza una fila por evento para futuros resultados; se conserva la medida aditiva para evitar alterar el contrato de Analytics. En la misma operación se alineó Neon mediante una transacción explícita sin borrar filas de negocio ni load_status: id_est pasó a user_email, se añadió booking_note nullable, se retiró booking_hour y se eliminó el registro de migraciones de desarrollo. Cambios de carga publicados en 85d09ed después de alinear la base; se retiró la rama operacional. No se lanzó carga manual de las fuentes. Verificar la siguiente ejecución nocturna con el esquema base.

## Simplificación de columnas de LibCal

El usuario confirmó que conserva nombres, correos y teléfonos. Se retiraron booking_account_email, booking_date, booking_start, booking_end y booking_duration por derivarse sin pérdida. Los instantes canónicos son booking_starts_at y booking_ends_at. Validado localmente que las expresiones de fecha/hora Lima y duración en minutos reproducen exactamente las columnas anteriores, y que una carga sobre el esquema nuevo guarda la reserva y sus notas. Se conservan extraction_date y row_number como clave de la foto del día; extraction_date puede diferir de la fecha local de inicio en LibCal. loaded_at en los datos registra el inicio de su escritura; load_status registra cuándo se confirma la carga por tabla/día. Las tres columnas suman 20 bytes de datos por fila antes de alineación e índice; loaded_at por fila es prescindible si basta trazabilidad por lote.

Alineación real confirmada en run 45703942280: la transacción conservó 2764 reservas de LibCal y retiró solo las cinco columnas derivadas acordadas. Cambios del importador publicados en a2ffff7 después de alinear Neon. La prueba con reserva ficticia cruzando medianoche confirmó que la fecha local de inicio puede diferir de extraction_date y que la duración se mantiene correcta con la sesión en UTC. Falta observar la siguiente carga nocturna; no se disparó carga manual.

## Unicidad de identificadores de origen

Diagnóstico de solo lectura verificado en el run 45705676053 del 25/11. En Neon, Alma Loans tiene 544 filas con item_loan_id presente y distinto en las 544. Ex Libris documenta ITEM_LOAN_ID como clave del préstamo. Renewals tiene 60 filas con 60 IDs distintos, pero item_loan_id sigue identificando el préstamo y este puede renovarse más de una vez. No imponer la unicidad para renovaciones a partir de esta muestra.

LibCal tiene 2764 filas, 2713 booking_id distintos (2727 pares extraction_date/booking_id) y 2759 source_row_id distintos. El par extraction_date/source_row_id es distinto en las 2764; considerar ese par para reemplazar row_number solo tras confirmar su unicidad por captura en el contrato. Ambos campos vienen del origen y booking_id es el código de reserva. extraction_date delimita la foto que se reemplaza.

Elogim tiene 4574 filas, 779 correos distintos y 4469 combinaciones distintas de los cinco campos extraídos. SHOW INDEX en MariaDB confirmó que id es la única columna de la PK del origen y el SELECT actual no la extrae. Es candidata para reemplazar row_number si se incorpora a la extracción y se define cómo incorporar el ID a las filas históricas. No deduplicar por correo ni por los cinco campos. Este diagnóstico no cambió columnas, claves ni datos. Si basta la hora de confirmación por lote, loaded_at se puede retirar de las cuatro tablas de datos conservándolo en etl.load_status.
