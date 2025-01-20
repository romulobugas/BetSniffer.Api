using BetSniffer.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace BetSniffer.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GamesUpdateController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly BatchScrapingController _batchScrapingController;

        public GamesUpdateController(
            ApplicationDbContext dbContext,
            BatchScrapingController batchScrapingController)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _batchScrapingController = batchScrapingController ?? throw new ArgumentNullException(nameof(batchScrapingController));
        }
    }
}
