using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using System.Linq;
using System.Threading.Tasks;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model;
using SabberStoneAICompetition.src.AIAgents.MyAgent;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.MyAgent
{
	class MyAgent : AbstractAgent
	{
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();

		private readonly CustomStopwatch StopWatch = new CustomStopwatch();
		private bool FirstTime = true;

		public override void InitializeAgent()
		{
			Type deckType = typeof(Decks);

			/* -------------- Constructing possible decks ------------------- */

			foreach (string deckName in DeckNames)
			{
				var cards = (IEnumerable<Card>)deckType
					.GetProperty(deckName)
					.GetGetMethod()
					.Invoke(null, null);

				//cards.ToList().ForEach(c => Console.WriteLine(c));
				DecksDict.Add(deckName, cards.ToList());
				ProbabilitiesDict.Add(deckName, 0);
			}

			//DecksDict.Values.First().ForEach(c => Console.WriteLine(c));
		}

		public override void FinalizeAgent()
		{
		}

		public override void InitializeGame()
		{
		}

		public override void FinalizeGame()
		{
		}

		public override PlayerTask GetMove(POGame game)
		{
			//StopWatch.StartWithReset();

			int threadCount = 4;
			var player = game.CurrentPlayer;
			var opponent = game.CurrentOpponent;

			var validOpts = game.Simulate(player.Options()).Where(x => x.Value != null);
			var optsCount = validOpts.Count();
			var history = opponent.PlayHistory;

			countProbabilities();
			//predictOpponentTask();

			/* ------------- SANDBOX TESTING -------------- */
			var opponentCards = new List<IPlayable>();
			opponent.HandZone.ForEach(card => opponentCards.Add(card));

			opponentCards.ForEach(card => opponent.HandZone.Remove(card));

			//var blizzardEntity = Entity.FromCard(in opponent, Cards.FromName("Blizzard"));
			//opponent.HandZone.Add(blizzardEntity);

			var lastPlayed = game.CurrentOpponent.LastCardPlayed;
			var opponentPlayedCards = game.CurrentOpponent.PlayHistory;

			//opponentPlayedCards.ForEach(p => Console.WriteLine(p.SourceCard));

			/* ------------ Choosing best option ------------------- */

			/*var returnValue = validOpts.Any() ?
				validOpts.Select(x => scoreAsync(x, player.PlayerId, (optsCount >= 5) ? ((optsCount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Result.Value).Last().Result.Key :
				player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);*/

			MCTS mcts = new MCTS(game);
			PlayerTask result = mcts.UCTSearch();
			//StopWatch.StopWithMessage(String.Format("Compute {0} options in {1} ms", optcount, StopWatch.ElapsedMilliseconds));

			return result;

			void countProbabilities()
			{
				/* ----------- Counting probabilities ------------ */
				foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
				{
					int similarCount = 0;
					var playedCards = history.Select(h => h.SourceCard).ToList();
					var deckCards = deck.Value;
					var deckCardsDistinct = deckCards.Distinct().ToList();

					playedCards
						.ForEach(playedCard =>
						{
							deckCardsDistinct.ForEach(deckCard =>
							{
								if (playedCard.Name == deckCard.Name)
								{
									similarCount++;
								}
							});
						});

					double probability = Math.Round((double)similarCount / deckCards.Count(), 2);
					ProbabilitiesDict[deck.Key] = probability;
					//if (probability > 0) Console.WriteLine(deck.Key + " has probability of " + ProbabilitiesDict[deck.Key] * 100 + "%");
				}

			}

			void predictOpponentTask()
			{
				/* ------ Simulations against possible decks' cards ----- */
				/* Best to be applied only for few best tasks
				 * otherwise it would take a lot of compute time
				 * trying to predict opponent's task for all
				 * possible player's tasks
				 */
				foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
				{
					//Console.WriteLine(ProbabilitiesDict[deck.Key]);
					if (ProbabilitiesDict[deck.Key] > 0.3)
					{
						//Console.WriteLine("Probability higher than 30% with deck " + deck.Key);
						var playedCards = history.Select(h => h.SourceCard).ToList();
						var deckCards = deck.Value;

						var subactions = new Dictionary<PlayerTask, POGame>();

						//Console.WriteLine(opponent);

						// removing played cards
						playedCards.ForEach(playedCard =>
						{
							deckCards.Remove(playedCard);
						});

						// adding cards to hand for simualtion
						deckCards.ForEach(deckCard =>
						{
							if (deckCard.Cost <= opponent.BaseMana)
							{
								if (!opponent.HandZone.IsFull)
								{
									opponent.HandZone.Add(Entity.FromCard(in opponent, Cards.FromId(deckCard.Id)));
									//Console.WriteLine("Add " + deckCard.Name);
								}
								else
								{
									//Console.WriteLine("Before clear");
									//printPlayerHand(opponent);
									player.Options().ForEach(option => Console.Write(option + " "));
									// for opponent simulation it is required the state of POGame with his turn 
									/*game*/
									validOpts.First().Value.Simulate(opponent.Options()).ToList().ForEach(item =>
									{
										Console.WriteLine(item.Key);
										subactions.Add(item.Key, item.Value);
									});
									clearHand(opponent);
									//Console.WriteLine(opponent.HandZone.Count());

								}

							}
						});

						//Console.WriteLine(subactions.ToString());
					}
				}
			}

			async Task<KeyValuePair<PlayerTask, int>> scoreAsync(KeyValuePair<PlayerTask, POGame> state, int player_id, int max_depth = 3)
			{
				int max_score = Int32.MinValue;
				if (max_depth > 0 && state.Value.CurrentPlayer.PlayerId == player_id)
				{
					var subactions = state.Value.Simulate(state.Value.CurrentPlayer.Options()).Where(x => x.Value != null);

					foreach (var subaction in subactions)
						if (threadCount > 0)
						{
							threadCount--;
							max_score = Math.Max(max_score, await Task.Run(() => scoreAsync(subaction, player_id, max_depth - 1).Result.Value));
						}
						else
						{
							max_score = Math.Max(max_score, scoreAsync(subaction, player_id, max_depth - 1).Result.Value);
						}

				}
				else if (FirstTime && state.Value.CurrentPlayer.PlayerId != player_id)
				{
					state.Value.CurrentPlayer.Options().ForEach(task => Console.Write(task + " "));
					FirstTime = false;
				}
				max_score = Math.Max(max_score, Score(state.Value, player_id));
				return new KeyValuePair<PlayerTask, int>(state.Key, max_score);
			}

			void printPlayerHand(Controller pplayer)
			{
				pplayer.HandZone.ForEach(card => Console.WriteLine(card));
			}

			void clearHand(Controller pplayer)
			{
				while (pplayer.HandZone.Count() > 0)
				{
					pplayer.HandZone.Remove(0);
				}
				//Console.WriteLine("Hand cleared: " + pplayer.HandZone.Count());
				printPlayerHand(pplayer);
			}

		}


		private static int Score(POGame state, int playerId)
		{
			var p = state.CurrentPlayer.PlayerId == playerId ? state.CurrentPlayer : state.CurrentOpponent;

			return new MyScore { Controller = p }.Rate();
		}
	}
}
