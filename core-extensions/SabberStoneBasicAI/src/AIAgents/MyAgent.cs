using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model;
using System.Reflection;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.DecksDB
{
	class MyAgent : AbstractAgent
	{
		string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();

		private readonly CustomStopwatch StopWatch = new CustomStopwatch();

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

			var optcount = validOpts.Count();


			/* ----------- Counting probabilities ------------ */
			var history = opponent.PlayHistory;


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

			/* ------------- Opponent cards simulation -------------- */
			var opponentCards = new List<IPlayable>();
			opponent.HandZone.ForEach(card => opponentCards.Add(card));

			opponentCards.ForEach(card => opponent.HandZone.Remove(card));

			//var blizzardEntity = Entity.FromCard(in opponent, Cards.FromName("Blizzard"));
			//opponent.HandZone.Add(blizzardEntity);

			var lastPlayed = game.CurrentOpponent.LastCardPlayed;
			var opponentPlayedCards = game.CurrentOpponent.PlayHistory;

			//Console.WriteLine();

			//opponentPlayedCards.ForEach(p => Console.WriteLine(p.SourceCard));

			/* ------ Simulations against possible decks' cards ----- */
			foreach (KeyValuePair<string, List<Card>> deck in DecksDict)
			{
				//Console.WriteLine(ProbabilitiesDict[deck.Key]);
				if (ProbabilitiesDict[deck.Key] > 0.3)
				{
					//Console.WriteLine("Probability higher than 30% with deck " + deck.Key);
					var playedCards = history.Select(h => h.SourceCard).ToList();
					var deckCards = deck.Value;

					//Console.WriteLine();

					var subactions = new Dictionary<PlayerTask, POGame>();

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
								game.Simulate(opponent.Options()).ToList().ForEach(item =>
								{
									//Console.WriteLine(item.Key);
									subactions.Add(item.Key, item.Value);
								});
								clearHand(opponent);
								//Console.WriteLine(opponent.HandZone.Count());

							}

						}
					});
				}
			}

			/* ------------ Choosing best option ------------------- */

			var returnValue = validOpts.Any() ?
				validOpts.Select(x => scoreAsync(x, player.PlayerId, (optcount >= 5) ? ((optcount >= 25) ? 1 : 2) : 3)).OrderBy(x => x.Result.Value).Last().Result.Key :
				player.Options().First(x => x.PlayerTaskType == PlayerTaskType.END_TURN);

			//var opponentCopy = new Controller(gameCopy.getGame(), "Hypothetic", opponent.PlayerId, opponent.Id, opponent.NativeTags);
			//opponentCopy.HandZone.Add(Entity.FromCard(in opponent, Cards.FromName("Blizzard")));
			//var opponentOptions = game.Simulate(opponentCopy.Options()).Where(x => x.Value != null);
			//opponentOptions.ToList().ForEach(option => Console.WriteLine(option.Value));

			//StopWatch.StopWithMessage(String.Format("Compute {0} options in {1} ms", optcount, StopWatch.ElapsedMilliseconds));

			return returnValue;

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

	class MyScore : Score.Score
	{
		readonly double[] scaling = new double[] {
				21.5,
				33.6,
				41.1,
				19.4,
				54,
				60.5,
				88.5,
				84.7
		};

		public override int Rate()
		{
			if (OpHeroHp < 1)
				return Int32.MaxValue;

			if (HeroHp < 1)
				return Int32.MinValue;

			double score = 0.0;

			score += scaling[0] * HeroHp;
			score -= scaling[1] * OpHeroHp;

			score += scaling[2] * BoardZone.Count;
			score -= scaling[3] * OpBoardZone.Count;

			foreach (Minion boardZoneEntry in BoardZone)
			{
				score += scaling[4] * boardZoneEntry.Health;
				score += scaling[5] * boardZoneEntry.AttackDamage;
			}

			foreach (Minion boardZoneEntry in OpBoardZone)
			{
				score -= scaling[6] * boardZoneEntry.Health;
				score -= scaling[7] * boardZoneEntry.AttackDamage;
			}

			return (int)Math.Round(score);
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}
	}

	class CustomStopwatch : Stopwatch
	{
		public void StartWithReset()
		{
			Reset();
			Start();
		}

		public void StopWithMessage(string msg)
		{
			Stop();
			Console.WriteLine(msg);
		}
	}
}
