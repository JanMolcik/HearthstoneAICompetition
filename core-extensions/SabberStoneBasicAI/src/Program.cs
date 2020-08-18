#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.Meta;
using SabberStoneBasicAI.Nodes;
using SabberStoneBasicAI.Score;
using SabberStoneBasicAI.AIAgents;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneBasicAI.CompetitionEvaluation;
using SabberStoneBasicAI.AIAgents.MyAgents;
using System.Text;
using System.Reflection;
using System.IO;

namespace SabberStoneBasicAI
{
	internal class Program
	{
		private static readonly Random Rnd = new Random();
		private static readonly string ReadPrompt = "> ";

		private static void Main()
		{
			MainLoop();
			
			Console.WriteLine("Bye!");
		}

		private static void MainLoop()
		{
			PlayableAgent player1 = new PlayableAgent();
			PlayableAgent player2 = new PlayableAgent();
			StringBuilder help = new StringBuilder();
			bool exit = false;

			help.AppendLine("Use command 'player1' to setup first agent.");
			help.AppendLine("Use command 'player2' to setup second agent.");
			help.AppendLine("Use command 'play (count)' to start (count) number of simulations.");
			help.Append("Use command 'exit' to quit.");

			Console.WriteLine("Duels of Agents");
			while (!exit)
			{
				string input = ReadFromConsole(help.ToString());
				if (String.IsNullOrWhiteSpace(input)) continue;

				try
				{
					List<string> parsedInput = input.Split(' ').ToList();

					switch (parsedInput[0])
					{
						case "play":
							int count;
							if (parsedInput.Count < 2)
							{
								Console.WriteLine("Missing param");
								continue;
							}
							if (Int32.TryParse(parsedInput[1], out count))
							{
								string inp = ReadFromConsole("Save game log to file? (y/n)");
								if (!String.IsNullOrWhiteSpace(inp) && inp.ToLower() == "y")
								{
									string path = "log.txt";
									if (File.Exists(path))
									{
										string inpt = ReadFromConsole("Will overwrite old log file. Do you agree? (y/n)");
										AgentDuel(count, player1, player2, !String.IsNullOrWhiteSpace(inpt) && inpt.ToLower() == "y");
									} else
									{
										AgentDuel(count, player1, player2, true);
									}
								}
							}
							else
							{
								Console.WriteLine(parsedInput[1] + " not valid.");
							}
							break;
						case "player1":
							player1 = SetupPlayer();
							break;
						case "player2":
							player2 = SetupPlayer();
							break;
						case "exit":
							exit = true;
							break;
						default:
							break;
					}
				}
				catch (Exception e)
				{
					//Console.WriteLine(e);
				}
			}
		}

		private static PlayableAgent SetupPlayer()
		{
			StringBuilder help = new StringBuilder();
			help.AppendLine("<---------- Player Setup ---------->");
			help.AppendLine("Choose player deck and agent");
			help.AppendLine("Available decks: AggroPirateWarrior, MidrangeBuffPaladin, MidrangeJadeShaman, MidrangeSecretHunter, " +
				"MiraclePirateRogue, RenoKazakusDragonPriest, RenoKazakusMage");
			help.AppendLine("Available agents: RandomAgent, GreedyAgent, BeamSearchAgent, DynamicLookaheadAgent, MCTSAgent");
			help.Append("Example: AggroPirateWarrior BeamSearchAgent");
			string input = ReadFromConsole(help.ToString());

			PlayableAgent agent = new PlayableAgent();

			if (String.IsNullOrWhiteSpace(input)) return agent;

			List<string> parsedInput = input.Split(' ').ToList();
			if (parsedInput.Count >= 2)
			{
				agent.SetDeck(parsedInput[0]);

				var assembly = Assembly.GetExecutingAssembly();

				try
				{
					var type = assembly.GetTypes()
						.First(t => t.Name == parsedInput[1]);

					if (parsedInput[1] == "MCTSAgent")
					{
						agent.Agent = GetParamsForAgent();
					}
					else
					{
						AbstractAgent agentInstance = (AbstractAgent)Activator.CreateInstance(type);
						agent.Agent = agentInstance;
					}

				}
				catch (Exception e)
				{
					Console.WriteLine("Invalid agent name. Using default agent");
				}
			}
			else
			{
				Console.WriteLine("Not enough parameters. Using default deck and agent.");
			}

			return agent;
		}

