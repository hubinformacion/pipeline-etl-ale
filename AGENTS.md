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

Alma: D−2; LibCal: D; Elogim: D−1. Interpretación provisional: un día calendario específico por origen, no un rango acumulado. America/Lima y un único día calendario por fuente fueron confirmados por el usuario. Horario único confirmado: 22:00 America/Lima; detalles de LibCal siguen pendientes. Alma ya tiene filtros y columnas confirmados.

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
- [x] Programación diaria de Alma autorizada por el usuario: 22:00 America/Lima. Workflow usa cron 0 22 * * * con timezone IANA; conserva dispatch manual.
- [ ] Verificar primera ejecución automática tras el horario.
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
- [x] LibCal: cliente OAuth, inspección y extracción paginada implementados; solución Release y pruebas simuladas pasaron, revisión independiente sin hallazgos importantes; sin persistencia ni workflow aún.
- [ ] LibCal: usuario debe configurar credenciales locales e inspeccionar/contrastar respuesta real.
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

Siguiente paso: usuario debe ejecutar --inspect-libcal con LIBCAL_CLIENT_ID/LIBCAL_CLIENT_SECRET locales, compartir solo campos/tipos/conteos y luego contrastar --extract-libcal con LibCal. Base https://continental.libcal.com, token /1.1/oauth/token, GET /1.1/space/bookings. Después definir persistencia legible e incorporar al workflow conjunto, conservando independencia por fuente. El usuario aprobó foto de D a22:00Lima: biblioteca cierra21:00 y confirma no habrá nuevos registros después de22:00. No cambiar D a D−1 para LibCal. Workflow conjunto publicado (commit57c1705) y prueba https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44390426509 verificada como success; referencia2026-11-18, sourceall, un job/build y47s. Confirma Loans225/Renewals38 para2026-11-16 y Elogim1626 para2026-11-17. Ambos pasos y resumen terminaron success. No inventar campos ni identificadores de usuarios; cierres y foto22:00 ya confirmados para LibCal.

## Operación y decisiones de ejecución

- Un workflow, un runner ubuntu-24.04, preparación y compilación Release de consola una vez; cargas secuenciales Alma y Elogim a las 22:00 America/Lima. Mantener dispatch manual con fecha D explícita y choice all/alma/elogim.
- La fecha D se fija una vez al inicio y se comparte por GITHUB_OUTPUT; schedule calcula hoy en Lima. Alma carga D−2, Elogim D−1. Para recuperar días anteriores usar dispatch, no recalcular con un rerun de schedule de otro día.
- El usuario eligió continuar la otra fuente si falla una, pero marcar el job fallido si cualquier carga falla. Cada carga tiene if !cancelled() y build.outcome=success, sin continue-on-error. Resumen por fuente al final. Si falla preparación/compilación o se cancela, no cargar fuentes.
- Son dos invocaciones de la misma consola dentro del job conjunto (--load-alma y --load-elogim). --source all por sí solo sigue siendo planificación de fechas, no carga.
- Secretos existentes verificados solo por nombre: ALMA_API_KEY, ELOGIM_CONNECTION_STRING y NEON_CONNECTION_STRING. No leer ni pedir valores. hubinformacion es cuenta personal; acceso actual push, sin admin de facturación. Usuario confirmó ausencia de tarjeta y autorizó cuota gratuita. No cambiar facturación.
- Workflows antiguos Alma422966584 y Elogim423059081 fueron eliminados; API confirma state=deleted. Intento de disable de Alma respondió403 por no estar activo; no es un bloqueo de permisos ni requiere aprobación/ampliar acceso. Solo ETL conjunto423069134 activo; evitar dos schedules. Grupo nuevo etl-neon, sin cancelar carga activa; timeout30min. Acciones fijadas por SHA, permisos contents:read, sin artefactos de datos.

## Referencia técnica por fuente

