using Pokerface.Components.Card;
using Pokerface.Enums;
using Pokerface.Services.DB;

namespace Pokerface.Models
{
    public class GameSessionModel
    {
        private readonly DbTableService? _dbTableService;

        public EventHandler? OnPlayerJoined;
        public EventHandler? OnGameChanged;
        public EventHandler? OnRoundFinished;

        public int Id { get; set; }

        public TableModel GameTable { get; set; } = new TableModel();

        public List<PlayerModel> Players { get; set; } = new List<PlayerModel>();

        public List<Card>? CardSet { get; set; }
        public List<Card>? CommunityCards { get; set; }

        public GameContext CurrentGame { get; set; } = new GameContext();

        private int CurrentPlayer;

        public List<ActionOption> AvailableActions { get; private set; } = new();

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
            if (GameTable.CurrentUsers == 0)
            {
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = false;
            }
        }


        public void StartGame()
        {

            //Reset the game
            CardSet = CardDeck.GenerateShuffledDeck();
            CommunityCards = new List<Card>();
            CurrentGame = new();
            AvailableActions = new();
            CurrentGame.TheWinners = new();
            CurrentPlayer = 0;

            CurrentGame.RoundLocked = true;
            CurrentGame.RoundFinished = false;
            CurrentGame.DealerIndex = (CurrentGame.DealerIndex + 1) % Players.Count;
            CurrentGame.SmallBlindIndex = (CurrentGame.DealerIndex + 1) % Players.Count;
            CurrentGame.BigBlindIndex = (CurrentGame.DealerIndex + 2) % Players.Count;

            //start with the very first player
            if (Players.Count < 2)
                return;

            //subscribe to all player actions
            foreach (var player in Players)
            {
                player.ResetRoundSettings();
                player.PlayerInput -= OnPlayerActionComitted;
                player.PlayerInput += OnPlayerActionComitted;

                player.Card1 = CardSet[0];
                CardSet.RemoveAt(0);

                player.Card2 = CardSet[0];
                CardSet.RemoveAt(0);
            }

            CurrentPlayer = (CurrentGame.BigBlindIndex + 1) % Players.Count;
            Players[CurrentPlayer].IsNext = true;
            UpdateAvailableActions(Players[CurrentPlayer]);
  
            OnGameChanged?.Invoke(this, EventArgs.Empty);

            //waiting for player input by event callback
        }


