# Pipeline ETL ALE

Pipeline ETL en C# y .NET 10 para integrar datos de Alma/Analytics, Springshare/LibCal y Elogim/MariaDB. El destino inicial será PostgreSQL en Neon.

Desarrollo incremental con VS Code y WSL2. Incluye extracción paginada de Alma/Analytics con carga transaccional en PostgreSQL, e inspección y extracción de Elogim/MariaDB desde consola. La lógica se organiza en Domain, Application e Infrastructure; Azure Functions queda reservado para una etapa posterior.
