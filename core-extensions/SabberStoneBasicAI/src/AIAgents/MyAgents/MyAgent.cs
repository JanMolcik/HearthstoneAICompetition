using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using System.Linq;
using System.Threading.Tasks;
using SabberStoneBasicAI.Meta;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneBasicAI.Score;


// TODO choose your own namespace by setting up <submission_tag>
// each added file needs to use this namespace or a subnamespace of it
namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class MyAgent : AbstractAgent
	{
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();
		private Dictionary<string, double> ProbabilitiesDict = new Dictionary<string, double>();

		public override void InitializeAgent()
		{
			System.Type deckType = typeof(Decks);

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

			// Implement a simple Mulligan Rule
			if (player.MulliganState == Mulligan.INPUT)
			{
				List<int> mulligan = new AggroScore().MulliganRule().Invoke(player.Choice.Choices.Select(p => game.getGame().IdEntityDic[p]).ToList());
				return ChooseTask.Mulligan(player, mulligan);
			}

			countProbabilities();
			
			MCTS mcts = new MCTS(game, DecksDict, ProbabilitiesDict);
			PlayerTask result = mcts.Search();
			//StopWatch.StopWithMessage(String.Format("Compute {0} options in {1} ms", optcount, StopWatch.ElapsedMilliseconds));

			//Console.WriteLine("Final task: " + result);
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
		}
	}
}
