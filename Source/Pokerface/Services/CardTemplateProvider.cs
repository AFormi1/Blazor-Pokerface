
using System.Text;

namespace Pokerface.Services
{
    public class CardTemplateProvider
    {
        private Dictionary<string, string> _backgroundCache = new();
        private Dictionary<string, string> _frontCache = new();

        public CardTemplateProvider()
        {

        }

        public void Initialize()
        {
            var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/cards");
            Directory.CreateDirectory(outputFolder);

            // For the back, you might just want one standard back
            const string backKey = "standard_back";
            if (!_backgroundCache.ContainsKey(backKey))
            {
                _backgroundCache[backKey] = GenerateBackSvg();
                var filename = Path.Combine(outputFolder, $"standard_back.svg");
                File.WriteAllText(filename, _backgroundCache[backKey]);
            }

            // Optional: you could pre-generate front cards here if you like
            // Example: for all suits and ranks
            var suits = new[] { "♠", "♥", "♦", "♣" };
            var ranks = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

            foreach (var suit in suits)
            {
                foreach (var rank in ranks)
                {
                    var key = $"{rank}_{suit}";
                    if (!_frontCache.ContainsKey(key))
                        _frontCache[key] = GenerateFrontSvg(rank, suit);
                }
            }
        }


        public string GetCardBackground(string key)
        {
            if (!_backgroundCache.ContainsKey(key))
                _backgroundCache[key] = GenerateBackSvg();
            return _backgroundCache[key];
        }

        public string GetCardFront(string rank, string suit)
        {
            var key = $"{rank}_{suit}";
            if (!_frontCache.ContainsKey(key))
                _frontCache[key] = GenerateFrontSvg(rank, suit);
            return _frontCache[key];
        }


        public string GenerateBackSvg(int width = 510, int height = 800)
        {
            var sb = new StringBuilder();

            // SVG header
            sb.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 {width} {height}"" width=""{width}"" height=""{height}"">");

            // Transparent full viewport background
            sb.AppendLine($@"  <rect x=""0"" y=""0"" width=""{width}"" height=""{height}"" fill=""transparent"" />");

            // Portrait card rectangle, slightly smaller than viewport
            sb.AppendLine($@"  <rect x=""2"" y=""2"" width=""{width - 4}"" height=""{height - 4}"" rx=""40"" ry=""40"" style=""fill:#AAAAAA; stroke:#555555; stroke-width:4"" />");

            sb.AppendLine("</svg>");

            return sb.ToString();
        }


        public string GenerateFrontSvg(string rank, string suit, int width = 60, int height = 90)
        {
            return string.Empty;
        }
    }
}