		private static MCTSAgent GetParamsForAgent()
		{
			StringBuilder help = new StringBuilder();
			MCTSAgent agent = new MCTSAgent();

			help.AppendLine("<---------- MCTSAgent advanced parameters ---------->");
			help.AppendLine("Turn depth (max depth of search): positive integer (default 1 -> flat MCTS)");
			help.AppendLine("Time budget (max time per move): time in msec >= 1000 (default 2000 -> 2 sec)");
			help.AppendLine("Selection strategy: UCT, MaxChild, RobustChild, MaxRobustChild, MaxRatioChild, SecureChild");
			help.AppendLine("State rate strategy: Aggro, Control, Ramp, Ejnar, Greedy, Fatigue");
			help.Append("Example: 1 2000 UCT Greedy");
			string input = ReadFromConsole(help.ToString());

			if (String.IsNullOrWhiteSpace(input))
			{
				Console.WriteLine("Using deafult agent.");
				return agent;
			}

			string[] parsedInput = input.Split(' ');

			if (parsedInput.Count() == 4)
			{
				try
				{
					int turnDepth;
					int timeBudget;
					SelectionStrategy selection;
					StateRateStrategy stateRate;

					if (!Int32.TryParse(parsedInput[0], out turnDepth) || turnDepth < 1)
					{
						Console.WriteLine("Not valid turn depth. Using default.");
						turnDepth = 1;
					}

					if (!Int32.TryParse(parsedInput[1], out timeBudget) || timeBudget < 1000)
					{
						Console.WriteLine("Time budget less then 1000 msec. Using default.");
						timeBudget = 2000;
					}

					if (Enum.GetNames(typeof(SelectionStrategy)).Contains(parsedInput[2]))
					{
						selection = (SelectionStrategy)Enum.Parse(typeof(SelectionStrategy), parsedInput[2]);
					}
					else
					{
						Console.WriteLine("Not valid Selection strategy. Using deafult.");
						selection = SelectionStrategy.UCT;
					}

					if (Enum.GetNames(typeof(StateRateStrategy)).Contains(parsedInput[3]))
					{
						stateRate = (StateRateStrategy)Enum.Parse(typeof(StateRateStrategy), parsedInput[3]);
					}
					else
					{
						Console.WriteLine("Not valid State rate strategy. Using deafult.");
						stateRate = StateRateStrategy.Greedy;
					}

					agent.TurnDepth = turnDepth;
					agent.TimeBudget = timeBudget;
					agent.Selection = selection;
					agent.StateRate = stateRate;

				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}


			return agent;
		}

		private static string ReadFromConsole(string promptMessage = "")
		{
			Console.WriteLine(promptMessage);
			Console.Write(ReadPrompt);
			return Console.ReadLine();
		}
		public static void TestPOGameMyAgent(int count, CardClass player, CardClass opponent, List<Card> playerDeck, List<Card> opponentDeck, AbstractAgent opponentAgent)
		{
			Console.WriteLine("Setup gameConfig");

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1HeroClass = player,
				Player2HeroClass = opponent,
				Player1Deck = playerDeck,
				Player2Deck = opponentDeck,
				FillDecks = false,
				Shuffle = true,
				Logging = true
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new MCTSAgent();
			AbstractAgent player2 = opponentAgent;
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");
			gameHandler.PlayGames(nr_of_games: count, addResultToGameStats: true, debug: false);

			GameStats gameStats = gameHandler.getGameStats();

			Console.WriteLine(player + " vs " + opponent);
			gameStats.printResults();
			Console.WriteLine("Test successful");
		}

		public static void AgentDuel(int count, PlayableAgent player, PlayableAgent opponent, bool saveLogs = false)
		{
			Console.WriteLine("Setup gameConfig");

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1HeroClass = player.AgentClass,
				Player2HeroClass = opponent.AgentClass,
				Player1Deck = player.Deck,
				Player2Deck = opponent.Deck,
				FillDecks = false,
				Shuffle = true,
				Logging = true
			};

			//Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = player.Agent;
			AbstractAgent player2 = opponent.Agent;
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);
			Console.WriteLine("Simulate Games");
			gameHandler.PlayGames(nr_of_games: count, addResultToGameStats: true, debug: false);

