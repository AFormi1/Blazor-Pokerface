using Pokerface.Enums;

namespace Pokerface.Models
{
    public class ActionOption
    {
        public EnumPlayerAction ActionType { get; }
        public int RequiredAmount { get; set; }
        public string Label => ActionType.ToString();

        public int SelectedAmount { get; set; }

        public ActionOption(EnumPlayerAction actionType, int requiredAmount)
        {
            ActionType = actionType;
            RequiredAmount = requiredAmount;
        }
    }

}
