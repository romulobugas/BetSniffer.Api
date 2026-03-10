using BetSniffer.Ia.Models;

namespace BetSniffer.Ia.Services;

public interface IVisionAgentClient
{
    Task<VisualAgentResponse> DecideNextActionAsync(VisualAgentRequest request, CancellationToken cancellationToken);
}
