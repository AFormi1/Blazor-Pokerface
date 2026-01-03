
using Pokerface.Models;
using System.Text;

namespace Pokerface.Services
{
    public class CardTemplateProvider
    {
     
        public CardTemplateProvider()
        {

        }

        public string GetBacksideSvg()
        {
            return "images/cards/backside.svg";
        }

        public string GetFrontsideSvg(EnumCardRank rank, EnumCardSuit suit)
        {
            var rankName = rank.ToString();
            var suitName = suit.ToString();

            return $"images/cards/{suitName}_{rankName}.svg";
        }

    }
}
