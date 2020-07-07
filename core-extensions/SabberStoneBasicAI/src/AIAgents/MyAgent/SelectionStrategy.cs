using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	public enum SelectionStrategy
	{
		MaxChild,
		RobustChild,
		MaxRobustChild,
		SecureChild,
		MaxRatioChild,
		UCT,
		UCB1
	}
}
