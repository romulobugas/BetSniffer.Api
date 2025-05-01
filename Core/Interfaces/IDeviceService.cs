namespace BetSniffer.Api.Core.Interfaces
{
    public interface IDeviceService
    {
        void ConnectToDevice();
        string ExecuteShellCommand(string command);
        void OpenApp(string appPackageName);
        void Disconnect();
    }
}
