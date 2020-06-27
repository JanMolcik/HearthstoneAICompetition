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
		private readonly int COMPUTATIONAL_BUDGET = 500; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private readonly POGame InitialState;
		Node Root { get; set; }

		public MCTS(POGame poGame)
		{
			Root = new Node();
			InitialState = poGame;
			InitializeRoot();

		}

		public PlayerTask UCTSearch()
		{
			// needs testing
			StopWatch.Start();
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root);
				double delta = DefaultPolicy(state, lastNode);

			}
			StopWatch.Stop();

			return BestChild(Root, EXP_CONSTANT).Action;
		}

		private Node TreePolicy(Node node)
		{
			// work in progress
			return new Node();
		}

		private Node Expand(Node node)
		{
			// work in progress
			return new Node();
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
				PlayerTask randomAction = state.CurrentPlayer.Options()[Rand.Next(state.CurrentPlayer.Options().Count())];
				state = state.Simulate(new List<PlayerTask> { randomAction })[randomAction];
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
			}
		}

		private void InitializeRoot()
		{
			InitialState.CurrentPlayer.Options().ForEach(option =>
			{
				Root.Children.Add(new Node(option, Root));
			});
		}
	}
}
