using Microsoft.AspNetCore.Components;
using Pokerface.Enums;

namespace Pokerface.Services
{
    public static class CardSvgProvider
    {
        public static string GetBacksideSvg()
            => "images/cards/backside.svg";

        public static string GetFrontsideSvg(EnumCardSuit suit, EnumCardRank rank)
            => $"images/cards/{suit}_{rank}.svg";
    }

}
