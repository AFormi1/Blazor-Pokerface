using Pokerface.Models;

namespace Pokerface.Services
{
    public static class CardSvgProvider
    {     
     
        public static string GetBacksideSvg() => "images/cards/backside.svg";

        public static string GetFrontsideSvg(EnumCardRank rank, EnumCardSuit suit)
            => $"images/cards/{suit}_{rank}.svg";
    }
}
