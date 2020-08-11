using SabberStoneBasicAI.PartialObservation;
using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
	class MyScore : Score.Score
	{
		public override int Rate()
		{

			if (HeroHp < 1)
			{
				return Int32.MinValue;
			}

			if (OpHeroHp < 1)
			{
				return Int32.MaxValue;
			}

			int result = 0;
			int scale = 1000;

			Controller player = Controller;
			Controller opp = player.Opponent;

			int heroHp = HeroHp + player.Hero.Armor;
			int oppHeroHp = OpHeroHp + opp.Hero.Armor;
			result += (heroHp - oppHeroHp) * scale;

			result += HandCnt * 3 * scale;
			result -= OpHandCnt * 3 * scale;

			Minion[] minions = BoardZone.GetAll();
			result += minions.Count() * 2 * scale;
			foreach (Minion minion in minions)
			{
				result += MinionScore(minion) * scale;
			}

			Minion[] oppMinions = OpBoardZone.GetAll();
			result -= oppMinions.Count() * 2 * scale;
			foreach (Minion oppMinion in oppMinions)
			{
				result -= MinionScore(oppMinion) * scale;
			}

			return result;
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}

		private int MinionScore(Minion minion)
		{
			int result = 0;

			int minionHp = minion.Health;
			int minionAtk = minion.AttackDamage;

			result = minionAtk + minionHp;
			int baseValue = result;

			if (minion.IsFrozen)
			{
				return minionHp;
			}

			if (minion.HasTaunt)
			{
				result += 2;
			}

			if (minion.HasWindfury)
			{
				result += (int)(minionAtk * 0.5f);
			}

			if (minion.HasDivineShield)
			{
				result += (int)(baseValue * 1.5f);
			}

			if (minion.IsEnraged)
			{
				result += 1;
			}

			if (minion.HasStealth)
			{
				result += 1;
			}

			if (minion.CantBeTargetedBySpells)
			{
				result += (int)(baseValue * 1.5f);
			}

			return result;
		}
	}
}
