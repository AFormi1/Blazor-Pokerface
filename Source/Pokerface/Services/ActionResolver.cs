using Pokerface.Enums;
using Pokerface.Models;

namespace Pokerface.Services
{
    public static class ActionResolver
    {
        public static List<ActionOption> GetLegalActions(
            PlayerModel player,
            GameContext ctx)
        {
            var actions = new List<ActionOption>();

            // Fold is almost always allowed
            actions.Add(new ActionOption(EnumPlayerAction.Fold));

            if (player.CurrentBet == ctx.CurrentBet)
            {
                actions.Add(new ActionOption(EnumPlayerAction.Check));
            }
            else if (player.CurrentBet < ctx.CurrentBet)
            {
                actions.Add(new ActionOption(EnumPlayerAction.Call));
            }

            if (ctx.CurrentBet == 0)
            {
                actions.Add(new ActionOption(EnumPlayerAction.Bet, requiresAmount: true));
            }
            else
            {
                actions.Add(new ActionOption(EnumPlayerAction.Raise, requiresAmount: true));
            }

            // All-in is always possible if player has chips
            if (player.RemainingBet > 0)
            {
                actions.Add(new ActionOption(EnumPlayerAction.AllIn));
            }

            return actions;
        }
    }

}
