using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class Node
	{
		public int PlayerID { get; set; }
		public POGame State { get; set; }
		public PlayerTask Action { get; set; }
		public Node Parent { get; set; }
		public List<Node> Children { get; set; }
		// TurnDepth determines how deep this node is turn-based, not tree-based
		public int TurnDepth { get; set; }
		public int VisitedCount { get; set; }
		public double Reward { get; set; }

		public Node(POGame state, int playerId)
		{
			PlayerID = playerId;
			State = state;
			Action = null;
			Parent = null;
			Children = new List<Node>();
			TurnDepth = 0;
			VisitedCount = 0;
			Reward = 0;
		}

		public Node(POGame state, PlayerTask action, Node parent, int playerId, int turnDepth)
		{
			PlayerID = playerId;
			State = state;
			Action = action;
			Parent = parent;
			Children = new List<Node>();
			TurnDepth = turnDepth;
			VisitedCount = 0;
			Reward = 0;
		}

	}
}
