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

			//OneTurn();
			//FullGame();
			//RandomGames();
			//TestPOGame();
			//TestFullGames();
			//TestTournament();
			//TestPOGameTestAgent(1);

			//ExperimentSetup();

			Console.WriteLine("Test ended!");
			//Console.ReadLine();
		}

		private static void MainLoop()
		{
			PlayableAgent player1 = new PlayableAgent();
			PlayableAgent player2 = new PlayableAgent();
			StringBuilder help = new StringBuilder();
			bool exit = false;

			help.AppendLine();
			help.Append("Use command 'player1' to setup first agent.");
			help.AppendLine();
			help.Append("Use command 'player2' to setup second agent.");
			help.AppendLine();
			help.Append("Use command 'start (count)' to start (count) number of simulations.");

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
						case "start":
							int count;
							if (parsedInput.Count < 2)
							{
								Console.WriteLine("Missing param");
								continue;
							}
							if (Int32.TryParse(parsedInput[1], out count))
							{
								string inp = ReadFromConsole("Save game logs to file? (y/n)");
								AgentDuel(count, player1, player2, inp.ToLower() == "y");
							}
							else
							{
								Console.WriteLine(parsedInput[1] + " not valid");
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

					parsedInput.RemoveAt(0);

					/*
					while (parsedInput.Any())
					{
						
					}
					
					string result = ExecuteCommand(input);
					Console.WriteLine(result);
					*/
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}
		}

		private static PlayableAgent SetupPlayer()
		{
			StringBuilder help = new StringBuilder();
			help.AppendLine("<---------- Player Setup ---------->");
			help.AppendLine("Choose player deck and agent");
			help.AppendLine("Available decks: AggroPirateWarrior, MidrangeBuffPaladin, MidrangeJadeShaman, MidrangeSecretHunter, " +
				"MiraclePirateRogue, RenoKazakusDragonPries, RenoKazakusMage, ZooDiscardWarlock");
			help.AppendLine("Available agents: RandomAgent, GreedyAgent, BeamSearchAgent, DynamicLookaheadAgent, MCTSAgent");
			help.AppendLine("Example: AggroPirateWarrior BeamSearch");
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
			help.AppendLine("Turn depth (max depth of search): positive integer (default 1 -> flat MC)");
			help.AppendLine("Time budget (max time per move): time in msec => 1000 (default 2000 -> 2 sec)");
			help.AppendLine("Selection strategy: UCT, MaxChild, RobustChild, MaxRobustChild, MaxRatioChild, SecureChild");
			help.AppendLine("State rate strategy: Aggro, Control, Ramp, Ejnar, Greedy, Fatigue");
			help.AppendLine("Example: 1 2000 UCT Greedy");
			string input = ReadFromConsole(help.ToString());

			if (String.IsNullOrWhiteSpace(input))
			{
				Console.WriteLine("Using deafult agent");
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

		private static string ExecuteCommand(string command)
		{
			return "Executed " + command;
		}

		private static string ReadFromConsole(string promptMessage = "")
		{
			Console.WriteLine(promptMessage);
			Console.Write(ReadPrompt);
			return Console.ReadLine();
		}

		public static void TestTournament()
		{
			Agent[] agents = new Agent[2];
			agents[0] = new Agent(typeof(RandomAgent), "Random Agent");
			agents[1] = new Agent(typeof(GreedyAgent), "Greedy Agent");
			//agents[2] = new Agent(typeof(DynamicLookaheadAgent), "Dynamic Lookahead Agent");
			//agents[3] = new Agent(typeof(BeamSearchAgent), "Beam Search Agent");

			CompetitionEvaluation.Deck[] decks = new CompetitionEvaluation.Deck[3];
			decks[0] = new CompetitionEvaluation.Deck(Decks.RenoKazakusMage, CardClass.MAGE, "Mage");
			decks[1] = new CompetitionEvaluation.Deck(Decks.AggroPirateWarrior, CardClass.WARRIOR, "Warrior");
			decks[2] = new CompetitionEvaluation.Deck(Decks.MidrangeJadeShaman, CardClass.SHAMAN, "Shaman");

			RoundRobinCompetition competition = new RoundRobinCompetition(agents, decks, "");
			competition.CreateTasks(100);
			competition.startEvaluation(8);

			Console.WriteLine("Total Games Played: " + competition.GetTotalGamesPlayed());
			competition.PrintAgentStats();
		}

		public static void TestPOGame()
		{
			Console.WriteLine("Setup gameConfig");

			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1HeroClass = CardClass.MAGE,
				Player2HeroClass = CardClass.MAGE,
				Player1Deck = Decks.RenoKazakusMage,
				Player2Deck = Decks.RenoKazakusMage,
				FillDecks = false,
				Shuffle = true,
				Logging = false
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new GreedyAgent();
			AbstractAgent player2 = new GreedyAgent();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");
			//gameHandler.PlayGame();
			gameHandler.PlayGames(nr_of_games: 1000, addResultToGameStats: true, debug: false);
			GameStats gameStats = gameHandler.getGameStats();

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}

		public static void RandomGames()
		{
			int total = 1;
			var watch = Stopwatch.StartNew();

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1Name = "FitzVonGerald",
				Player1HeroClass = CardClass.PALADIN,
				Player1Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				Player2Name = "RehHausZuckFuchs",
				Player2HeroClass = CardClass.PALADIN,
				Player2Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				FillDecks = false,
				Shuffle = true,
				SkipMulligan = false,
				Logging = true,
				History = true
			};

			int turns = 0;
			int[] wins = new[] { 0, 0 };
			for (int i = 0; i < total; i++)
			{
				var game = new Game(gameConfig);
				game.StartGame();

				game.Process(ChooseTask.Mulligan(game.Player1, new List<int>()));
				game.Process(ChooseTask.Mulligan(game.Player2, new List<int>()));

				game.MainReady();

				while (game.State != State.COMPLETE)
				{
					List<PlayerTask> options = game.CurrentPlayer.Options();
					PlayerTask option = options[Rnd.Next(options.Count)];
					//Console.WriteLine(option.FullPrint());
					game.Process(option);


				}
				turns += game.Turn;
				if (game.Player1.PlayState == PlayState.WON)
					wins[0]++;
				if (game.Player2.PlayState == PlayState.WON)
					wins[1]++;
				Console.WriteLine("game ended");
				// Console.Write(game.PowerHistory.ToString());
				using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"powerhistory.log"))
				{
					file.WriteLine(game.PowerHistory.Print());
				}
				using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"logger.log"))
				{
					foreach (LogEntry log in game.Logs)
					{
						file.WriteLine(log.ToString());
					}
				}
			}

			watch.Stop();

			Console.WriteLine($"{total} games with {turns} turns took {watch.ElapsedMilliseconds} ms => " +
							  $"Avg. {watch.ElapsedMilliseconds / total} per game " +
							  $"and {watch.ElapsedMilliseconds / (total * turns)} per turn!");
			Console.WriteLine($"playerA {wins[0] * 100 / total}% vs. playerB {wins[1] * 100 / total}%!");
		}

		public static void OneTurn()
		{
			var game = new Game(
				new GameConfig()
				{
					StartPlayer = 1,
					Player1Name = "FitzVonGerald",
					Player1HeroClass = CardClass.WARRIOR,
					Player1Deck = Decks.AggroPirateWarrior,
					Player2Name = "RehHausZuckFuchs",
					Player2HeroClass = CardClass.SHAMAN,
					Player2Deck = Decks.MidrangeJadeShaman,
					FillDecks = false,
					Shuffle = false,
					SkipMulligan = false
				});
			game.Player1.BaseMana = 10;
			game.StartGame();

			var aiPlayer1 = new AggroScore();
			var aiPlayer2 = new AggroScore();

			game.Process(ChooseTask.Mulligan(game.Player1, aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList())));
			game.Process(ChooseTask.Mulligan(game.Player2, aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList())));

			game.MainReady();

			while (game.CurrentPlayer == game.Player1)
			{
				Console.WriteLine($"* Calculating solutions *** Player 1 ***");

				List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);

				var solution = new List<PlayerTask>();
				solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
				Console.WriteLine($"- Player 1 - <{game.CurrentPlayer.Name}> ---------------------------");

				foreach (PlayerTask task in solution)
				{
					Console.WriteLine(task.FullPrint());
					game.Process(task);
					if (game.CurrentPlayer.Choice != null)
						break;
				}
			}

			Console.WriteLine(game.Player1.HandZone.FullPrint());
			Console.WriteLine(game.Player1.BoardZone.FullPrint());
		}

		public static void FullGame()
		{
			var game = new Game(
				new GameConfig()
				{
					StartPlayer = 1,
					Player1Name = "FitzVonGerald",
					Player1HeroClass = CardClass.WARRIOR,
					Player1Deck = Decks.AggroPirateWarrior,
					Player2Name = "RehHausZuckFuchs",
					Player2HeroClass = CardClass.WARRIOR,
					Player2Deck = Decks.AggroPirateWarrior,
					FillDecks = false,
					Shuffle = true,
					SkipMulligan = false,
					History = false
				});
			game.StartGame();

			var aiPlayer1 = new AggroScore();
			var aiPlayer2 = new AggroScore();

			List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
			List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

			Console.WriteLine($"Player1: Mulligan {String.Join(",", mulligan1)}");
			Console.WriteLine($"Player2: Mulligan {String.Join(",", mulligan2)}");

			game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
			game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

			game.MainReady();

			while (game.State != State.COMPLETE)
			{
				Console.WriteLine("");
				Console.WriteLine($"Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState} - " +
								  $"ROUND {(game.Turn + 1) / 2} - {game.CurrentPlayer.Name}");
				Console.WriteLine($"Hero[P1]: {game.Player1.Hero.Health} / Hero[P2]: {game.Player2.Hero.Health}");
				Console.WriteLine("");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
				{
					Console.WriteLine($"* Calculating solutions *** Player 1 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 1 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}

				// Random mode for Player 2
				Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
				{
					//var options = game.Options(game.CurrentPlayer);
					//var option = options[Rnd.Next(options.Count)];
					//Log.Info($"[{option.FullPrint()}]");
					//game.Process(option);
					Console.WriteLine($"* Calculating solutions *** Player 2 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}
			}
			Console.WriteLine($"Game: {game.State}, Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState}");

		}

		public static void TestFullGames()
		{

			int maxGames = 100;
			int maxDepth = 10;
			int maxWidth = 14;
			int[] player1Stats = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			int[] player2Stats = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1Name = "FitzVonGerald",
				Player1HeroClass = CardClass.PALADIN,
				Player1Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				Player2Name = "RehHausZuckFuchs",
				Player2HeroClass = CardClass.PALADIN,
				Player2Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				FillDecks = false,
				Shuffle = true,
				SkipMulligan = false,
				Logging = false,
				History = false
			};

			for (int i = 0; i < maxGames; i++)
			{
				var game = new Game(gameConfig);
				game.StartGame();

				var aiPlayer1 = new AggroScore();
				var aiPlayer2 = new AggroScore();

				List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
				List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

				game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
				game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

				game.MainReady();

				while (game.State != State.COMPLETE)
				{
					while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
					{
						List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, maxDepth, maxWidth);
						var solution = new List<PlayerTask>();
						solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
						foreach (PlayerTask task in solution)
						{
							game.Process(task);
							if (game.CurrentPlayer.Choice != null)
								break;
						}
					}
					while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
					{
						List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, maxDepth, maxWidth);
						var solution = new List<PlayerTask>();
						solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
						foreach (PlayerTask task in solution)
						{
							game.Process(task);
							if (game.CurrentPlayer.Choice != null)
								break;
						}
					}
				}

				player1Stats[(int)game.Player1.PlayState]++;
				player2Stats[(int)game.Player2.PlayState]++;

				Console.WriteLine($"{i}.Game: {game.State}, Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState}");
			}

			Console.WriteLine($"Player1: {String.Join(",", player1Stats)}");
			Console.WriteLine($"Player2: {String.Join(",", player2Stats)}");
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
					string path = "logs.txt";
					//if (!File.Exists(path))
					//{
						using (StreamWriter sw = File.CreateText(path))
						{
							gameStats.GameInfoLogs.ForEach(log => log.ForEach(logEntry => sw.WriteLine(logEntry)));
						}
					//}
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
			/* DONE 20
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

			/* DONE 40
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
			/* DONE 40
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
			/* DONE 40
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
			/* DONE 20 
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
		}

		public static void TestPOGameTestAgent(int count)
		{
			Console.WriteLine("Setup gameConfig");
			Random rnd = new Random();

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1HeroClass = CardClass.PALADIN,
				Player2HeroClass = CardClass.PALADIN,
				Player1Deck = Decks.MidrangeBuffPaladin,
				Player2Deck = Decks.MidrangeBuffPaladin,
				FillDecks = false,
				Shuffle = true,
				Logging = true
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new TestAgent();
			AbstractAgent player2 = new DynamicLookaheadAgent();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");
			gameHandler.PlayGames(nr_of_games: count, addResultToGameStats: true, debug: false);

			GameStats gameStats = gameHandler.getGameStats();

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}

		public static void TestRandomVsMy(int count)
		{
			Console.WriteLine("Setup gameConfig");
			Random rnd = new Random();

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1HeroClass = CardClass.PRIEST,
				Player2HeroClass = CardClass.PRIEST,
				Player1Deck = Decks.RenoKazakusDragonPriest,
				Player2Deck = Decks.RenoKazakusDragonPriest,
				FillDecks = false,
				Shuffle = true,
				Logging = false
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new MCTSAgent();
			AbstractAgent player2 = new RandomAgent();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");

			gameHandler.PlayGames(nr_of_games: count, addResultToGameStats: true, debug: false);
			GameStats gameStats = gameHandler.getGameStats();

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}

		public static void TestGreedyVsMy(int count)
		{
			Console.WriteLine("Setup gameConfig");
			Random rnd = new Random();

			var gameConfig = new GameConfig()
			{
				StartPlayer = rnd.Next(1, 2),
				Player1HeroClass = CardClass.PRIEST,
				Player2HeroClass = CardClass.PRIEST,
				Player1Deck = Decks.RenoKazakusDragonPriest,
				Player2Deck = Decks.RenoKazakusDragonPriest,
				FillDecks = false,
				Shuffle = true,
				Logging = false
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new MCTSAgent();
			AbstractAgent player2 = new RandomAgent();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");

			gameHandler.PlayGames(nr_of_games: count, addResultToGameStats: true, debug: false);
			GameStats gameStats = gameHandler.getGameStats();

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}
	}
}
