using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class ActionEstimator
	{

		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private readonly Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();
		private readonly Random Rand = new Random();

		public ActionEstimator(Dictionary<string, List<Card>> decksDict, Dictionary<string, double> probabilitiesDict)
		{
			DecksDict = decksDict;
			ProbabilitiesDict = probabilitiesDict;
		}

		public List<PlayerTask> ActionEstimaton(POGame state, Controller player)
		{
			// estimate best action for upcoming game state which depends on probability of opponent's actions
			// a.k.a. simulate his possibilities (remove incomplete information) and choose most promising (maybe more?)

			var history = player.PlayHistory;
			List<Card> playedCards = history.Select(h => h.SourceCard).ToList();
			List<PlayerTask> resultActions = new List<PlayerTask>();
			List<PlayerTask> originActions = player.Options();
			float threshold = 0.05f;

			foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
			{
				//Console.WriteLine(ProbabilitiesDict[deck.Key]);
				if (ProbabilitiesDict[deck.Key] > threshold)
				{
					List<Card> remainingCards = new List<Card>(DecksDict[deck.Key]);
					HashSet<PlayerTask> subactions = new HashSet<PlayerTask>();
					int removed = 0;

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

					IEnumerable<Card> estimatedCards =
						remainingCards
							.GroupBy(card => card.AssetId)
							.Select(group => new KeyValuePair<Card, int>(group.First(), group.Count()))
							.Where(pair => pair.Key.Cost <= player.RemainingMana)
							.OrderByDescending(pair => pair.Value)
							.Take(removed)
							.Select(pair => pair.Key);

					estimatedCards
						.ToList()
						.ForEach(card =>
						{
							IPlayable playable = Entity.FromCard(in player, Cards.FromId(card.Id));
							if (!player.HandZone.IsFull && !player.HandZone.Contains(playable))
							{
								player.HandZone.Add(playable);
							}
						});

					//Console.WriteLine(String.Format("Removed {0} unknown cards", removed));

					// adding cards to hand for simualtion
					/*
					for (int i = 0; i < removed; i++)
					{
						Card deckCard = remainingCardsWithCount.ElementAt(i).Key;

						if (deckCard.Cost <= player.RemainingMana)
						{
							if (player.HandZone.IsFull)
							{
								player.Options()
									.Where(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD)
									.ToList()
									.ForEach(option => subactions.Add(option));
								ClearHand(player);
								break;
							}

							if (deckCard.Class == player.HeroClass || deckCard.Class == CardClass.NEUTRAL)
							{
								Console.WriteLine("Adding card: " + deckCard);
								player.HandZone.Add(Entity.FromCard(in player, Cards.FromId(deckCard.Id)));
							}
						} else
						{
							if (i < remainingCards.Count)
							{

							}
						}
					}

					// choosing best subactions
					resultActions.AddRange(
						TakeRanom(subactions
							.Where(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToList(), removed)
							//.Where(action => !(action.HasSource && action.Source.ZonePosition > player.BoardZone.Count))
							//.OrderBy(action => StateRateStrategies
							//.GetStateRateStrategy(StateRateStrategy.Greedy)(state, player))
							//.TakeLast(removed)
							);
					foreach (PlayerTask task in resultActions)
					{
						if (task.HasSource && task.PlayerTaskType == PlayerTaskType.PLAY_CARD && !player.HandZone.IsFull && !player.HandZone.Contains(task.Source))
						{
							player.HandZone.Add(task.Source);
						}
					}
					*/
					//resultActions.AddRange(player.Options().Where(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD));
				}
			}

			resultActions.AddRange(player.Options());
			//resultActions.ForEach(action => Console.WriteLine(action));
			//Console.WriteLine();

			return resultActions.Count > 0 ? resultActions : originActions;
		}

		public List<PlayerTask> DrawSimulation(Controller player)
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

		private List<PlayerTask> TakeRanom(List<PlayerTask> tasks, int count)
		{
			List<PlayerTask> tasksCopy = new List<PlayerTask>(tasks);
			List<PlayerTask> resultTasks = new List<PlayerTask>();

			for (int i = 0; i < count; i++)
			{
				int randomIndex = Rand.Next(tasksCopy.Count);
				resultTasks.Add(tasksCopy[randomIndex]);
				tasksCopy.RemoveAt(randomIndex);
			}

			return resultTasks;
		}

		private void ClearHand(Controller pplayer)
		{
			while (pplayer.HandZone.Count() > 0)
			{
				pplayer.HandZone.Remove(0);
			}
		}
	}
}
