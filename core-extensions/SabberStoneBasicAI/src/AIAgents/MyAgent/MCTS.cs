using SabberStoneBasicAI.AIAgents;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
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
		private readonly int COMPUTATIONAL_BUDGET = 10000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly Controller player;
		private readonly ChildSelector ChildSelection = new ChildSelector();
		private POGame InitialState;
		private Node Root { get; set; }

		public MCTS(POGame poGame)
		{
			Root = new Node();
			InitialState = poGame;
			InitializeNode(Root, InitialState);
			player = poGame.CurrentPlayer;

			//poGame.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
			Console.WriteLine();
		}

		public PlayerTask Search()
		{
			StopWatch.Start();
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root, state);
				double delta = DefaultPolicy(state, lastNode);
				Backup(lastNode, delta);
			}
			StopWatch.Stop();
			PlayerTask best = ChildSelection.SelectBestChild(InitialState, Root, EXPLORATION_CONSTANT, player, SelectionStrategy.UCT, StateRateStrategy.Ejnar, true).Action;
			Console.WriteLine("Final task: " + best);

			return best;
		}

		private Node TreePolicy(Node node, POGame state)
		{
			while (state.State != State.COMPLETE)
			{
				if (FullyExpanded(node))
				{
					node = ChildSelection.SelectBestChild(state, node, EXPLORATION_CONSTANT, player, SelectionStrategy.MaxRatioChild, StateRateStrategy.Ejnar);
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
				List<PlayerTask> actions = FilterFalsyCards(state.CurrentPlayer.Options());

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

		private List<PlayerTask> FilterFalsyCards(List<PlayerTask> actions)
		{
			actions.RemoveAll(action => action.PlayerTaskType == PlayerTaskType.PLAY_CARD && action.Source.Card.Name == "No Way!");
			return actions;
		}
	}
}
