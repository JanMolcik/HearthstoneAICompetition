using SabberStoneBasicAI.AIAgents;
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
	class MCTS
	{
		private readonly double EXP_CONSTANT = 1 / Math.Sqrt(2); // magic constant for BestChild function
		private readonly int COMPUTATIONAL_BUDGET = 10000; // in ms
		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private readonly Random Rand = new Random();
		private POGame InitialState;
		private readonly Controller player;
		Node Root { get; set; }

		public MCTS(POGame poGame)
		{
			Root = new Node();
			InitialState = poGame;
			InitializeNode(Root, ref InitialState);
			player = poGame.CurrentPlayer;

			//poGame.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
			Console.WriteLine();
		}

		public PlayerTask UCTSearch()
		{
			StopWatch.Start();
			while (StopWatch.ElapsedMilliseconds < COMPUTATIONAL_BUDGET)
			{
				POGame state = InitialState.getCopy();
				Node lastNode = TreePolicy(Root, ref state);
				double delta = DefaultPolicy(state, lastNode);
				Backup(lastNode, delta);
			}
			StopWatch.Stop();
			PlayerTask best = FinalBestChild(ref InitialState, Root).Action;
			Console.WriteLine("Final task: " + best);

			return best;
		}

		private Node TreePolicy(Node node, ref POGame state)
		{
			while (state.State != State.COMPLETE)
			{
				if (FullyExpanded(node))
				{
					node = BestChild(ref state, node, EXP_CONSTANT);
					state = state.Simulate(new List<PlayerTask> { node.Action })[node.Action];
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
			Node child;

			do
			{
				child = node.Children[Rand.Next(node.Children.Count)];
			}
			while (child.Children.Count > 0); // unvisited child

			POGame childState = state.getCopy();
			childState = childState.Simulate(new List<PlayerTask> { child.Action })[child.Action];

			InitializeNode(child, ref childState);

			return child;
		}

		private Node BestChild(ref POGame state, Node node, double c, bool print = false)
		{
			// refactor to generic function with different evaluation methods (UCB1, UCT, ...)
			List<double> maxUCT = new List<double>();
			List<int> maxIndex = new List<int>();
			maxUCT.Add(0);
			maxIndex.Add(0);

			for (int i = 0; i < node.Children.Count(); i++)
			{
				Node child = node.Children[i];
				double UCT = (child.Reward / child.VisitedCount) +
					(c * Math.Sqrt(2 * Math.Log(node.VisitedCount) / child.VisitedCount));

				if (print) Console.WriteLine(
					String.Format("Child: {0}, visited: {1}, reward: {2}, UCT value: {3}",
					child.Action, child.VisitedCount, child.Reward, UCT));

				if (UCT > maxUCT[0])
				{
					maxUCT.Clear();
					maxIndex.Clear();
					maxUCT.Add(UCT);
					maxIndex.Add(i);
				}
				else if (UCT == maxUCT[0])
				{
					maxUCT.Add(UCT);
					maxIndex.Add(i);
				}
			}
			
			if (maxUCT.Count > 1)
			{
				return BestChildTieBreaker(ref state, node, maxUCT, maxIndex);
			}
			return node.Children[maxIndex[0]];

		}

		private double DefaultPolicy(POGame state, Node node)
		{
			double result = -1;
			while (state.State != State.COMPLETE)
			{
				// instead of removing unknown cards I should rather try guess what is
				// in opponent's hand and simulate most probable and best actions
				List<PlayerTask> actions = FilterFalsyCards(state.CurrentPlayer.Options());

				// instead of random action I could be looking for best actions but..

				PlayerTask randomAction = actions[Rand.Next(actions.Count())];
				PlayerTask bestAction = BestAction(state, actions);
				state = state.Simulate(new List<PlayerTask> { randomAction })[randomAction];
				
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
			while (node != null)
			{
				node.VisitedCount++;
				node.Reward += delta;
				node = node.Parent;
			}
		}

		private void InitializeNode(Node node, ref POGame state)
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

		private PlayerTask BestAction(POGame state, List<PlayerTask> actions)
		{
			var validOpts = state.Simulate(actions).Where(x => x.Value != null);

			PlayerTask bestAction = validOpts.Any() ?
				validOpts.OrderBy(x => MyAgent.Score(x.Value, player.PlayerId)).Last().Key :
				actions.First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			return bestAction;
		}

		private Node FinalBestChild(ref POGame state, Node node)
		{
			// needs testing
			List<double> maxValue = new List<double>();
			List<int> maxIndex = new List<int>();
			maxValue.Add(0);
			maxIndex.Add(0);

			for (int i = 0; i < node.Children.Count(); i++)
			{
				Node child = node.Children[i];
				double value = child.Reward / child.VisitedCount;

				Console.WriteLine(
					String.Format("Child: {0}, visited: {1}, reward: {2}, value: {3}",
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

			//if (print) Console.WriteLine("BestChild UCT value: " + maxUCT);
			if (maxValue.Count > 1)
			{
				return BestChildTieBreaker(ref state, node, maxValue, maxIndex);
			}
			return node.Children[maxIndex[0]];
		}

		private Node BestChildTieBreaker(ref POGame state, Node node, List<double> maxValue, List<int> maxIndex)
		{
			var bestUCTs = new Dictionary<PlayerTask, int>();

			foreach (int i in maxIndex)
			{
				bestUCTs.TryAdd(node.Children[i].Action, i);
			}
			var bestAction = BestAction(state, bestUCTs.Keys.ToList());

			return node.Children[bestUCTs[bestAction]];
		}
	}
}
