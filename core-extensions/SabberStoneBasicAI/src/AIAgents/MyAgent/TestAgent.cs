using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class TestAgent : AbstractAgent
	{
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private readonly string deckName = "MidrangeBuffPaladin";
		private readonly Random Rand = new Random();

		public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}

		public override void InitializeAgent()
		{
			Type deckType = typeof(Decks);

			/* -------------- Constructing possible decks ------------------- */

			foreach (string deckName in DeckNames)
			{
				var cards = (IEnumerable<Card>)deckType
					.GetProperty(deckName)
					.GetGetMethod()
					.Invoke(null, null);

				//cards.ToList().ForEach(c => Console.WriteLine(c));
				DecksDict.Add(deckName, cards.ToList());
			}

			//DecksDict.Values.First().ForEach(c => Console.WriteLine(c));
		}

		public override void InitializeGame()
		{
		}

		public override PlayerTask GetMove(POGame poGame)
		{
			var player = poGame.CurrentPlayer;
			var validOpts = poGame.Simulate(player.Options()).Where(x => x.Value != null);
			/*
			Co chci udelat:
			 - otestovat simulaci card draw
			    - simulovat akce
			 - otestovat simulaci oponentových karet
			 */
			//var actions = DrawSimulation();
			int optionsCount = validOpts.Count();

			var action = validOpts.Any() ?
				validOpts.Select(option => Score(option, poGame.CurrentPlayer.PlayerId, (optionsCount >= 5) ? ((optionsCount >= 25) ? 1 : 2) : 3)).OrderBy(pair => pair.Value).Last().Key :
				player.Options().First(option => option.PlayerTaskType == PlayerTaskType.END_TURN);

			//Console.WriteLine("TestAgent: " + action);
			return action;

		}


		private KeyValuePair<PlayerTask, int> Score(KeyValuePair<PlayerTask, POGame> state, int player_id, int max_depth = 5)
		{
			int max_score = Int32.MinValue;
			if (max_depth > 0)
			{
				bool uncertainity = state.Value.CurrentPlayer.Options()
					.Any(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source.Card.Name == "No Way!");

				Controller player = state.Value.CurrentPlayer.Clone(state.Value.getGame());

				List<PlayerTask> actions = null;

				if (uncertainity)
				{
					actions = state.Value.CurrentPlayer.PlayerId == player_id ?
						DrawSimulation(player) :
						ActionEstimator(state.Value, player);
				}
				else
				{
					actions = state.Value.CurrentPlayer.Options();
				}

				var subactions = state.Value.Simulate(actions).Where(x => x.Value != null);

				foreach (var subaction in subactions)
					max_score = Math.Max(max_score, Score(subaction, player_id, max_depth - 1).Value);

			}
			max_score = Math.Max(max_score, Score(state.Value, player_id));
			return new KeyValuePair<PlayerTask, int>(state.Key, max_score);
		}

		private int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;

			return new MyScore { Controller = p }.Rate();
		}

		private List<PlayerTask> ActionEstimator(POGame state, Controller player)
		{
			// estimate best action for upcoming game state which depends on probability of opponent's actions
			// a.k.a. simulate his possibilities (remove incomplete information) and choose most promising (maybe more?)

			//Console.WriteLine("Estimator " + player.PlayerId);
			var history = player.PlayHistory;
			List<Card> remainingCards = new List<Card>(DecksDict[deckName]);
			List<Card> playedCards = history.Select(h => h.SourceCard).ToList();
			int removed = 0;

			HashSet<PlayerTask> subactions = new HashSet<PlayerTask>();
			List<PlayerTask> resultActions = new List<PlayerTask>();

			//Console.WriteLine(opponent);

			// removing played cards
			foreach (Card playedCard in playedCards)
			{
				remainingCards.Remove(playedCard);
			}

			// removing unknown cards from hand
			for (int i = 0; i < player.HandZone.Count; i++)
			{
				IPlayable handCard = player.HandZone[i];
				if (handCard.Card.Name == "No Way!")
				{
					player.HandZone.Remove(i);
					removed++;
					// performing remove on the iterated object so I need to adjust
					// the iterator to reflect the object's items count change
					i--;
				}
				else
				{
					remainingCards.Remove(handCard.Card);
				}
			}

			//Console.WriteLine(String.Format("Removed {0} unknown cards", removed));

			// adding cards to hand for simualtion
			foreach (Card deckCard in remainingCards)
			{
				if (deckCard.Cost <= player.BaseMana)
				{
					if (player.HandZone.IsFull)
					{
						player.Options()
							.Where(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD)
							.ToList()
							.ForEach(option => subactions.Add(option));
						clearHand(player);
					}

					player.HandZone.Add(Entity.FromCard(in player, Cards.FromId(deckCard.Id)));
				}
				else
				{
					player.Options()
						.Where(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD)
						.ToList()
						.ForEach(option => subactions.Add(option));
					clearHand(player);
					break;
				}
			};

			// choosing best subactions
			resultActions.AddRange(
				subactions
					.Where(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD)
					.Where(action => !(action.HasSource && action.Source.ZonePosition > player.BoardZone.Count))
					.OrderBy(action => StateRateStrategies
						.GetStateRateStrategy(StateRateStrategy.Greedy)(state, player))
					.TakeLast(removed)
					);
			resultActions.AddRange(player.Options());

			foreach (PlayerTask task in resultActions)
			{
				if (task.HasSource && task.PlayerTaskType == PlayerTaskType.PLAY_CARD)
				{
					player.HandZone.Add(task.Source);
				}
			}

			//resultActions.ForEach(action => Console.WriteLine(action));
			//Console.WriteLine();

			return resultActions.Count > 0 ? resultActions : player.Options();

			void clearHand(Controller pplayer)
			{
				while (pplayer.HandZone.Count() > 0)
				{
					pplayer.HandZone.Remove(0);
				}
				//Console.WriteLine("Hand cleared: " + pplayer.HandZone.Count());
				//printPlayerHand(pplayer);
			}
		}


		private List<PlayerTask> DrawSimulation(Controller player)
		{
			//Console.WriteLine("Draw Simulation:");
			var uncertainActions = player.Options();
			var history = player.PlayHistory;
			List<Card> remainingCards = new List<Card>(player.DeckCards);
			var playedCards = history.Select(h => h.SourceCard).ToList();
			int removed = 0;
			// filter played cards and cards in hand

			foreach (Card playedCard in playedCards)
			{
				remainingCards.Remove(playedCard);
			}

			for (int i = 0; i < player.HandZone.Count; i++)
			{
				IPlayable handCard = player.HandZone[i];
				if (handCard.Card.Name == "No Way!")
				{
					//Console.WriteLine("Removing: " + handCard);
					player.HandZone.Remove(i);
					removed++;
					i--;
				}
				else
				{
					remainingCards.Remove(handCard.Card);
				}
			}

			//Console.WriteLine(player.HandZone.Count);

			for (int i = 0; i < removed; i++)
			{
				if (!player.HandZone.IsFull && remainingCards.Any())
				{
					int randomIndex = Rand.Next(remainingCards.Count);
					player.HandZone.Add(Entity.FromCard(in player, remainingCards[randomIndex]));
					remainingCards.RemoveAt(randomIndex);
				}
				else break;
			}

			//player.Options().ForEach(option => Console.WriteLine(option));

			return player.Options();
		}

		private bool ContainsCard(List<Card> deck, Card card)
		{
			foreach (Card deckCard in deck)
			{
				if (deckCard.AssetId == card.AssetId) return true;
			}

			return false;
		}

		private bool HandContainsCard(HandZone hand, Card card)
		{
			foreach (IPlayable handCard in hand)
			{
				if (handCard.Card.AssetId == card.AssetId) return true;
			}

			return false;
		}
	}
}
