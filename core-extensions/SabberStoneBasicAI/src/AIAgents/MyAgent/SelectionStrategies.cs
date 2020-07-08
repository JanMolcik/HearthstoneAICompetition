﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class SelectionStrategies
	{
		private Dictionary<SelectionStrategy, Func<Node, Node, double, double>> SelectionStrategiesDict;

		public SelectionStrategies()
		{
			SelectionStrategiesDict = new Dictionary<SelectionStrategy, Func<Node, Node, double, double>>
			{
				{
					SelectionStrategy.UCT,
					(Node parent, Node child, double c) =>
					(child.Reward / child.VisitedCount) + (2 * c * Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount))
				},

				{
					SelectionStrategy.UCB1,
					(Node parent, Node child, double c) =>
					(child.Reward / child.VisitedCount) + Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount)
				},

				{
					SelectionStrategy.MaxChild,
					(Node parent, Node child, double c) => child.Reward
				},

				{
					SelectionStrategy.RobustChild,
					(Node parent, Node child, double c) => child.VisitedCount
				},

				{
					SelectionStrategy.MaxRobustChild,
					(Node parent, Node child, double c) => child.VisitedCount + child.Reward
				},

				{
					SelectionStrategy.MaxRatioChild,
					(Node parent, Node child, double c) => child.Reward / child.VisitedCount
				},

				{
					SelectionStrategy.SecureChild,
					(Node parent, Node child, double c) =>
					(child.Reward / child.VisitedCount) - (c * Math.Sqrt(2 * Math.Log(parent.VisitedCount) / child.VisitedCount))
				}
			};
		}

		public Func<Node, Node, double, double> GetSelectionStrategy(SelectionStrategy strategy)
		{
			return SelectionStrategiesDict.GetValueOrDefault(strategy, SelectionStrategiesDict[SelectionStrategy.MaxRatioChild]);
		}

	}
}
