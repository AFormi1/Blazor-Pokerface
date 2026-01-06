using Pokerface.Enums;
using Pokerface.Models;

namespace Pokerface.Services
{
    public static class ActionResolver
    {
        public static List<ActionOption> GetLegalActions(PlayerModel player, GameContext ctx)
        {
            var actions = new List<ActionOption>();

            // Fold is always allowed
            actions.Add(new ActionOption(EnumPlayerAction.Fold, 0));

            int callAmount = ctx.CurrentBet - player.CurrentBet;

            // Check / Call
            if (callAmount <= 0)
            {
                actions.Add(new ActionOption(EnumPlayerAction.Check, 0));
            }
            else
            {
                actions.Add(new ActionOption(EnumPlayerAction.Call, callAmount));
            }

            // Bet / Raise
            if (ctx.CurrentBet == 0)
            {
                // Bet: min amount is ctx.MinBet
                actions.Add(new ActionOption(EnumPlayerAction.Bet, ctx.MinBet));
            }
            else
            {
                // Raise: min amount is either MinBet or enough to match current bet
                int minRaise = Math.Max(ctx.MinBet, callAmount);
                actions.Add(new ActionOption(EnumPlayerAction.Raise, minRaise));
            }

            // All-in
            if (player.RemainingStack > 0)
            {
                actions.Add(new ActionOption(EnumPlayerAction.AllIn, player.RemainingStack));
            }

            return actions;
        }
    }


}
