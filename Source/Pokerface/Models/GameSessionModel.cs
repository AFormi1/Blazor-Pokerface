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

        public List<PlayerModel> PlayersPending { get; set; } = new List<PlayerModel>();

        public List<Card>? CardSet { get; set; }
        public List<Card>? CommunityCards { get; set; }

        public GameContext CurrentGame { get; set; } = new GameContext();

        public List<ActionOption> AvailableActions { get; private set; } = new();

        public GameSessionModel(DbTableService dbTableService)
        {
            _dbTableService = dbTableService;
        }

        public PlayerModel? GetPlayerById(int player)
        {
            return PlayersPending.Where(p => p.Id == player).FirstOrDefault();
        }

        public async Task AddPlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            if (PlayersPending.Count > GameTable.MaxUsers)
                throw new InvalidOperationException("Cannot add more players than the maximum allowed.");

            PlayersPending.Add(player);

            GameTable.CurrentUsers = PlayersPending.Count;

            //Update the TableModel in DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

        }

        public async Task RemovePlayer(PlayerModel player)
        {
            if (_dbTableService == null)
                throw new ArgumentNullException("_dbTableService is null");

            // Mark as folded / sitting out
            player.HasFolded = true;
            player.IsSittingOut = true;

            // If the next player is sitting out or left, auto-fold
            if (player.IsNext)
            {
                OnPlayerActionComitted(player, new PlayerAction { ActionType = EnumPlayerAction.Fold });
                return;
            }

            PlayersPending.Remove(player);
            GameTable.CurrentUsers = PlayersPending.Count(p => !p.IsSittingOut);

            // Update DB
            await _dbTableService.SaveItemAsync(GameTable);

            OnPlayerJoined?.Invoke(this, EventArgs.Empty);

            // If everyone left, close the session
            if (PlayersPending.All(p => p.IsSittingOut))
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
            //Add all players from the list to the current game
            CurrentGame = new(PlayersPending);
            AvailableActions = new();

            //start with the very first player
            if (CurrentGame.Players.Count < 2)
                return;

            //subscribe to all player actions
            foreach (var player in CurrentGame.Players)
            {
                player.ResetRoundSettings();
                player.PlayerInput -= OnPlayerActionComitted;
                player.PlayerInput += OnPlayerActionComitted;

                player.Card1 = CardSet[0];
                CardSet.RemoveAt(0);

                player.Card2 = CardSet[0];
                CardSet.RemoveAt(0);
            }

            CurrentGame.CurrentPlayer = (CurrentGame.BigBlindIndex + 1) % CurrentGame.Players.Count;
            CurrentGame.Players[CurrentGame.CurrentPlayer].IsNext = true;
            UpdateAvailableActions(CurrentGame.Players[CurrentGame.CurrentPlayer]);

            OnGameChanged?.Invoke(this, EventArgs.Empty);

            //waiting for player input by event callback
        }


        private void OnPlayerActionComitted(PlayerModel player, PlayerAction action)
        {
            if (!player.IsNext)
                return;

            // --- Apply player's action ---
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
                    break;

                case EnumPlayerAction.BigBlind:
                    player.CurrentBet += CurrentGame.BigBlind;
                    player.RemainingStack -= CurrentGame.BigBlind;
                    CurrentGame.Pot += CurrentGame.BigBlind;
                    player.HasPostedBigBlind = true;
                    CurrentGame.CurrentBet = Math.Max(CurrentGame.CurrentBet, CurrentGame.BigBlind);
                    break;

                case EnumPlayerAction.PostAnte:
                    player.CurrentBet += CurrentGame.SmallBlind; // or ante amount
                    player.RemainingStack -= CurrentGame.SmallBlind;
                    CurrentGame.Pot += CurrentGame.SmallBlind;
                    break;

                case EnumPlayerAction.SitOut:
                    player.IsSittingOut = true;
                    player.HasActedThisRound = true;
                    break;

                case EnumPlayerAction.SitIn:
                    player.IsSittingOut = false;
                    break;

                case EnumPlayerAction.Timeout:
                    player.HasActedThisRound = true;
                    break;
            }

            // Mark current player as done
            player.IsNext = false;

            // --- Check if only one active player remains ---
            var activePlayers = CurrentGame.Players.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();
            if (activePlayers.Count == 1)
            {
                // Only one player left: assign full pot and finish the round
                CalculateWinner();
                OnGameChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // --- Check if all remaining active players are all-in ---
            if (activePlayers.All(p => p.AllIn))
            {
                // Deal all remaining community cards immediately
                switch (CurrentGame.CurrentRound)
                {
                    case BettingRound.PreFlop:
                        DealFlop();
                        CurrentGame.CurrentRound = BettingRound.Flop;
                        goto case BettingRound.Flop;

                    case BettingRound.Flop:
                        DealTurn();
                        CurrentGame.CurrentRound = BettingRound.Turn;
                        goto case BettingRound.Turn;

                    case BettingRound.Turn:
                        DealRiver();
                        CurrentGame.CurrentRound = BettingRound.River;
                        goto case BettingRound.River;

                    case BettingRound.River:
                        CurrentGame.CurrentRound = BettingRound.Showdown;
                        CalculateWinner();
                        OnGameChanged?.Invoke(this, EventArgs.Empty);
                        return;
                }
            }

            // --- Normal turn rotation ---
            do
            {
                CurrentGame.CurrentPlayer = (CurrentGame.CurrentPlayer + 1) % CurrentGame.Players.Count;
            } while (CurrentGame.Players[CurrentGame.CurrentPlayer].HasFolded || CurrentGame.Players[CurrentGame.CurrentPlayer].IsSittingOut);

            var nextPlayer = CurrentGame.Players[CurrentGame.CurrentPlayer];
            nextPlayer.IsNext = true;

            // If the next player is sitting out or left, auto-fold
            if (nextPlayer.IsSittingOut)
            {
                OnPlayerActionComitted(nextPlayer, new PlayerAction { ActionType = EnumPlayerAction.Fold });
                return;
            }

            UpdateAvailableActions(nextPlayer);

            // Check if betting round is complete
            if (IsBettingRoundComplete())
                AdvanceRound();

            OnGameChanged?.Invoke(this, EventArgs.Empty);
        }


        public void UpdateAvailableActions(PlayerModel player)
        {
            AvailableActions.Clear();

            // Only active player's turn
            if (!player.IsNext)
                return;

            if (CurrentGame.CurrentRound == BettingRound.Showdown)
                return;

            var actions = new List<ActionOption>();
            int playerIndex = CurrentGame.Players.IndexOf(player);

            // Fold is always available
            actions.Add(new ActionOption(EnumPlayerAction.Fold, 0));

            // --- PRE-FLOP BLINDS ---
            if (CurrentGame.CurrentRound == BettingRound.PreFlop)
            {
                if (playerIndex == CurrentGame.SmallBlindIndex && !player.HasPostedSmallBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.SmallBlind, CurrentGame.SmallBlind));
                    AvailableActions = actions;
                    return;
                }

                if (playerIndex == CurrentGame.BigBlindIndex && !player.HasPostedBigBlind)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.BigBlind, CurrentGame.BigBlind));
                    AvailableActions = actions;
                    return;
                }
            }

            int callAmount = CurrentGame.CurrentBet - player.CurrentBet;

            // --- Call / Check ---
            if (callAmount > 0)
            {
                if (player.RemainingStack >= callAmount)
                {
                    actions.Add(new ActionOption(EnumPlayerAction.Call, callAmount));
                }
                else
                {
                    // Player can't fully call, only all-in
                    actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));
                    AvailableActions = actions;
                    return;
                }
            }
            else
            {
                actions.Add(new ActionOption(EnumPlayerAction.Check, 0));
            }

            // --- Bet / Raise ---
            int minBetOrRaise = CurrentGame.CurrentBet == 0 ? CurrentGame.MinBet : Math.Max(CurrentGame.MinBet, callAmount);

            if (player.RemainingStack >= minBetOrRaise)
            {
                if (CurrentGame.CurrentBet == 0)
                    actions.Add(new ActionOption(EnumPlayerAction.Bet, minBetOrRaise));
                else
                    actions.Add(new ActionOption(EnumPlayerAction.Raise, minBetOrRaise));
            }
            else if (player.RemainingStack > 0)
            {
                // Player can't meet minimum bet/raise → All-in is the only option
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));
                AvailableActions = actions;
                return;
            }

            // --- All-in ---
            if (player.RemainingStack > 0 && !actions.Any(a => a.ActionType == EnumPlayerAction.AllIn))
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));

            AvailableActions = actions;
        }



        private void AdvanceRound()
        {
            // Clear per-round flags
            foreach (var p in CurrentGame.Players)
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
            CurrentGame.CurrentPlayer = GetFirstActivePlayerAfterDealer();
            CurrentGame.Players[CurrentGame.CurrentPlayer].IsNext = true;
            UpdateAvailableActions(CurrentGame.Players[CurrentGame.CurrentPlayer]);
        }



        private int GetFirstActivePlayerAfterDealer()
        {
            if (CurrentGame.Players.Count < 2)
                return 0;

            int index = (CurrentGame.DealerIndex + 1) % CurrentGame.Players.Count;

            // Find first active player (not folded, not sitting out)
            for (int i = 0; i < CurrentGame.Players.Count; i++)
            {
                var player = CurrentGame.Players[index];
                if (!player.HasFolded && !player.IsSittingOut)
                    return index;

                index = (index + 1) % CurrentGame.Players.Count;
            }

            // fallback: return dealer if everyone else folded/sitting out
            return CurrentGame.DealerIndex;
        }


        private bool IsBettingRoundComplete()
        {
            var activePlayers = CurrentGame.Players
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
            // Get all active players (not folded, not sitting out)
            var activePlayers = CurrentGame.Players.Where(p => !p.HasFolded && !p.IsSittingOut).ToList();

            // If no active players, everyone folded
            if (!activePlayers.Any())
            {
                foreach (var p in CurrentGame.Players)
                    p.Result = "Alle haben gefoldet.\nUnentschieden.";

                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();
                OnRoundFinished?.Invoke(this, EventArgs.Empty);
                return;
            }

            // If only one active player remains, they automatically win the pot
            if (activePlayers.Count == 1)
            {
                var winner = activePlayers[0];
                winner.RemainingStack += CurrentGame.Pot;
                winner.Result = $"Alle anderen haben gefoldet.\nDu gewinnst den Pot: {CurrentGame.Pot}";

                foreach (var p in CurrentGame.Players)
                {
                    if (p != winner)
                        p.Result = "Du hast gefoldet.";

                    p.IsNext = false;
                }

                CurrentGame.TheWinners = new() { new PlayerModel(winner, "Gewinner durch Fold") };
                CurrentGame.RoundLocked = false;
                CurrentGame.RoundFinished = true;
                AvailableActions.Clear();
                OnRoundFinished?.Invoke(this, EventArgs.Empty);
                return;
            }

            // If there are multiple active players, proceed with normal hand evaluation
            if (CommunityCards == null || CommunityCards.Count < 5)
                return; // wait until showdown

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

            foreach (var p in CurrentGame.Players)
            {
                var handName = bestHands.ContainsKey(p) ? bestHands[p].HandName : "Keine Hand";

                if (topPlayers.Contains(p))
                {
                    p.RemainingStack += potShare;

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