- Contrato LibCal suministrado por usuario: docs/apis/libcal-v1.1.openapi.yml. /space/bookings ignora fechas pasadas; guardia rechaza D anterior a hoyLima antes de consultar y después de cada respuesta (también evita cruces de medianoche). No prometer backfill histórico con esta ruta. /space/bookings/updates solo admite últimas24h y no tiene los mismos campos/parámetros, no implementarlo como fallback automático.
- LibCal query: dateD,days0 (un día; days1 amplía),lid/cid por categoría,page1..,limit100; continuar si count==limit, parar al recibir menos, max10000 con fallo explícito y guardia de página repetida. Flags form_answers/check_in_status/include_cancel/include_tentative/include_denied=1. Respuesta raíz array; OAuth object por schema, form-urlencoded client_credentials, Bearer con caché/expiry y una renovación al401. No seguir redirects de token/carga.
- LibCal categorías JSON en Console, copiado a output/publish,14categorías de6campus con SourceName/FormId. No contiene credenciales. --inspect-libcal consulta solo primera página hasta25/categoría y muestra unión de nombres/tipos, sin valores personales. --extract-libcal recorre todas las páginas y conserva PayloadJson completo, no escribe archivos ni base; valida cid/lid si vienen y solape del intervalo de usoD en Lima, fechas ISO con offset explícito.
- Formularios de referencia LibCal:8253 espacios grupales (q25458 teléfono,q25536 términos,q25459 correo integrante2,q25460 correo integrante3; booking_form_answer_1/2 corresponden q25459/q25460);8254 computadoras/laptops y Kindle (q25458 teléfono,q25536 términos). LibCal devuelve check_in_status como estado(in/out/-), no inferir check-in/out timestamps. No derivar user_id desde email hasta validación universitaria; tampoco usar id de reserva como id de usuario. Conservar account/email y respuestas originales.
- Alma API base https://api-na.hosted.exlibrisgroup.com; carpeta /shared/Universidad Continental 51UCCI_INST/Reports/API/ con Loans y Renewals. Conservar las 19 y 18 columnas, incluida Column0; no confundir COUNT(*) de filas de Renewals con SUM(renewals).
- Filtros is prompted: Loans usa "Loan Date"."Loan Date"; Renewals usa "Renewal Date"."Renewal Date". Se envía igualdad xsd:date, páginas de1000 y token inicial reutilizado hasta IsFinished. No reintentar automáticamente una continuación, pues puede avanzar el cursor; repetir el día completo si falla. Cada fila valida fecha objetivo.
- Elogim origen predeterminado elogim_continental.est_ezproxy. SELECT fecha_sesion,id_est,identificacion,bd y CASE tipo_consulta (pdf→Descarga,busqueda→Búsqueda,sesion→Acceso,resto sin cambiar). D−1 se filtra >= medianoche inicial y < medianoche siguiente con parámetros MySqlDbType.DateTime.
- Usuario confirmó fecha_sesion DATETIME y otros cuatro campos VARCHAR, todos NOT NULL; system_time_zone=-05,time_zone=SYSTEM. Tratar DATETIME como hora local Perú sin convertir a UTC; identificadores se conservan como texto, incluidos ceros iniciales.
- ELOGIM_CONNECTION_STRING con Server/Port/Database/User ID/Password; ELOGIM_TABLE opcional (default est_ezproxy). Usuario usa Dataflare con SSL Disabled; respetar SSL Mode explícito. Sin modo explícito, remoto predetermina VerifyFull. Diagnósticos seguros distinguen red, TLS no disponible, certificados y autenticación sin imprimir excepciones originales.
- PostgreSQL: etl.alma_loans, etl.alma_renewals y etl.elogim_sessions; PK por extraction_date,row_number sin asumir identificadores únicos. loaded_at UTC; fecha_sesion timestamp without time zone. Transacción de reemplazo por reporte/día, locks51001 (Alma) y51002 (Elogim), COPY en streaming. Fallo revierte solo la carga activa, no otras confirmadas; día vacío reemplaza con cero filas.
- PostgresDatabase comparte configuración y scripts001+002; --init-db es idempotente, no borra datos. NEON_CONNECTION_STRING usa formato .NET (no URL); TLS remoto VerifyFull.

## Evidencia de validación

- Usuario contrastó Alma y Elogim en consola y Neon y confirmó coincidencia. No atribuir estos contrastes al agente.
- Alma remota exitosa: https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44371949917; D2026-11-18, objetivo2026-11-16, Loans225 y Renewals38 filas, job39s.
- Elogim remota exitosa: https://github.com/hubinformacion/pipeline-etl-ale/actions/runs/44388911385; D2026-11-18, objetivo2026-11-17,1626filas/5columnas, job35s. Logs emitidos tras commit; sin SQL independiente por el agente.
- Pruebas fuera del repo con MariaDB SELECT-only y PostgreSQL temporales: límites del día, CASE, ceros/nulos/vacíos/Unicode/microsegundos, repetición/cargas concurrentes, rollback ante errores/fecha/esquema/cancelación/NULL, día vacío y días vecinos intactos. Regresión de Alma también pasó. No conservar suite Python ni instalar frameworks por rutina.
- Primera ejecución automática diaria todavía no observada. Workflow conjunto validado con actionlint, scripts Bash temporales, revisión independiente sin hallazgos importantes y prueba remota success run44390426509. Política de fallo parcial configurada según documentación GitHub; no se provocaron fallos contra los servicios reales.

## Repositorio

Remoto autorizado: git@github.com:hubinformacion/pipeline-etl-ale.git. Rama principal: main. El usuario autorizó commit inicial y primer push. El usuario autorizó ejecuciones manuales y programación diaria conjunta 22:00 America/Lima de las fuentes implementadas; no habilitar servicios ni consumo facturable.