			GameStats gameStats = gameHandler.getGameStats();

			if (saveLogs)
			{
				try
				{
					string path = "log.txt";
					using (StreamWriter sw = File.CreateText(path))
					{
						gameStats.GameInfoLogs.ForEach(log => log.ForEach(logEntry => sw.WriteLine(logEntry)));
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}

			Console.WriteLine(player.AgentClass + " vs " + opponent.AgentClass);
			gameStats.printResults();
			Console.WriteLine("Duel successful");
		}

		public static void ExperimentSetup()
		{
			/* DONE 100
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.WARRIOR, Decks.AggroPirateWarrior, Decks.AggroPirateWarrior, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.PALADIN, Decks.AggroPirateWarrior, Decks.MidrangeBuffPaladin, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.MAGE, Decks.AggroPirateWarrior, Decks.RenoKazakusMage, new RandomAgent());
			
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.WARRIOR, Decks.MidrangeBuffPaladin, Decks.AggroPirateWarrior, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.PALADIN, Decks.MidrangeBuffPaladin, Decks.MidrangeBuffPaladin, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.MAGE, Decks.MidrangeBuffPaladin, Decks.RenoKazakusMage, new RandomAgent());

			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.WARRIOR, Decks.RenoKazakusMage, Decks.AggroPirateWarrior, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.PALADIN, Decks.RenoKazakusMage, Decks.MidrangeBuffPaladin, new RandomAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.MAGE, Decks.RenoKazakusMage, Decks.RenoKazakusMage, new RandomAgent());
			*/

			/* DONE 100
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.WARRIOR, Decks.AggroPirateWarrior, Decks.AggroPirateWarrior, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.PALADIN, Decks.AggroPirateWarrior, Decks.MidrangeBuffPaladin, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.MAGE, Decks.AggroPirateWarrior, Decks.RenoKazakusMage, new GreedyAgent());
			
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.WARRIOR, Decks.MidrangeBuffPaladin, Decks.AggroPirateWarrior, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.PALADIN, Decks.MidrangeBuffPaladin, Decks.MidrangeBuffPaladin, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.MAGE, Decks.MidrangeBuffPaladin, Decks.RenoKazakusMage, new GreedyAgent());
			
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.WARRIOR, Decks.RenoKazakusMage, Decks.AggroPirateWarrior, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.PALADIN, Decks.RenoKazakusMage, Decks.MidrangeBuffPaladin, new GreedyAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.MAGE, Decks.RenoKazakusMage, Decks.RenoKazakusMage, new GreedyAgent());
			*/
			/* DONE 100
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.WARRIOR, Decks.AggroPirateWarrior, Decks.AggroPirateWarrior, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.PALADIN, Decks.AggroPirateWarrior, Decks.MidrangeBuffPaladin, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.MAGE, Decks.AggroPirateWarrior, Decks.RenoKazakusMage, new BeamSearchAgent());
			
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.WARRIOR, Decks.MidrangeBuffPaladin, Decks.AggroPirateWarrior, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.PALADIN, Decks.MidrangeBuffPaladin, Decks.MidrangeBuffPaladin, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.MAGE, Decks.MidrangeBuffPaladin, Decks.RenoKazakusMage, new BeamSearchAgent());
			
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.WARRIOR, Decks.RenoKazakusMage, Decks.AggroPirateWarrior, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.PALADIN, Decks.RenoKazakusMage, Decks.MidrangeBuffPaladin, new BeamSearchAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.MAGE, Decks.RenoKazakusMage, Decks.RenoKazakusMage, new BeamSearchAgent());
			*/
			/* DONE 100
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.WARRIOR, Decks.AggroPirateWarrior, Decks.AggroPirateWarrior, new DynamicLookaheadAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.PALADIN, Decks.AggroPirateWarrior, Decks.MidrangeBuffPaladin, new DynamicLookaheadAgent());
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.MAGE, Decks.AggroPirateWarrior, Decks.RenoKazakusMage, new DynamicLookaheadAgent());
			
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.WARRIOR, Decks.MidrangeBuffPaladin, Decks.AggroPirateWarrior, new DynamicLookaheadAgent());
			
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.PALADIN, Decks.MidrangeBuffPaladin, Decks.MidrangeBuffPaladin, new DynamicLookaheadAgent());
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.MAGE, Decks.MidrangeBuffPaladin, Decks.RenoKazakusMage, new DynamicLookaheadAgent());

			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.WARRIOR, Decks.RenoKazakusMage, Decks.AggroPirateWarrior, new DynamicLookaheadAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.PALADIN, Decks.RenoKazakusMage, Decks.MidrangeBuffPaladin, new DynamicLookaheadAgent());
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.MAGE, Decks.RenoKazakusMage, Decks.RenoKazakusMage, new DynamicLookaheadAgent());
			*/
			/* DONE 100
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.WARRIOR, Decks.AggroPirateWarrior, Decks.AggroPirateWarrior, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.PALADIN, Decks.AggroPirateWarrior, Decks.MidrangeBuffPaladin, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.WARRIOR, CardClass.MAGE, Decks.AggroPirateWarrior, Decks.RenoKazakusMage, new MyAgent() { TurnDepth = 2 });
			*/
			/*
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.WARRIOR, Decks.MidrangeBuffPaladin, Decks.AggroPirateWarrior, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.PALADIN, Decks.MidrangeBuffPaladin, Decks.MidrangeBuffPaladin, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.PALADIN, CardClass.MAGE, Decks.MidrangeBuffPaladin, Decks.RenoKazakusMage, new MyAgent() { TurnDepth = 2 });
			*/
			/*
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.WARRIOR, Decks.RenoKazakusMage, Decks.AggroPirateWarrior, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.PALADIN, Decks.RenoKazakusMage, Decks.MidrangeBuffPaladin, new MyAgent() { TurnDepth = 2 });
			TestPOGameMyAgent(20, CardClass.MAGE, CardClass.MAGE, Decks.RenoKazakusMage, Decks.RenoKazakusMage, new MyAgent() { TurnDepth = 2 });
			*/
			/*
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 1 } },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 10 } },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 100 } },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });
				*/
				/*
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 1 }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });

			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 10 }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 100 }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });

			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 1 }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 10 }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { ExplorationConstant = 100 }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });
				*/
			/*
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { TimeBudget = 4000 }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
				new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { TimeBudget = 4000, TurnDepth = 2 }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				 new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxChild }, AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.RobustChild }, AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxRobustChild }, AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.SecureChild }, AgentClass = CardClass.WARRIOR, Deck = Decks.AggroPirateWarrior });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass= CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
			new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.RobustChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
			new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxRobustChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.UCT, TimeBudget = 4000 }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxChild }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.RobustChild }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxRobustChild }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage },
				new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.SecureChild }, AgentClass = CardClass.MAGE, Deck = Decks.RenoKazakusMage });*/
			/*AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.UCT, TimeBudget = 4000 }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
			new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxRobustChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });
			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
			new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.MaxRobustChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });

			AgentDuel(50, new PlayableAgent() { Agent = new MCTSAgent(), AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin },
			new PlayableAgent() { Agent = new MCTSAgent() { Selection = SelectionStrategy.SecureChild }, AgentClass = CardClass.PALADIN, Deck = Decks.MidrangeBuffPaladin });*/
			Console.ReadLine();
		}

	}
}
