using Microsoft.Extensions.Options;
using System;
using System.IO;

namespace BetSniffer.Api.Core.Services
{
    /// <summary>
    /// Serviço que gerencia dinamicamente a connection string baseado na configuração.
    /// Suporta LocalDB (desenvolvimento) e SQL Server (produção).
    /// </summary>
    public class DatabaseConnectionService
    {
        private readonly Configuration.DatabaseSettings _databaseSettings;
        private readonly IConfiguration _configuration;

        public DatabaseConnectionService(
            IOptions<Configuration.DatabaseSettings> databaseSettings,
            IConfiguration configuration)
        {
            _databaseSettings = databaseSettings.Value ?? throw new ArgumentNullException(nameof(databaseSettings));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Obtém a connection string baseado nas configurações (LocalDB ou SQL Server).
        /// </summary>
        public string GetConnectionString()
        {
            if (_databaseSettings.UseLocalDb)
            {
                return BuildLocalDbConnectionString();
            }
            else
            {
                return GetProductionConnectionString();
            }
        }

        /// <summary>
        /// Constrói a connection string do LocalDB dinamicamente.
        /// Garante que a pasta de dados existe.
        /// </summary>
        private string BuildLocalDbConnectionString()
        {
            try
            {
                // Garante que a pasta de dados do LocalDB existe
                if (!string.IsNullOrWhiteSpace(_databaseSettings.LocalDbDataPath))
                {
                    var fullPath = Path.GetFullPath(_databaseSettings.LocalDbDataPath);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                        Console.WriteLine($"? Pasta de dados do LocalDB criada: {fullPath}");
                    }
                }

                // Constrói a connection string
                var connectionString = $"Server=(localdb)\\{_databaseSettings.LocalDbInstanceName};" +
                                      $"Database={_databaseSettings.LocalDbInitialCatalog};" +
                                      $"Integrated Security=true;" +
                                      $"Encrypt=false;";

                Console.WriteLine($"? Usando LocalDB: {_databaseSettings.LocalDbInitialCatalog}");
                return connectionString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro ao construir connection string do LocalDB: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtém a connection string de produção do appsettings.
        /// </summary>
        private string GetProductionConnectionString()
        {
            var connectionString = _configuration.GetConnectionString("ProductionConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "? Connection string 'ProductionConnection' não foi encontrada no appsettings.Production.json");
            }

            Console.WriteLine("? Usando conexão de Produção (SQL Server remoto)");
            return connectionString;
        }

        /// <summary>
        /// Validação: Verifica se pode conectar ao banco de dados.
        /// Retorna true se a conexão foi bem-sucedida, false caso contrário.
        /// </summary>
        public bool ValidateConnection()
        {
            try
            {
                var connectionString = GetConnectionString();
                Console.WriteLine($"?? Connection String: {MaskConnectionString(connectionString)}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Erro na validação de conexão: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Mascara a connection string para exibição segura (remove senhas).
        /// </summary>
        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "N/A";

            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"Password=[^;]+",
                "Password=****");
        }

        /// <summary>
        /// Obtém informações sobre o ambiente de banco de dados.
        /// </summary>
        public string GetDatabaseInfo()
        {
            return _databaseSettings.UseLocalDb
                ? $"LocalDB - {_databaseSettings.LocalDbInitialCatalog}"
                : "SQL Server Remoto - BetArbitrageDB_PROD";
        }
    }
}
