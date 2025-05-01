using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BetSniffer.Api.Core.Services
{
    public class DeviceService
    {
        private readonly string _adbPath;

        public DeviceService()
        {
            _adbPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "adb.exe");

            if (!File.Exists(_adbPath))
            {
                throw new FileNotFoundException($"ADB não encontrado no caminho: {_adbPath}");
            }

            StartAdbServer();
        }

        public void CreateAdbForward(int localPort, string remoteAbstract)
        {
            Console.WriteLine($"Criando forward: localhost:{localPort} -> {remoteAbstract}");

            var adbPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "adb.exe");

            var startInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = $"forward tcp:{localPort} {remoteAbstract}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process.WaitForExit();
        }

        private void StartAdbServer()
        {
            Console.WriteLine("Iniciando o servidor ADB...");

            ExecuteAdbCommand("start-server");

            Task.Delay(2000).Wait(); // Aguarda o servidor iniciar
        }

        public void ConnectToDevice()
        {
            Console.WriteLine("Verificando dispositivos conectados...");

            string output = ExecuteAdbCommand("devices");

            if (string.IsNullOrWhiteSpace(output) || !output.Contains("\tdevice"))
            {
                throw new Exception("Nenhum dispositivo ADB encontrado.");
            }

            Console.WriteLine("Dispositivo ADB conectado e pronto.");
        }

        public void Tap(int x, int y)
        {
            Console.WriteLine($"Tocando na posição: ({x},{y})");
            ExecuteShellCommand($"input tap {x} {y}");
        }

        public void InputText(string text)
        {
            Console.WriteLine($"Digitando texto de forma segura: {text}");

            // Corrige apenas espaços e caracteres que realmente quebram no ADB
            text = text.Replace(" ", "%s");
            text = text.Replace("&", "\\&");
            text = text.Replace("|", "\\|");
            text = text.Replace("<", "\\<");
            text = text.Replace(">", "\\>");
            text = text.Replace("\"", "\\\"");
            text = text.Replace("'", "\\'");
            text = text.Replace("(", "\\(");
            text = text.Replace(")", "\\)");
            text = text.Replace(";", "\\;");
            text = text.Replace("*", "\\*");
            text = text.Replace("?", "\\?");

            const int maxChunkSize = 70; // Limite seguro para cada envio

            for (int i = 0; i < text.Length; i += maxChunkSize)
            {
                var chunk = text.Substring(i, Math.Min(maxChunkSize, text.Length - i));
                ExecuteShellCommand($"input text \"{chunk}\"");
                Task.Delay(100).Wait(); // Pequeno delay para digitação natural
            }
        }

        public void OpenApp(string appPackageActivity)
        {
            Console.WriteLine($"Abrindo aplicativo: {appPackageActivity}...");
            ExecuteAdbShellCommand($"am start -n {appPackageActivity}");
        }

        public int DetectDevToolsPort()
        {
            Console.WriteLine("Detectando porta do DevTools via adb logcat...");

            var adbPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "adb.exe");

            var processInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = "logcat -d",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n');

            foreach (var line in lines)
            {
                if (line.Contains("DevTools listening on ws://"))
                {
                    var match = Regex.Match(line, @"ws://127.0.0.1:(\d+)/devtools/browser");
                    if (match.Success)
                    {
                        var portString = match.Groups[1].Value;
                        if (int.TryParse(portString, out int port))
                        {
                            Console.WriteLine($"Detectado DevTools na porta: {port}");
                            return port;
                        }
                    }
                }
            }

            throw new Exception("Não foi possível detectar a porta do DevTools.");
        }

        public void ExecuteShellCommand(string shellCommand)
        {
            Console.WriteLine($"Executando comando Shell: {shellCommand}...");
            ExecuteAdbShellCommand(shellCommand);
        }

        public void KillAdb()
        {
            Console.WriteLine("Finalizando o servidor ADB...");
            ExecuteAdbCommand("kill-server");
        }

        private string ExecuteAdbCommand(string arguments)
        {
            return RunProcess(_adbPath, arguments);
        }

        private string ExecuteAdbShellCommand(string shellCommand)
        {
            return RunProcess(_adbPath, $"shell {shellCommand}");
        }

        public string DumpScreen()
        {
            Console.WriteLine("Capturando XML da tela...");

            var tempPath = "/sdcard/dump.xml";

            // Faz o dump da tela
            ExecuteShellCommand($"uiautomator dump {tempPath}");

            // Copia o arquivo para o PC
            var adbPullCommand = $"pull {tempPath} {Path.Combine(AppContext.BaseDirectory, "Drivers", "dump.xml")}";
            RunProcess(_adbPath, adbPullCommand);

            var dumpPath = Path.Combine(AppContext.BaseDirectory, "Drivers", "dump.xml");

            if (!File.Exists(dumpPath))
            {
                throw new Exception("Arquivo de dump não encontrado após captura!");
            }

            return File.ReadAllText(dumpPath);
        }

        private string RunProcess(string fileName, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine($"[ADB ERRO]: {error}");
            }

            return output.Trim();
        }
    }
}
