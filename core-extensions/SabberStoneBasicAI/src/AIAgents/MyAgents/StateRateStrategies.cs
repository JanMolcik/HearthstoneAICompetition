using SabberStoneBasicAI.PartialObservation;
using SabberStoneBasicAI.Score;
using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class StateRateStrategies
	{
		public static readonly Dictionary<StateRateStrategy, Func<POGame, Controller, int>> StateRateStrategiesDict =
				new Dictionary<StateRateStrategy, Func<POGame, Controller, int>>
			{
				{ StateRateStrategy.Greedy, (POGame state, Controller player) => new MyScore { Controller = player }.Rate() },

				{ StateRateStrategy.Ejnar, (POGame state, Controller player) => new EjnarScore { Controller = player }.Rate() },

				{ StateRateStrategy.Aggro, (POGame state, Controller player) => new AggroScore { Controller = player }.Rate() },

				{ StateRateStrategy.Ramp, (POGame state, Controller player) => new RampScore { Controller = player }.Rate() },

				{ StateRateStrategy.Control, (POGame state, Controller player) => new ControlScore { Controller = player }.Rate() }
			};

		public static Func<POGame, Controller, int> GetStateRateStrategy(StateRateStrategy strategy)
		{
			return StateRateStrategiesDict.GetValueOrDefault(strategy, StateRateStrategiesDict[StateRateStrategy.Greedy]);
		}
	}
}
