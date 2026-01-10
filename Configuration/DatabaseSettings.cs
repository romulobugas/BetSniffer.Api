namespace BetSniffer.Api.Configuration
{
    /// <summary>
    /// Configurações de banco de dados parametrizáveis.
    /// Permite alternar entre LocalDB (desenvolvimento) e SQL Server (produção).
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Ambiente da aplicação: "Development", "Staging", "Production"
        /// </summary>
        public string Environment { get; set; } = "Development";

        /// <summary>
        /// Se true, usa LocalDB; se false, usa SQL Server remoto
        /// </summary>
        public bool UseLocalDb { get; set; } = true;

        /// <summary>
        /// Caminho local onde o LocalDB armazenará os arquivos de banco de dados
        /// Exemplo: "./Data/LocalDB"
        /// </summary>
        public string LocalDbDataPath { get; set; } = "./Data/LocalDB";

        /// <summary>
        /// Nome da instância LocalDB a usar
        /// Padrão: "mssqllocaldb"
        /// </summary>
        public string LocalDbInstanceName { get; set; } = "mssqllocaldb";

        /// <summary>
        /// Nome do banco de dados inicial para LocalDB
        /// Exemplo: "BetArbitrageDB_DEV"
        /// </summary>
        public string LocalDbInitialCatalog { get; set; } = "BetArbitrageDB_DEV";

        public override string ToString()
        {
            return $"Environment: {Environment}, UseLocalDb: {UseLocalDb}, LocalDbPath: {LocalDbDataPath}";
        }
    }
}
