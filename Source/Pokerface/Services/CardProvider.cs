
using Pokerface.Models;
using System.Text;

namespace Pokerface.Services
{
    public class CardProvider
    {

        public CardProvider()
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

        public record CardLayout(double X, double Y, double Rotation);

        public readonly Dictionary<EnumCardPositions, CardLayout> Positions =
            new()
            {
                // ===== Community cards (centered row) =====
                { EnumCardPositions.Flop1,  new(38, 50, 0) },
                { EnumCardPositions.Flop2,  new(45, 50, 0) },
                { EnumCardPositions.Flop3,  new(50, 50, 0) },
                { EnumCardPositions.Turn,   new(50, 50, 0) },
                { EnumCardPositions.River,  new(50, 50, 0) },

                // ===== Top side (3 players, 2 cards each) =====
                { EnumCardPositions.Player1Card1, new(28, 18, 0) },
                { EnumCardPositions.Player1Card2, new(31, 18, 0) },

                { EnumCardPositions.Player2Card1, new(45, 18, 0) },
                { EnumCardPositions.Player2Card2, new(48, 18, 0) },

                { EnumCardPositions.Player3Card1, new(62, 18, 0) },
                { EnumCardPositions.Player3Card2, new(65, 18, 0) },

                // ===== Bottom side (mirrored) =====
                { EnumCardPositions.Player4Card1, new(28, 82, 0) },
                { EnumCardPositions.Player4Card2, new(31, 82, 0) },

                { EnumCardPositions.Player5Card1, new(45, 82, 0) },
                { EnumCardPositions.Player5Card2, new(48, 82, 0) },

                { EnumCardPositions.Player6Card1, new(62, 82, 0) },
                { EnumCardPositions.Player6Card2, new(65, 82, 0) },

                // ===== Left round side =====
                { EnumCardPositions.Player7Card1, new(12, 47, -90) },
                { EnumCardPositions.Player7Card2, new(12, 53, -90) },

                // ===== Right round side =====
                { EnumCardPositions.Player8Card1, new(88, 47, 90) },
                { EnumCardPositions.Player8Card2, new(88, 53, 90) }
            };

    }
}
