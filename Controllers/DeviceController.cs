using BetSniffer.Api.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DeviceController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        [HttpPost("connect")]
        public IActionResult Connect()
        {
            try
            {
                _deviceService.ConnectToDevice();
                return Ok("Dispositivo conectado com sucesso.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao conectar ao dispositivo: {ex.Message}");
            }
        }

        [HttpPost("openApp")]
        public IActionResult OpenApp(string appPackageName)
        {
            try
            {
                _deviceService.OpenApp(appPackageName);
                return Ok("Aplicativo aberto com sucesso.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao abrir o aplicativo: {ex.Message}");
            }
        }

        [HttpPost("executeCommand")]
        public IActionResult ExecuteCommand(string command)
        {
            try
            {
                var result = _deviceService.ExecuteShellCommand(command);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao executar comando: {ex.Message}");
            }
        }

        [HttpPost("disconnect")]
        public IActionResult Disconnect()
        {
            try
            {
                _deviceService.Disconnect();
                return Ok("Dispositivo desconectado com sucesso.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Erro ao desconectar do dispositivo: {ex.Message}");
            }
        }
    }
}
