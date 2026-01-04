
using Pokerface.Services.DB;

namespace Pokerface.Models
{
    public class GameSessionModel
    {
        public EventHandler? OnPlayerJoined;

        public EventHandler? OnGameChanged;

        private readonly DbTableService? _dbTableService;
        public int Id { get; set; }

        public TableModel GameTable { get; set; } = new TableModel();

        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();

        public List<Card> CardSet { get; set; } = CardDeck.MixWholeRandomCards();


        public GameSessionModel(DbTableService dbTableService)
        {
            _dbTableService = dbTableService;
        }

        public PlayerModel? GetPlayerById(int player)
        {
            return Players.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            if (Players.Count > GameTable.MaxUsers)
                throw new InvalidOperationException("Cannot add more players than the maximum allowed.");

            Players.Add(player);

            GameTable.CurrentUsers = Players.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

        }

        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            Players.Remove(player);

            GameTable.CurrentUsers = Players.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

            //if players is zero, close the game session ???

        }

        public void StartGame()
        {
            //give every player two cards
            foreach (var player in Players)
            {
                player.Card1.SetCard(CardSet[0], true);
                CardSet.RemoveAt(0);
                player.Card2.SetCard(CardSet[0], true);
                CardSet.RemoveAt(0);   
            }

            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ExitGame()
        {
            //Todo... handle what should happen ...
            ////if players is zero, close the game session ???
        }

    }
}
