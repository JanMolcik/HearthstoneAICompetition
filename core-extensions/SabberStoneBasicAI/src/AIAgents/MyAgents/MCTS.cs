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

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class MCTS
	{
		private readonly double EXPLORATION_CONSTANT = 1 / Math.Sqrt(2); // magic constant for BestChild function
		private readonly int COMPUTATIONAL_BUDGET = 2000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly Controller player;
		private readonly ChildSelector ChildSelection = new ChildSelector();
		private readonly int TurnDepth = 0;
		private readonly Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private readonly Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private readonly SelectionStrategy Selection;
		private readonly StateRateStrategy StateRate;
		private ActionEstimator ActionEstimator;
		private POGame InitialState;
		private Node Root { get; set; }

		public MCTS(POGame poGame, Dictionary<string, List<Card>> decksDict, Dictionary<string, double> probsDict,
			int turnDepth = 1, int timeBudget = 2000, SelectionStrategy selectionStrategy = SelectionStrategy.UCT,
			StateRateStrategy stateRateStrategy = StateRateStrategy.Greedy)
		{
			TurnDepth = turnDepth;
			COMPUTATIONAL_BUDGET = timeBudget;
			Selection = selectionStrategy;
			StateRate = stateRateStrategy;
			player = poGame.CurrentPlayer;
			Root = new Node(poGame, player.PlayerId);
			InitialState = poGame;
			InitializeNode(Root, InitialState);
			DecksDict = decksDict;
			ProbabilitiesDict = probsDict;
			ActionEstimator = new ActionEstimator(DecksDict, ProbabilitiesDict);
			//poGame.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
			//Console.WriteLine();
		}

		public PlayerTask Search()
		{
			List<PlayerTask> options = player.Options();
			if (options.Count == 1 && options[0].PlayerTaskType == PlayerTaskType.END_TURN) return options.First();

			StopWatch.Start();
			var selectionStrategy = SelectionStrategies.GetSelectionStrategy(Selection);
			var stateRateStrategy = StateRateStrategies.GetStateRateStrategy(StateRate);
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root);
				float delta = DefaultPolicyHeuristic(lastNode);
				Backup(lastNode, delta);
			}
			StopWatch.Stop();

			return ChildSelection.SelectBestChild(InitialState, Root, EXPLORATION_CONSTANT, player, selectionStrategy, stateRateStrategy).Action;
		}

		private Node TreePolicy(Node node)
		{
			var state = node.State;
			while (state?.State != State.COMPLETE && node.TurnDepth < TurnDepth)
			{
				if (state == null) return node;
				if (FullyExpanded(node))
				{
					var selectionStrategy = SelectionStrategies.GetSelectionStrategy(Selection);
					var stateRateStrategy = StateRateStrategies.GetStateRateStrategy(StateRate);
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
			while (child.Children.Count > 0);

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
				List<PlayerTask> actions = state.CurrentPlayer.Options();
				List<PlayerTask> oldActions = new List<PlayerTask>(actions);
				bool uncertainity = state.CurrentPlayer.Options()
					.Any(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source.Card.Name == "No Way!");

				Controller currentPlayer = state.CurrentPlayer;

				if (uncertainity)
				{
					// player's drawn cards are also unknown -> need to simulate
					actions = state.CurrentPlayer.PlayerId == player.PlayerId ?
						ActionEstimator.DrawSimulation(currentPlayer) :
						ActionEstimator.ActionEstimaton(state, currentPlayer);
				}

				var bestPair = state.Simulate(actions.Any() ? actions : oldActions)
					.Where(pair => pair.Value != null)
					.OrderBy(pair => StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy)(pair.Value, player))
					.Last();
				//Console.WriteLine("Choosing: " + bestAction);

				state = bestPair.Value;

				// this should be redundant.. but, you know.. just in case..
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

		private float DefaultPolicyHeuristic(Node node)
		{
			float result = 0;
			POGame state = node.State;
			int currentTurnDepth = node.TurnDepth;

			while (state.State != State.COMPLETE &&
				(node.Action.PlayerTaskType == PlayerTaskType.END_TURN ?
				currentTurnDepth < TurnDepth - 1 :
				currentTurnDepth < TurnDepth))
			{
				Controller currentPlayer = state.CurrentPlayer;
				List<PlayerTask> actions = currentPlayer.Options();
				List<PlayerTask> oldActions = new List<PlayerTask>(actions);
				bool uncertainity = currentPlayer.Options()
					.Any(option => option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source.Card.Name == "No Way!");

				// end potential infinite loop of 0 cost spells (happend few times with a mage)
				if (currentPlayer.CardsPlayedThisTurn.Count > 50) break;

				if (uncertainity)
				{
					// depending on active player choose one:
					// 1) simulate player's card draw
					// 2) simulate opponent's possible actions
					actions = state.CurrentPlayer.PlayerId == player.PlayerId ?
						ActionEstimator.DrawSimulation(currentPlayer) :
						ActionEstimator.ActionEstimaton(state, currentPlayer);
				}

				var bestPair = state.Simulate(actions.Any() ? actions : oldActions)
					.Where(pair => pair.Value != null)
					.OrderBy(pair => StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy)(pair.Value, currentPlayer))
					.Last();

				//Console.WriteLine(currentTurnDepth + ", " + bestPair.Key);

				if (bestPair.Key.PlayerTaskType == PlayerTaskType.END_TURN)
				{
					currentTurnDepth++;
				}

				state = bestPair.Value;

				// this should be redundant.. but, you know.. just in case..
				if (state == null) return 0.5f;
			}

			var firstPlayer = state.CurrentPlayer.PlayerId == player.PlayerId ? state.CurrentPlayer : state.CurrentOpponent;

			result = StateRateStrategies.GetStateRateStrategy(StateRateStrategy.Greedy)(state, firstPlayer);

			return result;
		}

		private void Backup(Node node, float delta)
		{
			while (node != null)
			{
				//Console.WriteLine("Backup: " + delta + ", " + node.Action);
				node.VisitedCount++;
				node.Reward += delta;
				node = node.Parent;
			}
		}

		private void InitializeNode(Node node, POGame state)
		{
			if (state != null)
			{
				var validOpts = state.CurrentPlayer.Options()
					.Where(option => !(option.HasSource && option.Source.Card.Name == "No Way!"));

				var simulations = state.Simulate(validOpts.ToList());
				simulations.Keys
					.ToList()
					.ForEach(option =>
						node.Children.Add(new Node(simulations[option], option, node, state.CurrentPlayer.PlayerId,
							node.Action?.PlayerTaskType == PlayerTaskType.END_TURN ? node.TurnDepth + 1 : node.TurnDepth)));
			}
		}

		private bool FullyExpanded(Node node)
		{
			foreach (Node child in node.Children)
			{
				if (child.Children.Count == 0 && !(child.Action.PlayerTaskType == PlayerTaskType.END_TURN && child.TurnDepth >= TurnDepth))
					return false;
			}
			return true;
		}
	}
}
