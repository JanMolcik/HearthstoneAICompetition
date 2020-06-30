using SabberStoneBasicAI.AIAgents.MyAgent;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneAICompetition.src.AIAgents.MyAgent
{
	class MCTS
	{
		private readonly double EXP_CONSTANT = 1 / Math.Sqrt(2); // magic constant for BestChild function
		private readonly int COMPUTATIONAL_BUDGET = 20000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly POGame InitialState;
		Node Root { get; set; }

		public MCTS(POGame poGame)
		{
			Root = new Node();
			InitialState = poGame;
			InitializeNode(Root, ref InitialState);
		}

		public PlayerTask UCTSearch()
		{
			// needs testing
			StopWatch.Start();
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root, ref state);
				double delta = DefaultPolicy(state, lastNode);
				Backup(lastNode, delta);
				//if (delta > 0.5) Console.WriteLine("Delta: " + delta);
			}
			StopWatch.Stop();
			PlayerTask best = BestChild(Root, EXP_CONSTANT).Action;
			Console.WriteLine("Final task: " + best);

			return best;
		}

		private Node TreePolicy(Node node, ref POGame state)
		{
			// needs testing
			while (state.State != State.COMPLETE)
			{
				if (FullyExpanded(node))
				{
					node = BestChild(node, EXP_CONSTANT);
					List<PlayerTask> action = new List<PlayerTask> { node.Action };
					state = state.Simulate(new List<PlayerTask> { node.Action })[node.Action];
					//Console.WriteLine("Choosing Best Child..");
				}
				else
				{
					return Expand(node, ref state);
				}
			}

			return node;
		}

		private Node Expand(Node node, ref POGame state)
		{
			// needs testing
			Node child;
			do
			{
				child = node.Children[Rand.Next(node.Children.Count)];
				//Console.WriteLine("Expanding..");
			}
			while (child.Children.Count > 0); // unvisited child

			POGame childState = state.getCopy();
			childState = childState.Simulate(new List<PlayerTask> { child.Action })[child.Action];

			InitializeNode(child, ref childState);

			return child;
		}

		private Node BestChild(Node node, double c)
		{
			// needs testing
			double maxUCT = 0;
			int maxIndex = 0;

			for (int i = 0; i < node.Children.Count(); i++)
			{
				Node child = node.Children[i];
				double UCT = (child.Reward / child.VisitedCount) + (c * Math.Sqrt(2 * Math.Log(node.VisitedCount) / child.VisitedCount));

				if (UCT > maxUCT)
				{
					maxUCT = UCT;
					maxIndex = i;
				}
			}

			return node.Children[maxIndex];

		}

		private double DefaultPolicy(POGame state, Node node)
		{
			// needs testing
			double result = -1;
			while (state.State != State.COMPLETE)
			{
				PlayerTask randomAction = state.CurrentPlayer.Options()[Rand.Next(state.CurrentPlayer.Options().Count)];
				state = state.Simulate(new List<PlayerTask> { randomAction })[randomAction];

				//Console.WriteLine("Default Policy Simulating..");
				if (state == null)
				{
					return 0.5;
				}

			}

			if (state.CurrentPlayer.PlayState == PlayState.CONCEDED || state.CurrentPlayer.PlayState == PlayState.LOST)
			{
				result = 0;
			}
			else if (state.CurrentPlayer.PlayState == PlayState.WON)
			{
				result = 1;
			}
			return result;
		}

		private void Backup(Node node, double delta)
		{
			// needs testing
			while (node != null)
			{
				node.VisitedCount++;
				node.Reward += delta;
				node = node.Parent;
				//Console.WriteLine("Backing Up..");
			}
		}

		private void InitializeNode(Node node, ref POGame state)
		{
			if (state != null)
			{
				state.CurrentPlayer.Options().ForEach(option =>
				{
					node.AddChild(new Node(option, node));
				});
			}
		}

		private bool FullyExpanded(Node node)
		{
			foreach(Node child in node.Children)
			{
				if (child.Children.Count == 0) return false;
			}
			return true;
		}
	}
}
