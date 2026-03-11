using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using BetSniffer.Api.Data;

namespace BetSniffer.Api.Utils;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString)) return;

        bool isLocal = connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase);
        
        if (isLocal)
        {
            await EnsureLocalDbExists(connectionString, logger);
        }

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            logger.LogInformation("Verificando conexão com o banco de dados...");
            await context.Database.OpenConnectionAsync();
            await context.Database.CloseConnectionAsync();
            logger.LogInformation("Conexão com o banco de dados estabelecida com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na conexão com o banco de dados.");
            if (isLocal)
            {
                logger.LogWarning("Tentando restaurar o banco de dados local a partir do backup...");
                await RestoreLocalDb(connectionString, logger);
            }
        }

        await EnsureSchemaCreatedAsync(context, logger);
    }

    private static async Task EnsureSchemaCreatedAsync(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Garantindo integridade do esquema do banco de dados...");
            
            // Script SQL para garantir a tabela IaActivities e colunas extras
            var script = @"
                -- Tabela IaActivities
                IF OBJECT_ID('IaActivities', 'U') IS NULL
                BEGIN
                    CREATE TABLE IaActivities (
                        Id INT PRIMARY KEY IDENTITY(1,1),
                        JobId NVARCHAR(255) NOT NULL,
                        SiteName NVARCHAR(255) NOT NULL,
                        GameUrl NVARCHAR(MAX) NOT NULL,
                        HomeTeam NVARCHAR(255),
                        AwayTeam NVARCHAR(255),
                        GameDate DATETIME2,
                        League NVARCHAR(255),
                        Status NVARCHAR(50) NOT NULL,
                        LastAction NVARCHAR(MAX),
                        ErrorMessage NVARCHAR(MAX),
                        Timestamp DATETIME2 DEFAULT GETDATE()
                    );
                END

                -- Coluna League em GamesInfo
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GamesInfo') AND name = 'League')
                BEGIN
                    ALTER TABLE GamesInfo ADD League NVARCHAR(100);
                END

                -- Coluna GameName em GamesInfo
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('GamesInfo') AND name = 'GameName')
                BEGIN
                    ALTER TABLE GamesInfo ADD GameName NVARCHAR(255);
                END

                -- Coluna TagId em BetInfo
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('BetInfo') AND name = 'TagId')
                BEGIN
                    ALTER TABLE BetInfo ADD TagId INT;
                END

                -- Tabela ArbitrageResults (Caso não tenha sido criada pelo script manual)
                IF OBJECT_ID('ArbitrageResults', 'U') IS NULL
                BEGIN
                    CREATE TABLE ArbitrageResults (
                        Valor_Lucro DECIMAL(18,2),
                        Arbitrage_Lucro_Percent DECIMAL(18,2),
                        TagName_X NVARCHAR(255),
                        OverUnder_X NVARCHAR(50),
                        BetAmount_X DECIMAL(18,2),
                        Multiplier_X DECIMAL(18,2),
                        HomeTeam NVARCHAR(255),
                        SiteName_X NVARCHAR(255),
                        SiteName_Y NVARCHAR(255),
                        AwayTeam NVARCHAR(255),
                        OverUnder_Y NVARCHAR(50),
                        BetAmount_Y DECIMAL(18,2),
                        Multiplier_Y DECIMAL(18,2),
                        TagName_Y NVARCHAR(255),
                        SiteId_X INT,
                        GameDate_X DATETIME,
                        BetId_X INT,
                        BetId_Y INT,
                        SiteId_Y INT,
                        Stake_X DECIMAL(18,2),
                        Stake_Y DECIMAL(18,2),
                        League_X NVARCHAR(255),
                        League_Y NVARCHAR(255),
                        Game_X NVARCHAR(255),
                        Game_Y NVARCHAR(255),
                        Url_X NVARCHAR(255),
                        Url_Y NVARCHAR(255)
                    );
                END
            ";

            await context.Database.ExecuteSqlRawAsync(script);
            logger.LogInformation("Esquema do banco de dados validado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao garantir o esquema do banco de dados.");
        }
    }

    private static async Task EnsureLocalDbExists(string connectionString, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        var checkQuery = $"SELECT database_id FROM sys.databases WHERE name = '{databaseName}'";
        using var command = new SqlCommand(checkQuery, connection);
        var result = await command.ExecuteScalarAsync();

        if (result == null)
        {
            logger.LogWarning("Banco de dados {DbName} não encontrado no LocalDB. Iniciando restore...", databaseName);
            await RestoreLocalDb(connectionString, logger);
        }
    }

    private static async Task RestoreLocalDb(string connectionString, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        // Caminhos baseados na estrutura do projeto
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Tentamos localizar a pasta Scripts/Data subindo do bin
        string projectDir = FindProjectRoot(baseDir);
        string backupFile = Path.Combine(projectDir, "Scripts", "Data", "BetSniffer_10_06_25.bak");
        string mdfPath = Path.Combine(projectDir, "Scripts", "Data", "LocalDB", $"{databaseName}.mdf");
        string ldfPath = Path.Combine(projectDir, "Scripts", "Data", "LocalDB", $"{databaseName}_log.ldf");

        if (!File.Exists(backupFile))
        {
            logger.LogError("Arquivo de backup não encontrado em: {Path}", backupFile);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(mdfPath)!);

        try
        {
            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            logger.LogInformation("Executando RESTORE DATABASE {DbName}...", databaseName);

            // Query de restore ajustando os caminhos dos arquivos físicos
            var restoreQuery = $@"
                RESTORE DATABASE [{databaseName}] 
                FROM DISK = '{backupFile}' 
                WITH MOVE 'BetArbitrageDB' TO '{mdfPath}', 
                     MOVE 'BetArbitrageDB_log' TO '{ldfPath}',
                     REPLACE";

            using var command = new SqlCommand(restoreQuery, connection);
            command.CommandTimeout = 120; // 2 minutos para restore
            await command.ExecuteNonQueryAsync();

            logger.LogInformation("Restore concluído com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro crítico ao restaurar o banco de dados.");
        }
    }

    private static string FindProjectRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BetSniffer.Api.csproj")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? startDir;
    }
}
