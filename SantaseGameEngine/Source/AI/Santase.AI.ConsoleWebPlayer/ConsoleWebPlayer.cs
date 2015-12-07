namespace Santase.AI.ConsoleWebPlayer
{
    using Common;
    using Logic;
    using Logic.Cards;
    using Logic.Players;
    using Helpers;

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

        private readonly OpponentCards opponentCards = new OpponentCards();

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

        private PlayerAction ChooseCard(PlayerTurnContext context)
        {
            var orderedCardsByPower = this.PlayerActionValidator.GetPossibleCardsToPlay(context, this.Cards)
                                                      .OrderByDescending(x => x.GetValue())
                                                      .ToArray();

            if (context.State.ShouldObserveRules)
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

        private PlayerAction ChooseCardWhenPlayingFirst(PlayerTurnContext context, ICollection<Card> orderedCardsByPower)
        {
            var action = this.TryToAnnounceTwentyOrFourty(context, orderedCardsByPower);
            if (action != null)
            {
                return action;
            }

            var opponentBiggestTrumpCard = this.opponentCards.GetOpponentCards(
                                           this.Cards,
                                           this.playedCards,
                                           context.TrumpCard,
                                           context.TrumpCard.Suit)
                                           .OrderByDescending(x => x.GetValue())
                                           .FirstOrDefault();

            var myBiggestTrumpCard = orderedCardsByPower
                                            .Where(x => x.Suit == context.TrumpCard.Suit)    
                                            .FirstOrDefault();

            if (context.FirstPlayerRoundPoints >= 64 - myBiggestTrumpCard?.GetValue())
            {
                if (opponentBiggestTrumpCard == null || myBiggestTrumpCard.GetValue() > opponentBiggestTrumpCard.GetValue())
                {
                    return this.PlayCard(myBiggestTrumpCard);
                }
            }

            // If we have Ace and opponent doesn't has trump cards - we play them for sure points
            // TODO: Make HQC
            ICollection<Card> ourAces = orderedCardsByPower
                                    .Where(x => x.Type == CardType.Ace)
                                    .ToArray();

            if (ourAces.Count > 0 && opponentBiggestTrumpCard == null)
            {
                foreach (var ace in ourAces)
                {
                    if (this.playedCards.Contains(new Card(ace.Suit, CardType.Ace)))
                    {
                        return this.PlayCard(ace);
                    }
                }
            }

            ICollection<Card> ourTens = orderedCardsByPower
                                                .Where(x => x.Type == CardType.Ten)
                                                .ToArray();

            if (ourTens.Count > 0 && opponentBiggestTrumpCard == null)
            {
                foreach (var ten in ourTens)
                {
                    if (this.playedCards.Contains(new Card(ten.Suit, CardType.Ace)))
                    {
                        return this.PlayCard(ten);
                    }
                }
            }

            ICollection<Card> ourKings = orderedCardsByPower
                                    .Where(x => x.Type == CardType.King)
                                    .ToArray();

            if (ourKings.Count > 0)
            {
                foreach (var king in ourKings)
                {
                    if (this.playedCards.Contains(new Card(king.Suit, CardType.Ace))
                        && this.playedCards.Contains(new Card(king.Suit, CardType.Ten)))
                    {
                        return this.PlayCard(king);
                    }
                }
            }

            ICollection<Card> ourQueens = orderedCardsByPower
                        .Where(x => x.Type == CardType.Queen)
                        .ToArray();

            if (ourQueens.Count > 0)
            {
                foreach (var queen in ourQueens)
                {
                    if (this.playedCards.Contains(new Card(queen.Suit, CardType.Ace))
                        && this.playedCards.Contains(new Card(queen.Suit, CardType.Ten))
                        && this.playedCards.Contains(new Card(queen.Suit, CardType.King)))
                    {
                        return this.PlayCard(queen);
                    }
                }
            }

            ICollection<Card> ourJacks = orderedCardsByPower
            .Where(x => x.Type == CardType.Jack)
            .ToArray();

            if (ourJacks.Count > 0)
            {
                foreach (var jack in ourJacks)
                {
                    if (this.playedCards.Contains(new Card(jack.Suit, CardType.Ace))
                        && this.playedCards.Contains(new Card(jack.Suit, CardType.Ten))
                        && this.playedCards.Contains(new Card(jack.Suit, CardType.King))
                        && this.playedCards.Contains(new Card(jack.Suit, CardType.Queen)))
                    {
                        return this.PlayCard(jack);
                    }
                }
            }

            var lastCardToPlay = orderedCardsByPower.Where(x => x.Suit != context.TrumpCard.Suit)
                            .OrderBy(x => this.opponentCards.GetOpponentCards(
                                          this.Cards,
                                          this.playedCards,
                                          context.TrumpCard,
                                          x.Suit).Count)
                                                  .ThenBy(x => x.GetValue())
                                                  .FirstOrDefault();

            if (lastCardToPlay != null)
            {
                return this.PlayCard(lastCardToPlay);
            }

            lastCardToPlay = orderedCardsByPower.OrderBy(x => x.GetValue()).FirstOrDefault();
            return this.PlayCard(lastCardToPlay);
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
                // We will use trump cards only on strong opponent cards
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
