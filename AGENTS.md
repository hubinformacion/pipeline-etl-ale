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

Alma: D−2; LibCal: D; Elogim: D−1. Interpretación provisional: un día calendario específico por origen, no un rango acumulado. America/Lima y un único día calendario por fuente fueron confirmados por el usuario. Hora y detalles de las fuentes restantes siguen pendientes. Alma ya tiene filtros y columnas confirmados.

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
- [ ] Persistencia en Neon: pendiente de acordar esquema y claves.
- [ ] Automatización y otras fuentes: pendientes.
- [ ] Observabilidad: mover al final, antes del cierre operativo.
- [ ] Functions y cambio de motor: diferidos.

## 1. Comprender y depurar el ejemplo actual

Objetivo: comprender solución, proyecto, dependencia, punto de entrada, clase y resultado. Seguir Program → DemoPipeline → RunSummary. Compartir en el chat los comandos exactos y ejecutarlos, colocar un breakpoint y observar la llamada. Añadir configuración de depuración de consola si hace falta, sin activar Functions.

Aceptación: el usuario puede ejecutar el ejemplo y explicar quién inicia el proceso y dónde irá la lógica. No añadir fuentes ni persistencia todavía.

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

Siguiente etapa: primera extracción completa hacia Neon. El usuario eligió Alma/Analytics como primera fuente. Ruta confirmada: /shared/Universidad Continental 51UCCI_INST/Reports/API/ con reportes Loans y Renewals. URL base: https://api-na.hosted.exlibrisgroup.com. El usuario requiere todas las columnas y confirma filtros de fecha is prompted. Campos confirmados: Loans usa "Loan Date"."Loan Date"; Renewals usa "Renewal Date"."Renewal Date". Ambos están is prompted. Documentación oficial revisada: https://developers.exlibrisgroup.com/alma/apis/analytics/ (respuesta XML, paginación con token). No pedir credenciales por chat. La consola permite --inspect-alma (primera página, 25 filas, sin filtro); el usuario confirmó éxito real y columnas. --extract-alma [--date YYYY-MM-DD] consulta ambos reportes para D−2 con igualdad xsd:date, páginas de 1000 filas y token estable hasta IsFinished=true. Conserva todas las columnas como diccionarios por ColumnN, esquema y texto original, distinguiendo null/vacío. Application valida cada fecha devuelta antes de contar el lote; sin escritura ni exportación de valores todavía. ALMA_API_KEY se recibe por variable de entorno y se envía en Authorization, nunca en URL. No hacer reintentos automáticos de continuación: una solicitud puede avanzar el cursor remoto. Si falla, marcar ejecución incompleta y repetir desde la primera página. El usuario confirmó que la extracción real coincide con los resultados contrastados en Analytics. La etapa siguiente es definir esquema/claves y cargar Neon sin duplicar datos. El comando --date es fecha de referencia D, no fecha objetivo. DemoPipeline conserva el cambio de mensaje del usuario y sigue disponible para la entrada Functions diferida. No implementar fuentes sin definir sus datos.

## Repositorio

Remoto autorizado: git@github.com:hubinformacion/pipeline-etl-ale.git. Rama principal: main. El usuario autorizó commit inicial y primer push. No activar workflows ni servicios facturables.
