using SabberStoneBasicAI.AIAgents;
using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class MCTS
	{
		private readonly double EXPLORATION_CONSTANT = 1 / Math.Sqrt(2); // magic constant for BestChild function
		private readonly int COMPUTATIONAL_BUDGET = 10000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly Controller player;
		private readonly ChildSelector ChildSelection = new ChildSelector();
		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private readonly Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private OpponentActionsEstimator ActionEstimator;
		private POGame InitialState;
		private Node Root { get; set; }

		public MCTS(POGame poGame, Dictionary<string, List<Card>> decksDict, Dictionary<string, double> probsDict)
		{
			player = poGame.CurrentPlayer;
			Root = new Node(player.PlayerId);
			InitialState = poGame;
			InitializeNode(Root, InitialState);
			DecksDict = decksDict;
			ProbabilitiesDict = probsDict;
			ActionEstimator = new OpponentActionsEstimator(DecksDict, ProbabilitiesDict);
			//poGame.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
			Console.WriteLine();
		}

		public PlayerTask Search()
		{
			StopWatch.Start();
			var selectionStrategy = SelectionStrategies.GetSelectionStrategy(SelectionStrategy.MaxRobustChild);
			var stateRateStrategy = StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy);
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root, state);
				KeyValuePair<int, float> delta = DefaultPolicy(state, lastNode);
				Backup(lastNode, delta);
			}
			StopWatch.Stop();
			PlayerTask best = ChildSelection.SelectBestChild(InitialState, Root, EXPLORATION_CONSTANT, player, selectionStrategy, stateRateStrategy, true).Action;
			Console.WriteLine("Final task: " + best);

			return best;
		}

		private Node TreePolicy(Node node, POGame state)
		{
			while (state.State != State.COMPLETE)
			{
				if (FullyExpanded(node))
				{
					var selectionStrategy = SelectionStrategies.GetSelectionStrategy(SelectionStrategy.UCT);
					var stateRateStrategy = StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy);
					node = ChildSelection.SelectBestChild(state, node, EXPLORATION_CONSTANT, player, selectionStrategy, stateRateStrategy);
					state = state.Simulate(new List<PlayerTask> { node.Action })[node.Action];
				}
				else
				{
					return Expand(node, state);
				}
			}

			return node;
		}

		private Node Expand(Node node, POGame state)
		{
			Node child;

			do
			{
				child = node.Children[Rand.Next(node.Children.Count)];
			}
			while (child.Children.Count > 0); // unvisited child

			POGame childState = state.getCopy();
			childState = childState.Simulate(new List<PlayerTask> { child.Action })[child.Action];

			InitializeNode(child, childState);

			return child;
		}

		private KeyValuePair<int, float> DefaultPolicy(POGame state, Node node)
		{
			KeyValuePair<int, float> result = new KeyValuePair<int, float>(state.CurrentPlayer.PlayerId, 0.5f);
			while (state.State != State.COMPLETE)
			{
				// instead of removing unknown cards I should rather try guess what is
				// in opponent's hand and simulate most probable and best actions

				List<PlayerTask> actions = state.CurrentPlayer.Options();
				List<PlayerTask> oldActions = new List<PlayerTask>(actions);
				bool uncertainity = state.CurrentPlayer.Options()
					.Any(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source.Card.Name == "No Way!");

				Controller currentPlayer = state.CurrentPlayer;

				if (uncertainity)
				{
					// player's drawn cards are also unknown -> need to simulate

					actions = state.CurrentPlayer.PlayerId == player.PlayerId ? DrawSimulation(currentPlayer) : ActionEstimator.ActionEstimaton(state, currentPlayer);
					//actions.RemoveAll(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD && action.Source.Card.Name == "No Way!");
					/*actions
						.ForEach(action => Console.WriteLine(action + ", player: " + state.CurrentPlayer.PlayerId));*/
				}

				// instead of random action I could be looking for best actions
				if (actions.Any())
				{
					PlayerTask randomAction = actions[Rand.Next(actions.Count())];
					//Console.WriteLine("Choosing: " + randomAction);
					PlayerTask bestAction = actions
						.OrderBy(action => StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy)(state, player))
						.Last();
					//Console.WriteLine("Choosing: " + bestAction);

					state = state.Simulate(new List<PlayerTask> { bestAction })[bestAction];
				}
				else
				{
					PlayerTask endTurn = oldActions.First(action => action.PlayerTaskType == PlayerTaskType.END_TURN);
					state = state.Simulate(new List<PlayerTask> { endTurn })[endTurn];
				}

				if (state == null) return new KeyValuePair<int, float>(currentPlayer.PlayerId, 0.5f);
			}

			if (state.CurrentPlayer.PlayState == PlayState.CONCEDED || state.CurrentPlayer.PlayState == PlayState.LOST)
			{
				result = new KeyValuePair<int, float>(state.CurrentPlayer.PlayerId, 0);
			}
			else if (state.CurrentPlayer.PlayState == PlayState.WON)
			{
				result = new KeyValuePair<int, float>(state.CurrentPlayer.PlayerId, 1);
			}
			else if (state.CurrentPlayer.PlayState == PlayState.TIED)
			{
				result = new KeyValuePair<int, float>(state.CurrentPlayer.PlayerId, 0.5f);
			}
			return result;
		}

		private void Backup(Node node, KeyValuePair<int, float> delta)
		{
			while (node != null)
			{
				node.VisitedCount++;
				// if the delta reward corresponds to other player, just reverse the reward (zero-sum game)
				node.Reward += delta.Key == node.PlayerID ? delta.Value : Math.Abs(delta.Value - 1);
				node = node.Parent;
			}
		}

		private void InitializeNode(Node node, POGame state)
		{
			if (state != null)
			{
				state.CurrentPlayer.Options().ForEach(option =>
				{
					node.Children.Add(new Node(option, node, state.CurrentPlayer.PlayerId));
				});
			}
		}

		private bool FullyExpanded(Node node)
		{
			foreach (Node child in node.Children)
			{
				if (child.Children.Count == 0) return false;
			}
			return true;
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

		/*
		private List<PlayerTask> ActionEstimator(POGame state, List<PlayerTask> actions, Controller player, int count)
		{
			// estimate best action for upcoming game state which depends on probability of opponents actions
			// a.k.a. simulate his possibilities (remove incomplete information) and choose most promising (maybe more?)

			//Console.WriteLine("Estimator " + player.PlayerId);
			var history = player.PlayHistory;
			List<PlayerTask> resultActions = new List<PlayerTask>();
			resultActions.AddRange(actions);

			foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
			{
				//Console.WriteLine(ProbabilitiesDict[deck.Key]);
				float threshold = 0.03f;
				if (ProbabilitiesDict[deck.Key] > threshold)
				{
					Console.WriteLine(String.Format("Probability higher than {0}% with deck {1} player {2} and count {3}", threshold * 100, deck.Key, player.PlayerId, count));
					var playedCards = history.Select(h => h.SourceCard).ToList();
					var deckCards = deck.Value;

					var subactions = new Dictionary<PlayerTask, POGame>();
					clearHand(player);

					//Console.WriteLine(opponent);

					// removing played cards
					playedCards.ForEach(playedCard =>
					{
						deckCards.Remove(playedCard);
					});

					// adding cards to hand for simualtion
					deckCards.ForEach(deckCard =>
					{
						if (deckCard.Cost <= player.BaseMana)
						{
							if (!player.HandZone.IsFull)
							{
								player.HandZone.Add(Entity.FromCard(in player, Cards.FromId(deckCard.Id)));
								//Console.WriteLine("Add " + deckCard.Name);
							}
							else
							{
								// for opponent's simulation the state of POGame with his turn is required
								state.Simulate(player.Options()).ToList().ForEach(item =>
								{
									//Console.WriteLine(item.Key);
									subactions.Add(item.Key, item.Value);
								});
								clearHand(player);

								//Console.WriteLine(opponent.HandZone.Count());
							}
						}
					});
					resultActions.AddRange(
						subactions
							.OrderBy(action => StateRateStrategies
								.GetStateRateStrategy(StateRateStrategy.Greedy)(action.Value, player))
							.TakeLast(count)
							.Select(action => action.Key)
							);
					resultActions.ForEach(action => Console.WriteLine(action));
					Console.WriteLine();
				}
			}

			return resultActions.Count > 0 ? resultActions : actions;

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
		*/

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
