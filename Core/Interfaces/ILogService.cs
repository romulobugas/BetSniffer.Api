using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace BetSniffer.Api.Core.Interfaces
{
    public interface ILogService
    {
        void Log(string message);
        void LogError(string errorMessage, Exception ex);
    }

    public class LogService : ILogService
    {
        private readonly string _logDirectory;
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        private static readonly ThreadLocal<string> ThreadId = new ThreadLocal<string>(() => Guid.NewGuid().ToString());

        public LogService(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            // Obtém o diretório de logs do appsettings.json com fallback seguro
            _logDirectory = configuration["LogSettings:Directory"] ??
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            try
            {
                Directory.CreateDirectory(_logDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Falha ao garantir diretório de logs '{_logDirectory}': {ex.Message}");
                _logDirectory = Path.Combine(Path.GetTempPath(), "BetSnifferLogs");
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public void Log(string message)
        {
            WriteToFile("log", message);
        }

        public void LogError(string errorMessage, Exception ex)
        {
            var fullMessage = $"{errorMessage}\nException: {ex.Message}\nStackTrace: {ex.StackTrace}";
            WriteToFile("error", fullMessage);
        }

        private void WriteToFile(string logType, string message)
        {
            try
            {
                Semaphore.Wait(); // Garante que apenas uma thread por vez escreve no arquivo

                // Inclui o ID da thread no nome do arquivo
                var logFileName = $"{logType}_{ThreadId.Value}_{DateTime.Now:yyyy-MM-dd}.log";
                var logFilePath = Path.Combine(_logDirectory, logFileName);

                using (var writer = new StreamWriter(logFilePath, append: true))
                {
                    writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][Thread-{Thread.CurrentThread.ManagedThreadId}] {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Falha ao escrever no log: {ex.Message}");
            }
            finally
            {
                Semaphore.Release(); // Libera o recurso para outra thread
            }
        }
    }
}
