using SabberStoneBasicAI.AIAgents;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class MCTS
	{
		private readonly double EXPLORATION_CONSTANT = 1 / Math.Sqrt(2); // magic constant for BestChild function
		private readonly int COMPUTATIONAL_BUDGET = 5000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly Controller player;
		private readonly ChildSelector ChildSelection = new ChildSelector();
		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();
		private POGame InitialState;
		private Node Root { get; set; }

		public MCTS(POGame poGame, Dictionary<string, List<Card>> decksDict, Dictionary<string, double> probsDict)
		{
			Root = new Node();
			InitialState = poGame;
			InitializeNode(Root, InitialState);
			player = poGame.CurrentPlayer;
			DecksDict = decksDict;
			ProbabilitiesDict = probsDict;

			//poGame.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
			Console.WriteLine();
		}

		public PlayerTask Search()
		{
			StopWatch.Start();
			var selectionStrategy = SelectionStrategies.GetSelectionStrategy(SelectionStrategy.MaxRatioChild);
			var stateRateStrategy = StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Ejnar);
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root, state);
				double delta = DefaultPolicy(state, lastNode);
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

		private double DefaultPolicy(POGame state, Node node)
		{
			double result = -1;
			while (state.State != State.COMPLETE)
			{
				// instead of removing unknown cards I should rather try guess what is
				// in opponent's hand and simulate most probable and best actions

				List<PlayerTask> actions = state.CurrentPlayer.Options();
				int removed = actions.RemoveAll(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD && action.Source.Card.Name == "No Way!");

				if (removed > 0)
				{
					actions = ActionEstimator(state, actions, state.CurrentPlayer, removed);
				}

				// instead of random action I could be looking for best actions (really tho?)
				PlayerTask randomAction = actions[Rand.Next(actions.Count())];
				//PlayerTask bestAction = BestAction(state, actions);
				state = state.Simulate(new List<PlayerTask> { randomAction })[randomAction];

				if (state == null) return 0.5;
			}

			if (state.CurrentPlayer.PlayState == PlayState.CONCEDED || state.CurrentPlayer.PlayState == PlayState.LOST)
			{
				result = 0;
			}
			else if (state.CurrentPlayer.PlayState == PlayState.WON)
			{
				result = 1;
			}
			else if (state.CurrentPlayer.PlayState == PlayState.TIED)
			{
				result = 0.5;
			}
			return result;
		}

		private void Backup(Node node, double delta)
		{
			while (node != null)
			{
				node.VisitedCount++;
				node.Reward += delta;
				node = node.Parent;
			}
		}

		private void InitializeNode(Node node, POGame state)
		{
			if (state != null)
			{
				state.CurrentPlayer.Options().ForEach(option =>
				{
					node.Children.Add(new Node(option, node));
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
		
		private List<PlayerTask> ActionEstimator(POGame state, List<PlayerTask> actions, Controller player, int count)
		{
			// estimate best action for upcoming game state which depends on probability of opponents actions
			// a.k.a. simulate his possibilities (remove incomplete information) and choose most promising (maybe more?)
			
			var history = player.PlayHistory;
			List<PlayerTask> resultActions = new List<PlayerTask>();

			foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
			{
				//Console.WriteLine(ProbabilitiesDict[deck.Key]);
				float threshold = 0.05f;
				if (ProbabilitiesDict[deck.Key] > threshold)
				{
					Console.WriteLine(String.Format("Probability higher than {0}% with deck {1}", threshold * 100, deck.Key));
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
								//Console.WriteLine("Before clear");
								//printPlayerHand(opponent);
								//player.Options().ForEach(option => Console.Write(option + " "));

								// for opponent simulation the state of POGame with his turn is required
								state.Simulate(player.Options()).ToList().ForEach(item =>
								{
									Console.WriteLine(item.Key);
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
					//Console.WriteLine(subactions.ToString());
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
	}
}
