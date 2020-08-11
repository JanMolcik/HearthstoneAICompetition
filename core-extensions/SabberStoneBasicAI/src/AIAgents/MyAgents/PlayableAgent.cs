using SabberStoneBasicAI.Meta;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class PlayableAgent
	{
		private readonly string[] DeckNames = new string[] {"AggroPirateWarrior", "MidrangeBuffPaladin", "MidrangeJadeShaman", "MidrangeSecretHunter",
			"MiraclePirateRogue", "RenoKazakusDragonPriest", "RenoKazakusMage", "ZooDiscardWarlock" };
		private readonly Dictionary<string, CardClass> ClassDict = new Dictionary<string, CardClass>()
		{
			{ "AggroPirateWarrior", CardClass.WARRIOR },
			{ "MidrangeBuffPaladin", CardClass.PALADIN },
			{ "MidrangeJadeShaman", CardClass.SHAMAN },
			{ "MidrangeSecretHunter", CardClass.HUNTER },
			{ "MiraclePirateRogue", CardClass.ROGUE },
			{ "RenoKazakusDragonPriest", CardClass.PRIEST },
			{ "RenoKazakusMage", CardClass.MAGE },
			{ "ZooDiscardWarlock", CardClass.WARLOCK },

		};
		private Dictionary<string, List<Card>> DecksDict = new Dictionary<string, List<Card>>();

		public AbstractAgent Agent { get; set; } = new GreedyAgent();
		public CardClass AgentClass { get; set; } = CardClass.WARRIOR;
		public List<Card> Deck { get; set; } = Decks.AggroPirateWarrior;

		public PlayableAgent()
		{
			System.Type deckType = typeof(Decks);

			foreach (string deckName in DeckNames)
			{
				var cards = (IEnumerable<Card>)deckType
					.GetProperty(deckName)
					.GetGetMethod()
					.Invoke(null, null);

				//cards.ToList().ForEach(c => Console.WriteLine(c));
				DecksDict.Add(deckName, cards.ToList());
			}
		}

		public void SetDeck(string deckName)
		{
			List<Card> value;
			if (DecksDict.TryGetValue(deckName, out value))
			{
				Deck = value;
				AgentClass = ClassDict[deckName];
			}
			else
			{
				Console.WriteLine("Deck not found! Using default deck");
			}
		}
	}
}
