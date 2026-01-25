using Microsoft.AspNetCore.Mvc;
using Pokerface.Services;
using Pokerface.Models;

namespace Pokerface.Controller
{
    [ApiController]
    [Route("api/session")]
    public class SessionController : ControllerBase
    {
        private readonly GameSessionService _gameSessionService;

        public SessionController(GameSessionService gameSessionService)
        {
            _gameSessionService = gameSessionService;
        }

        [HttpPost("exit")]
        [Consumes("application/json", "text/plain")]
        public async Task<IActionResult> Exit([FromBody] ExitSessionRequest request)
        {
            if (request == null)
                return BadRequest("Request body missing");

            var session = _gameSessionService.GetGameSessionById(request.SessionId);
            if (session == null)
                return Ok();

            var player = session.PlayersPending?
                .FirstOrDefault(p => p.Id == request.PlayerId);

            if (player == null)
                return Ok();

            await _gameSessionService.RemovePlayerFromSessionAsync(session, player);

            return Ok();
        }
    }
}
