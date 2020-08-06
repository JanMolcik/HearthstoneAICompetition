using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneBasicAI.Score;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class ChildSelector
	{
		public Node SelectBestChild(POGame state, Node node, double c,
			Controller player, Func<Node, Node, double, double> selectionStrategy,
			Func<POGame, Controller, int> stateRateStrategy, bool print = false)
		{

			if (!node.Children.Any()) return node;

			List<double> maxValue = new List<double>();
			List<int> maxIndex = new List<int>();
			maxValue.Add(Double.MinValue);
			maxIndex.Add(0);

			for (int i = 0; i < node.Children.Count; i++)
			{
				Node child = node.Children[i];
				double value = selectionStrategy(node, child, c);

				if (print) Console.WriteLine(
					String.Format("Child: {0}, visited: {1}, reward: {2}, search value: {3}",
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
				return BestChildTieBreaker(state, node, maxValue, maxIndex, player, stateRateStrategy);
			}
			return node.Children[maxIndex[0]];
		}

		private Node BestChildTieBreaker(POGame state, Node node,
			List<double> maxValue, List<int> maxIndex,
			Controller player, Func<POGame, Controller, int> strategy)
		{
			var values = new Dictionary<PlayerTask, int>();

			foreach (int i in maxIndex)
			{
				values.TryAdd(node.Children[i].Action, i);
			}
			PlayerTask bestAction = BestAction(state, values.Keys.ToList(), player, strategy);

			return node.Children[values[bestAction]];
		}

		private PlayerTask BestAction(POGame state, List<PlayerTask> actions, Controller player, Func<POGame, Controller, int> strategy)
		{
			var validOpts = state.Simulate(actions).Where(x => x.Value != null);

			PlayerTask bestAction = validOpts.Any() ?
				validOpts.OrderBy(x => strategy(state, player)).Last().Key :
				actions.First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			return bestAction;
		}
	}
}
