using Pokerface.Models;
using Pokerface.Services.DB;

namespace Pokerface.Services
{
    public class GameSessionService
    {
        private readonly DbTableService _tableService;

        public EventHandler<TableModel>? CurrentTableUsersChanged;

        public List<GameSessionModel> GameSessions { get; set; } = new List<GameSessionModel>();

        public GameSessionService(DbTableService tableService)
        {
            _tableService = tableService;
        }

        public GameSessionModel? GetGameSessionById(int sessionId)
        {
            return GameSessions.FirstOrDefault(s => s.Id == sessionId);
        }


        public async Task<int?> JoinGameSessionAsync(TableModel table, string playerName)
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
            PlayerModel player = new PlayerModel(session.GameTable.CurrentUsers + 1, playerName);
            await session.AddPlayer(player);

            CurrentTableUsersChanged?.Invoke(this, session.GameTable);

            return player.Id;
        }


        public async Task RemovePlayerFromSessionAsync(GameSessionModel session, PlayerModel player)
        {
            await session.RemovePlayer(player);

            CurrentTableUsersChanged?.Invoke(this, session.GameTable);

            // If no players left, remove the session
            if (session.Players.Count == 0)
                RemoveSession(session);
        }

        public void RemoveSession(GameSessionModel session)
        {
            if (session == null)
                return;

            GameSessions.Remove(session);

            // optionally: clear its lists to free memory faster
            session.Players.Clear();
            session.CardSet?.Clear();
        }

    }
}
