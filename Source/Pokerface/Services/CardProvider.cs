
using Pokerface.Models;
using System.Text;

namespace Pokerface.Services
{
    public class CardProvider
    {
        //ids must match with pokertable.svg
        public static readonly string[] CardElementIds =
        {
            "f1", "f2", "f3", "turn", "river",
            "p11","p12","p21","p22","p31","p32",
            "p41","p42","p51","p52","p61","p62",
            "p71","p72","p81","p82"
        };


        // maps SVG IDs -> enum
        public static readonly Dictionary<string, EnumCardPositions> SvgIdToEnumMap = new()
        {
            { "f1", EnumCardPositions.Flop1 },
            { "f2", EnumCardPositions.Flop2 },
            { "f3", EnumCardPositions.Flop3 },
            { "turn", EnumCardPositions.Turn },
            { "river", EnumCardPositions.River },

            { "p11", EnumCardPositions.Player1Card1 },
            { "p12", EnumCardPositions.Player1Card2 },
            { "p21", EnumCardPositions.Player2Card1 },
            { "p22", EnumCardPositions.Player2Card2 },
            { "p31", EnumCardPositions.Player3Card1 },
            { "p32", EnumCardPositions.Player3Card2 },
            { "p41", EnumCardPositions.Player4Card1 },
            { "p42", EnumCardPositions.Player4Card2 },
            { "p51", EnumCardPositions.Player5Card1 },
            { "p52", EnumCardPositions.Player5Card2 },
            { "p61", EnumCardPositions.Player6Card1 },
            { "p62", EnumCardPositions.Player6Card2 },
            { "p71", EnumCardPositions.Player7Card1 },
            { "p72", EnumCardPositions.Player7Card2 },
            { "p81", EnumCardPositions.Player8Card1 },
            { "p82", EnumCardPositions.Player8Card2 },
        };

        // current rects by enum
        public Dictionary<EnumCardPositions, DomRect> CardRects { get; private set; } = [];

        public void SetCardRects(Dictionary<string, DomRect> rects)
        {
            CardRects = SvgIdToEnumMap
                .Where(kv => rects.ContainsKey(kv.Key))
                .ToDictionary(kv => kv.Value, kv => rects[kv.Key]);
        }

        public DomRect GetCardRect(EnumCardPositions position)
        {
            return CardRects.TryGetValue(position, out var rect) ? rect : new DomRect();
        }

        public string GetBacksideSvg() => "images/cards/backside.svg";

        public string GetFrontsideSvg(EnumCardRank rank, EnumCardSuit suit)
            => $"images/cards/{suit}_{rank}.svg";
    }

}
