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
		public int VisitedCount { get; set; }
		public double Reward { get; set; }

		public Node()
		{
			Action = null;
			Parent = null;
			Children = new List<Node>();
			VisitedCount = 0;
			Reward = 0;
		}

		public Node(PlayerTask action, Node parent)
		{
			Action = action;
			Parent = parent;
			Children = new List<Node>();
			VisitedCount = 0;
			Reward = 0;
		}

	}
}