        private void OnPlayerActionComitted(PlayerModel player, PlayerAction action)
        {
            if (!player.IsNext)
                return;

            // Apply action to the game state
            switch (action.ActionType)
            {
                case EnumPlayerAction.None:
                    break;

                case EnumPlayerAction.Fold:
                    player.HasFolded = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Check:
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Call:
                    int callAmount = CurrentGame.CurrentBet - player.CurrentBet;
                    player.CurrentBet += callAmount;
                    player.RemainingStack -= callAmount;
                    CurrentGame.Pot += callAmount;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.Bet:
                case EnumPlayerAction.Raise:
                case EnumPlayerAction.ReRaise:
                    player.CurrentBet += action.CurrentBet;
                    player.RemainingStack -= action.CurrentBet;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                    CurrentGame.Pot += action.CurrentBet;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.AllIn:
                    player.CurrentBet += player.RemainingStack;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, player.CurrentBet);
                    CurrentGame.Pot += player.RemainingStack;
                    player.RemainingStack = 0;
                    player.AllIn = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.SmallBlind:
                    player.CurrentBet += CurrentGame.SmallBlind;
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    player.HasPostedSmallBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.SmallBlind);
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.BigBlind:
                    player.CurrentBet += CurrentGame.BigBlind;
                    player.RemainingStack -= CurrentGame.BigBlind;
                    CurrentGame.Pot += CurrentGame.BigBlind;
                    player.HasPostedBigBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.BigBlind);
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += CurrentGame.SmallBlind; // or ante amount
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    // DO NOT mark HasActedThisRound yet
                    break;

                case EnumPlayerAction.SitOut:
                    player.IsSittingOut = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.SitIn:
                    player.IsSittingOut = false;
                    break;

                case EnumPlayerAction.Timeout:
                    // optional: handle timeout
                    player.HasActedThisRound = true;
                    break;
            }

            // Mark current player as done
            player.IsNext = false;

            // Move to next active player
            do
            {
                CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            } while (Players[CurrentPlayer].HasFolded || Players[CurrentPlayer].IsSittingOut);

            var nextPlayer = Players[CurrentPlayer];
            nextPlayer.IsNext = true;

            // Compute available actions for the next player
            UpdateAvailableActions(nextPlayer);

            // Check if betting round is complete
            if (IsBettingRoundComplete())
            {
                AdvanceRound();
            }

            // Refresh UI
            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            AvailableActions.Clear();

            // HARD GUARD
            if (!player.IsNext)
                return;

            if (CurrentGame.CurrentRound == BettingRound.Showdown)
                return;

            var actions = new List<ActionOption>();
            int playerIndex = Players.IndexOf(player);

            actions.Add(new ActionOption(EnumPlayerAction.Fold));

            // --- PRE-FLOP BLINDS ---
            if (CurrentGame.CurrentRound == BettingRound.PreFlop)
            {
                if (playerIndex == CurrentGame.SmallBlindIndex && !player.HasPostedSmallBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.SmallBlind));
                    AvailableActions = actions;
                    return;
                }

                if (playerIndex == CurrentGame.BigBlindIndex && !player.HasPostedBigBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.BigBlind));
                    AvailableActions = actions;
                    return;
                }
            }

            if (player.CurrentBet < CurrentGame.CurrentBet)
                actions.Add(new ActionOption(EnumPlayerAction.Call));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Check));

            if (CurrentGame.CurrentBet == 0)
                actions.Add(new ActionOption(EnumPlayerAction.Bet, true));
            else
                actions.Add(new ActionOption(EnumPlayerAction.Raise, true));

            if (player.RemainingStack > 0)
                actions.Add(new ActionOption(EnumPlayerAction.AllIn));

            AvailableActions = actions;
        }

        private void AdvanceRound()
        {
            // Clear per-round flags
            foreach (var p in Players)
            {
                p.HasActedThisRound = false;
                p.CurrentBet = 0;
                p.IsNext = false;
            }

            CurrentGame.CurrentBet = 0;
            AvailableActions.Clear();

            switch (CurrentGame.CurrentRound)
            {
                case BettingRound.PreFlop:
                    DealFlop();
                    CurrentGame.CurrentRound = BettingRound.Flop;
                    break;

                case BettingRound.Flop:
                    DealTurn();
                    CurrentGame.CurrentRound = BettingRound.Turn;
                    break;

                case BettingRound.Turn:
                    DealRiver();
                    CurrentGame.CurrentRound = BettingRound.River;
                    break;

                case BettingRound.River:
                    CurrentGame.CurrentRound = BettingRound.Showdown;
                    CalculateWinner();
                    return;
            }

            // Assign NEXT SINGLE player
            CurrentPlayer = GetFirstActivePlayerAfterDealer();
            Players[CurrentPlayer].IsNext = true;
            UpdateAvailableActions(Players[CurrentPlayer]);
        }

        private int GetFirstActivePlayerAfterDealer()
        {
            if (Players.Count < 2)
                return 0;

            int index = (CurrentGame.DealerIndex + 1) % Players.Count;

            // Find first active player (not folded, not sitting out)
            for (int i = 0; i < Players.Count; i++)
            {
                var player = Players[index];
                if (!player.HasFolded && !player.IsSittingOut)
                    return index;

                index = (index + 1) % Players.Count;
            }

            // fallback: return dealer if everyone else folded/sitting out
            return CurrentGame.DealerIndex;
        }


        private bool IsBettingRoundComplete()
        {
            var activePlayers = Players
                .Where(p => !p.HasFolded && !p.IsSittingOut)
                .ToList();

            // Everyone has acted or is all-in
            if (!activePlayers.All(p => p.HasActedThisRound || p.AllIn))
                return false;

            // Everyone has matched the bet or is all-in
            if (!activePlayers.All(p => p.CurrentBet == CurrentGame.CurrentBet || p.AllIn))
                return false;

            return true;
        }


        



        private void CalculateWinner()
        {
            if (CommunityCards == null || CommunityCards.Count < 5)
                return;

            var activePlayers = Players
                .Where(p => !p.HasFolded && p.Card1 != null && p.Card2 != null)
                .ToList();

            if (!activePlayers.Any())
            {
                foreach (var p in Players)
                    p.Result = "Alle haben gefoldet.\nUnentschieden.";
                return;
            }

            // Bestes Hand-Ranking für jeden Spieler
            var bestHands = new Dictionary<PlayerModel, (int Rank, List<int> Tie, string HandName)>();
            foreach (var p in activePlayers)
            {
                var fullHand = new List<Card> { p.Card1!, p.Card2! };
                fullHand.AddRange(CommunityCards);
                bestHands[p] = EvaluateBestHand(fullHand);
            }

            int maxRank = bestHands.Values.Max(v => v.Rank);
            var topPlayers = bestHands.Where(kv => kv.Value.Rank == maxRank)
                                      .Select(kv => kv.Key)
                                      .ToList();

            int potShare = CurrentGame.Pot / topPlayers.Count;

            CurrentGame.TheWinners = new();

            foreach (var p in Players)
            {
                var handName = bestHands.ContainsKey(p) ? bestHands[p].HandName : "Keine Hand";

                if (topPlayers.Contains(p))
                {
                    p.RemainingStack += potShare; // Gewinn auszahlen

                    p.Result = topPlayers.Count > 1
                        ? $"Unentschieden!\nDeine beste Hand: {handName}\nGewonnener Pot: {potShare}"
                        : $"Du hast diese Runde gewonnen!\nDeine beste Hand: {handName}\nGewinnener Pot: {potShare}";

                    CurrentGame.TheWinners.Add(new PlayerModel(p, handName));
                }
                else
                {
                    p.Result = $"Du hast diese Runde verloren.\nDeine beste Hand: {handName}\nGewonnener Pot: 0";
                }

                p.IsNext = false;
            }

            CurrentGame.RoundLocked = false;
            CurrentGame.RoundFinished = true;
            AvailableActions.Clear();

            OnRoundFinished?.Invoke(this, EventArgs.Empty);
        }



        private static (int Rank, List<int> Tie, string HandName) EvaluateBestHand(List<Card> sevenCards)
        {
            var cardValues = sevenCards.Select(c => (int)c.Rank).OrderByDescending(v => v).ToList();
            var groups = cardValues.GroupBy(v => v)
                                   .OrderByDescending(g => g.Count())
                                   .ThenByDescending(g => g.Key)
                                   .ToList();

            var suitGroups = sevenCards.GroupBy(c => c.Suit)
                                       .Where(g => g.Count() >= 5)
                                       .ToList();
            bool isFlush = suitGroups.Any();

            var distinctValues = cardValues.Distinct().ToList();
            int straightHigh = -1;
            for (int i = 0; i <= distinctValues.Count - 5; i++)
            {
                if (distinctValues[i] - distinctValues[i + 4] == 4)
                {
                    straightHigh = distinctValues[i];
                    break;
                }
            }
            // Low-Ace straight (A-2-3-4-5)
            if (distinctValues.Contains((int)EnumCardRank.Ace) &&
                distinctValues.Contains((int)EnumCardRank.Five) &&
                distinctValues.Contains((int)EnumCardRank.Four) &&
                distinctValues.Contains((int)EnumCardRank.Three) &&
                distinctValues.Contains((int)EnumCardRank.Two))
            {
                straightHigh = 5;
            }
            bool isStraight = straightHigh != -1;

            string RankToName(int rank) => Enum.GetName(typeof(EnumCardRank), rank) ?? rank.ToString();

            // Straight flush / Royal flush
            if (isFlush && isStraight)
                return (900 + straightHigh, new List<int> { straightHigh },
                    straightHigh == (int)EnumCardRank.Ace ? "Royal Flush" : $"Straight Flush: {RankToName(straightHigh)} hoch");

            if (groups[0].Count() == 4)
                return (800 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"Vierling: {RankToName(groups[0].Key)}");

            if (groups[0].Count() == 3 && groups.Count > 1 && groups[1].Count() >= 2)
                return (700 + groups[0].Key, new List<int> { groups[1].Key, groups[0].Key },
                    $"Full House: {RankToName(groups[0].Key)} über {RankToName(groups[1].Key)}");

            if (isFlush)
            {
                var flushCards = suitGroups.First().Select(c => (int)c.Rank).OrderByDescending(v => v).Take(5).ToList();
                return (600 + flushCards[0], flushCards, $"Flush: {RankToName(flushCards[0])} hoch");
            }

            if (isStraight)
                return (500 + straightHigh, new List<int> { straightHigh }, $"Straight: {RankToName(straightHigh)} hoch");

            if (groups[0].Count() == 3)
                return (400 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"Drilling: {RankToName(groups[0].Key)}");

            if (groups[0].Count() == 2 && groups.Count > 1 && groups[1].Count() == 2)
                return (300 + groups[0].Key, groups.Select(g => g.Key).ToList(),
                    $"Zwei Paare: {RankToName(groups[0].Key)} & {RankToName(groups[1].Key)}");

            if (groups[0].Count() == 2)
                return (200 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"Ein Paar: {RankToName(groups[0].Key)}");

            return (100 + groups[0].Key, groups.Select(g => g.Key).ToList(), $"High Card: {RankToName(groups[0].Key)}");
        }

        private void DealFlop()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards ??= new List<Card>();

            CommunityCards.Add(CardSet[0]);
            CommunityCards.Add(CardSet[1]);
            CommunityCards.Add(CardSet[2]);

            CardSet.RemoveRange(0, 3);
        }

        private void DealTurn()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards!.Add(CardSet[0]);
            CardSet.RemoveAt(0);
        }

        private void DealRiver()
        {
            if (CardSet == null)
                return;

            BurnCard();
            CommunityCards!.Add(CardSet[0]);
            CardSet.RemoveAt(0);
        }

        private void BurnCard()
        {
            if (CardSet == null)
                return;

            CardSet.RemoveAt(0);
        }


    }
}
