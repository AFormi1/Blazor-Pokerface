using Pokerface.Enums;

namespace Pokerface.Models
{
    public class ActionOption
    {
        public EnumPlayerAction ActionType { get; }
        public bool RequiresAmount { get; }

        public string Label => ActionType.ToString();

        public ActionOption(EnumPlayerAction actionType, bool requiresAmount = false)
        {
            ActionType = actionType;
            RequiresAmount = requiresAmount;
        }
    }

}
