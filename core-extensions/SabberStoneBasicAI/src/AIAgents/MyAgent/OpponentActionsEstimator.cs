using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class OpponentActionsEstimator
	{

		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private readonly Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();


		public OpponentActionsEstimator(Dictionary<string, List<Card>> decksDict, Dictionary<string, double> probabilitiesDict)
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
			float threshold = 0.1f;

			foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
			{
				//Console.WriteLine(ProbabilitiesDict[deck.Key]);
				if (ProbabilitiesDict[deck.Key] > threshold)
				{
					List<Card> remainingCards = new List<Card>(DecksDict[deck.Key]);
					//Console.WriteLine("Estimator " + player.PlayerId);
					int removed = 0;

					HashSet<PlayerTask> subactions = new HashSet<PlayerTask>();

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

							if (deckCard.Class == player.HeroClass || deckCard.Class == CardClass.NEUTRAL)
							{
								player.HandZone.Add(Entity.FromCard(in player, Cards.FromId(deckCard.Id)));
							}
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
							//.Where(action => !(action.HasSource && action.Source.ZonePosition > player.BoardZone.Count))
							.OrderBy(action => StateRateStrategies
								.GetStateRateStrategy(StateRateStrategy.Greedy)(state, player))
							.TakeLast(removed)
							);

					foreach (PlayerTask task in resultActions)
					{
						if (task.HasSource && task.PlayerTaskType == PlayerTaskType.PLAY_CARD)
						{
							if (!player.HandZone.Contains(task.Source) && !player.HandZone.IsFull)
								player.HandZone.Add(task.Source);
						}
					}
				}
			}

			resultActions.AddRange(player.Options());
			//resultActions.ForEach(action => Console.WriteLine(action));
			//Console.WriteLine();

			return resultActions.Count > 0 ? resultActions : player.Options();

			void clearHand(Controller pplayer)
			{
				while (pplayer.HandZone.Count() > 0)
				{
					pplayer.HandZone.Remove(0);
				}
			}
		}
	}
}
