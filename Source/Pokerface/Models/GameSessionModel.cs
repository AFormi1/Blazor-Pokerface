
using Pokerface.Services.DB;

namespace Pokerface.Models
{
    public class GameSessionModel
    {
        private readonly DbTableService? _dbTableService;
        public int Id { get; set; }

        public TableModel GameTable { get; set; } = new TableModel();

        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();

        public List<Card> CardSet { get; set; } = CardDeck.MixWholeRandomCards();


        public GameSessionModel(DbTableService dbTableService)
        {
            _dbTableService = dbTableService;
        }

        public PlayerModel? GetPlayerByName(string player)
        {
            return Players.Where(p => p.Name == player).FirstOrDefault();
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

        }

        public void StartGame()
        {
            //give every player two cards
            foreach (var player in Players)
            {
                player.Card1 = new(CardSet[0]);
                CardSet.RemoveAt(0);
                player.Card2 = new(CardSet[0]);
                CardSet.RemoveAt(0);   
            }
        }

    }
}
