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

            GameSessionModel? session = _gameSessionService.GetGameSessionById(request.SessionId);
            if (session == null)
                return Ok();

            var player = session.CurrentGame.PlayersPending?
                .FirstOrDefault(p => p.Id == request.PlayerId);

            if (player == null)
                return Ok();

            await session.LeavePlayerGracefully(player);

            _gameSessionService.CurrentTableUsersChanged?.Invoke(this, session.CurrentGame);

            return Ok();
        }

    }
}
