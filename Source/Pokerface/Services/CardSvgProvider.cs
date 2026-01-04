
using Pokerface.Models;
using System.Text;

namespace Pokerface.Services
{
    public class CardSvgProvider
    {     
     
        public string GetBacksideSvg() => "images/cards/backside.svg";

        public string GetFrontsideSvg(EnumCardRank rank, EnumCardSuit suit)
            => $"images/cards/{suit}_{rank}.svg";
    }

}
