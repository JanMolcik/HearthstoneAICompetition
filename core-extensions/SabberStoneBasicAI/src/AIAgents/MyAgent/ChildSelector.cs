using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneBasicAI.Score;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class ChildSelector
	{
		public Node SelectBestChild(POGame state, Node node, double c,
			Controller player, SelectionStrategy selectionMethod,
			StateRateStrategy stateRateMethod, bool print = false)
		{
			List<double> maxValue = new List<double>();
			List<int> maxIndex = new List<int>();
			maxValue.Add(0);
			maxIndex.Add(0);

			for (int i = 0; i < node.Children.Count; i++)
			{
				Node child = node.Children[i];
				double value = SelectionMethod(node, child, c, selectionMethod);

				if (print) Console.WriteLine(
					String.Format("Child: {0}, visited: {1}, reward: {2}, UCT value: {3}",
					child.Action, child.VisitedCount, child.Reward, value));

				if (value > maxValue[0])
				{
					maxValue.Clear();
					maxIndex.Clear();
					maxValue.Add(value);
					maxIndex.Add(i);
				}
				else if (value == maxValue[0])
				{
					maxValue.Add(value);
					maxIndex.Add(i);
				}
			}

			if (maxValue.Count > 1)
			{
				return BestChildTieBreaker(ref state, node, maxValue, maxIndex, player, stateRateMethod);
			}
			return node.Children[maxIndex[0]];
		}

		private Node BestChildTieBreaker(ref POGame state, Node node,
			List<double> maxValue, List<int> maxIndex,
			Controller player, StateRateStrategy method)
		{
			var values = new Dictionary<PlayerTask, int>();

			foreach (int i in maxIndex)
			{
				values.TryAdd(node.Children[i].Action, i);
			}
			PlayerTask bestAction = BestAction(state, values.Keys.ToList(), player, method);

			return node.Children[values[bestAction]];
		}

		private PlayerTask BestAction(POGame state, List<PlayerTask> actions, Controller player, StateRateStrategy method)
		{
			var validOpts = state.Simulate(actions).Where(x => x.Value != null);

			PlayerTask bestAction = validOpts.Any() ?
				validOpts.OrderBy(x => StateRateMethod(state, player, method)).Last().Key :
				actions.First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			return bestAction;
		}

		private double SelectionMethod(Node parent, Node child, double c, SelectionStrategy method)
		{
			switch(method)
			{
				case SelectionStrategy.UCT:
					return (child.Reward / child.VisitedCount) + (2 * c * Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount));
				case SelectionStrategy.UCB1:
					return (child.Reward / child.VisitedCount) + Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount);
				case SelectionStrategy.MaxChild:
					return child.Reward;
				case SelectionStrategy.RobustChild:
					return child.VisitedCount;
				case SelectionStrategy.MaxRobustChild:
					return child.Reward + child.VisitedCount;
				case SelectionStrategy.MaxRatioChild:
					return child.Reward / child.VisitedCount;
				case SelectionStrategy.SecureChild:
					return (child.Reward / child.VisitedCount) - (c * Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount));
				default:
					return child.Reward / child.VisitedCount;
			}
		}

		private int StateRateMethod(POGame state, Controller player, StateRateStrategy method)
		{
			switch (method)
			{
				case StateRateStrategy.Greedy:
					return MyAgent.Score(state, player.PlayerId);
				case StateRateStrategy.Aggro:
					return new AggroScore { Controller = player }.Rate();
				case StateRateStrategy.Ramp:
					return new RampScore { Controller = player }.Rate();
				case StateRateStrategy.Control:
					return new ControlScore { Controller = player }.Rate();
				case StateRateStrategy.Ejnar:
					return new EjnarScore { Controller = player }.Rate();
				default:
					return MyAgent.Score(state, player.PlayerId);
			}
		}
	}
}
