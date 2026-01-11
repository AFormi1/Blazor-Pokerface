using Pokerface.Components.Pages;
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
            return GameSessions.FirstOrDefault(s => s.CurrentGame.Id == sessionId);
        }


        public async Task<int?> JoinGameSessionAsync(TableModel table, string playerName)
        {
            // Try to find an existing session for the table
            GameSessionModel? session = GameSessions
                .FirstOrDefault(s => s.CurrentGame?.Id == table.Id);

            if (session == null)
            {
                // No session exists --> create a new one
                session = new GameSessionModel(_tableService, table);
    
                GameSessions.Add(session);
            }
            else
            {
                // Session exists --> check if table is full
                if (session.CurrentGame?.CurrentPlayers >= TableModel.MaxPlayers)
                    return null; // table full


                // Check if player already exists in the session
                if (session.PlayersPending != null)
                {
                    if (session.PlayersPending.Any(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)))
                        return null; // player already joined
                }
            }

            // Add the player
            if (session.CurrentGame == null || session.PlayersPending == null)
                throw new ArgumentNullException("objects are null");

            int freeChair = Enumerable
                .Range(1, TableModel.MaxPlayers)
                .Except(session.PlayersPending.Select(p => p.Chair))
                .FirstOrDefault(-1);

            PlayerModel player = new PlayerModel(freeChair, playerName);
            await session.AddPlayer(player);

            CurrentTableUsersChanged?.Invoke(this, session.CurrentGame);

            return player.Id;
        }


        public async Task RemovePlayerFromSessionAsync(GameSessionModel session, PlayerModel player)
        {
            await session.RemovePlayer(player);

            CurrentTableUsersChanged?.Invoke(this, session.CurrentGame);

            // If no players left, remove the session
            if (session.PlayersPending?.Count == 0)
                await RemoveSession(session);
        }

        public async Task RemoveSession(GameSessionModel? session)
        {
            if (session == null || session.CurrentGame == null)
                return;

            session.CurrentGame.CurrentPlayers = 0;
            CurrentTableUsersChanged?.Invoke(this, session.CurrentGame);

            GameSessions.Remove(session);

            await session.DisposeAsync();

            session = null;
        }
    }
}
