using Pokerface.Components.Pages;
using Pokerface.Models;
using Pokerface.Services.DB;

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
            await session.AddPlayer(new PlayerModel { Name = playerName });

            return session;
        }

    }
}
