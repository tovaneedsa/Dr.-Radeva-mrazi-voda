namespace Santase.AI.ConsoleWebPlayer
{
    using Common;
    using Helpers;
    using Logic;
    using Logic.Cards;
    using Logic.Players;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class ConsoleWebPlayer : BasePlayer
    {
        public ConsoleWebPlayer()
            : this(WebPlayerConstants.BotName)
        {
        }

        public ConsoleWebPlayer(string name)
        {
            this.Name = name;
        }

        public override string Name { get; }

        private readonly ICollection<Card> playedCards = new List<Card>();

        private readonly OpponentSuitCardsProvider opponentSuitCardsProvider = new OpponentSuitCardsProvider();

        public override PlayerAction GetTurn(PlayerTurnContext context)
        {
            if (this.PlayerActionValidator.IsValid(PlayerAction.ChangeTrump(), context, this.Cards))
            {
                return this.ChangeTrump(context.TrumpCard);
            }

            if (this.CloseGame(context))
            {
                return this.CloseGame();
            }

            return this.ChooseCard(context);
        }

        public override void EndRound()
        {
            this.playedCards.Clear();
            base.EndRound();
        }

        public override void EndTurn(PlayerTurnContext context)
        {
            this.playedCards.Add(context.FirstPlayedCard);
            this.playedCards.Add(context.SecondPlayedCard);
        }

        private bool CloseGame(PlayerTurnContext context)
        {
            // If we have 61 points in the hand, it is possible to win the game with it;
            int currentAllPoints = 0;
            foreach (var card in this.Cards)
            {
                currentAllPoints += card.GetValue();
                if (card.Type == CardType.Queen && this.AnnounceValidator.GetPossibleAnnounce(this.Cards, card, context.TrumpCard) == Announce.Forty)
                {
                    currentAllPoints += 40;
                }
            }

            var shouldCloseGame = this.PlayerActionValidator.IsValid(PlayerAction.CloseGame(), context, this.Cards)
                                  && (this.Cards.Count(x => x.Suit == context.TrumpCard.Suit) == WebPlayerConstants.HasEnoughTrumpCards
                                  || currentAllPoints >= WebPlayerConstants.MinimumPointsForClosingGame);
            if (shouldCloseGame)
            {
                GlobalStats.GamesClosedByPlayer++;
            }

            return shouldCloseGame;
        }

        // TODO: Improve choosing best card to play
        // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
        // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
        // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
        // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
        // XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
        private PlayerAction ChooseCard(PlayerTurnContext context)
        {
            var orderedCardsByPower = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards)
                                                      .OrderByDescending(x => x.GetValue())
                                                      .ToArray();

            if (context.State.ShouldObserveRules)
            {
                if (context.IsFirstPlayerTurn)
                {
                    return this.ChooseCardWhenPlayingFirstAndWeHaveTheSameSuitCard(context, orderedCardsByPower);
                }
                else
                {
                    return this.ChooseCardWhenPlayingSecond(context, orderedCardsByPower);
                }
            }
            else
            {
                if (context.IsFirstPlayerTurn)
                {
                    return this.ChooseCardWhenPlayingFirst(context, orderedCardsByPower);
                }
                else
                {
                    return this.ChooseCardWhenPlayingSecond(context, orderedCardsByPower);
                }

            }
        }

        private PlayerAction ChooseCardWhenPlayingFirst(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            // Announce 40 or 20 if possible
            var action = this.TryToAnnounceTwentyOrFourty(context, possibleCardsToPlay);
            if (action != null)
            {
                return action;
            }

            // If the player is close to the win => play trump card which will surely win the trick
            var opponentBiggestTrumpCard = this.opponentSuitCardsProvider.GetOpponentCards(
                                           this.Cards,
                                           this.playedCards,
                                           context.TrumpCard,
                                           context.TrumpCard.Suit)
                                           .OrderByDescending(x => x.GetValue()).FirstOrDefault();

            var myBiggestTrumpCard = possibleCardsToPlay.Where(x => x.Suit == context.TrumpCard.Suit)
                                                        .OrderByDescending(x => x.GetValue())
                                                        .FirstOrDefault();

            if (context.FirstPlayerRoundPoints >= 64 - myBiggestTrumpCard?.GetValue())
            {
                if (opponentBiggestTrumpCard == null || myBiggestTrumpCard.GetValue() > opponentBiggestTrumpCard.GetValue())
                {
                    return this.PlayCard(myBiggestTrumpCard);
                }
            }

            // Smallest non-trump card from the shortest opponent suit
            var cardToPlay = possibleCardsToPlay.Where(x => x.Suit != context.TrumpCard.Suit)
                            .OrderBy(x => this.opponentSuitCardsProvider.GetOpponentCards(
                                          this.Cards,
                                          this.playedCards,
                                          context.TrumpCard,
                                          x.Suit).Count)
                                                  .ThenBy(x => x.GetValue())
                                                  .FirstOrDefault();

            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            // Should never happen
            cardToPlay = possibleCardsToPlay.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(cardToPlay);
        }

        private PlayerAction ChooseCardWhenPlayingFirstAndWeHaveTheSameSuitCard(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            // Find card that will surely win the trick
            var opponentHasTrump = this.opponentSuitCardsProvider.GetOpponentCards(
                                   this.Cards,
                                   this.playedCards,
                                   context.CardsLeftInDeck == 0 ? null : context.TrumpCard,
                                   context.TrumpCard.Suit).Any();

            var trumpCard = this.GetCardWhichWillSurelyWinTheTrick(
                                 context.TrumpCard.Suit,
                                 context.CardsLeftInDeck == 0 ? null : context.TrumpCard,
                                 opponentHasTrump);
            if (trumpCard != null)
            {
                return this.PlayCard(trumpCard);
            }

            foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            {
                var possibleCard = this.GetCardWhichWillSurelyWinTheTrick(
                                        suit,
                                        context.CardsLeftInDeck == 0 ? null : context.TrumpCard,
                                        opponentHasTrump);

                if (possibleCard != null)
                {
                    return this.PlayCard(possibleCard);
                }
            }

            // Announce 40 or 20 if possible
            var action = this.TryToAnnounceTwentyOrFourty(context, possibleCardsToPlay);
            if (action != null)
            {
                return action;
            }

            // Smallest non-trump card
            var cardToPlay = possibleCardsToPlay.Where(x => x.Suit != context.TrumpCard.Suit)
                              .OrderBy(x => x.GetValue())
                              .FirstOrDefault();

            if (cardToPlay != null)
            {
                return this.PlayCard(cardToPlay);
            }

            // Smallest card
            cardToPlay = possibleCardsToPlay.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(cardToPlay);
        }

        private Card GetCardWhichWillSurelyWinTheTrick(CardSuit suit, Card trumpCard, bool opponentHasTrump)
        {
            var myBiggestCard =
                this.Cards.Where(x => x.Suit == suit).OrderByDescending(x => x.GetValue()).FirstOrDefault();

            if (myBiggestCard == null)
            {
                return null;
            }

            var opponentBiggestCard =
                this.opponentSuitCardsProvider.GetOpponentCards(this.Cards, this.playedCards, trumpCard, suit)
                    .OrderByDescending(x => x.GetValue())
                    .FirstOrDefault();

            if (!opponentHasTrump && opponentBiggestCard == null)
            {
                return myBiggestCard;
            }

            if (opponentBiggestCard != null && opponentBiggestCard.GetValue() < myBiggestCard.GetValue())
            {
                return myBiggestCard;
            }

            return null;
        }

        private PlayerAction ChooseCardWhenPlayingSecond(PlayerTurnContext context, ICollection<Card> orderedCardsByPower)
        {
            ICollection<Card> trumpCards = orderedCardsByPower
                                                .Where(x => x.Suit == context.TrumpCard.Suit)
                                                .ToArray();
                                                
            // If we have a bigger card of the same suit, we play it.
            var biggerCardOfSameSuit = orderedCardsByPower
                    .Where(x => x.Suit == context.FirstPlayedCard.Suit && x.GetValue() > context.FirstPlayedCard.GetValue())
                    .FirstOrDefault();

            // If we have a card of the same suit, we play it.
            if (biggerCardOfSameSuit != null)
            {
                return this.PlayCard(biggerCardOfSameSuit);
            }
            // If we don't have a card of the same suit, we play the smallest trump card
            else
            {
                if (context.FirstPlayedCard.Type == CardType.Ten || context.FirstPlayedCard.Type == CardType.Ace)
                {
                    if (trumpCards.Count > 0)
                    {
                        return this.PlayCard(trumpCards.LastOrDefault());
                    }
                    else
                    {
                        return this.PlayCard(orderedCardsByPower.LastOrDefault());
                    }
                }

                return this.PlayCard(orderedCardsByPower.LastOrDefault());
            }
        }

        private PlayerAction TryToAnnounceTwentyOrFourty(PlayerTurnContext context, ICollection<Card> possibleCardsToPlay)
        {
            // Choose card with announce 40 if possible
            foreach (var card in possibleCardsToPlay)
            {
                if (card.Type == CardType.Queen
                    && this.AnnounceValidator.GetPossibleAnnounce(this.Cards, card, context.TrumpCard) == Announce.Forty)
                {
                    return this.PlayCard(card);
                }
            }

            // Choose card with announce 20 if possible
            foreach (var card in possibleCardsToPlay)
            {
                if (card.Type == CardType.Queen
                    && this.AnnounceValidator.GetPossibleAnnounce(this.Cards, card, context.TrumpCard) == Announce.Twenty)
                {
                    return this.PlayCard(card);
                }
            }

            return null;
        }
    }
}
