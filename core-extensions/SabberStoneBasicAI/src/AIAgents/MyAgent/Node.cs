using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneAICompetition.src.AIAgents.MyAgent
{
	class Node
	{
		public PlayerTask Action { get; set; }
		public Node Parent { get; set; }
		public List<Node> Children { get; set; }
		public List<PlayerTask> Unvisited { get; set; }
		public int VisitedCount { get; set; }
		public double Reward { get; set; }

		public Node()
		{
			Action = null;
			Parent = null;
			Children = new List<Node>();
			Unvisited = new List<PlayerTask>();
			VisitedCount = 0;
			Reward = 0;
		}

		public Node(PlayerTask action, Node parent)
		{
			Action = action;
			Parent = parent;
			Children = new List<Node>();
			Unvisited = new List<PlayerTask>();
			VisitedCount = 0;
			Reward = 0;
		}

		public void AddChild(Node child)
		{
			Children.Add(child);
			Unvisited.Add(child.Action);
		}

		public void VisitChild(PlayerTask action)
		{
			Unvisited.Remove(action);
		}

	}
}
