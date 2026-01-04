using Pokerface.Models;
using Pokerface.Services.DB;
using System.Xml.Linq;

namespace Pokerface.Services
{
    public class GameSessionService
    {
        private readonly DbTableService _tableService;

        public List<GameSessionModel> GameSessions { get; set; } = new List<GameSessionModel>();

        public GameSessionService(DbTableService tableService)
        {
            _tableService = tableService;
        }

        public GameSessionModel? GetGameSessionById(int sessionId)
        {
            return GameSessions.FirstOrDefault(s => s.Id == sessionId);
        }
 

        public async Task<GameSessionModel?> JoinGameSessionAsync(TableModel table, string playerName)
        {
            // Try to find an existing session for the table
            GameSessionModel? session = GameSessions
                .FirstOrDefault(s => s.GameTable.Id == table.Id);

            if (session == null)
            {
                // No session exists --> create a new one
                session = new GameSessionModel(_tableService)
                {
                    Id = table.Id,
                    GameTable = table
                };
                GameSessions.Add(session);
            }
            else
            {
                // Session exists --> check if table is full
                if (session.GameTable.CurrentUsers >= session.GameTable.MaxUsers)
                    return null; // table full


                // Check if player already exists in the session
                if (session.Players.Any(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
                    return null; // player already joined

            }

            // Add the player
            await session.AddPlayer(new PlayerModel(session.GameTable.CurrentUsers + 1, playerName));

            return session;
        }

    }
}
