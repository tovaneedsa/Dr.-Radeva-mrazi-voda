namespace Santase.Logic.PlayerActionValidate
{
    using System.Collections.Generic;

    using Cards;
    using Players;

    public interface IPlayerActionValidator
    {
        bool IsValid(PlayerAction action, PlayerTurnContext context, ICollection<Card> playerCards);

        ICollection<Card> GetPossibleCardsToPlay(PlayerTurnContext context, ICollection<Card> playerCards);
    }
}
