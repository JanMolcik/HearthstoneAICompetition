using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneBasicAI.Score
{
	class EjnarScore : Score
	{
		public override int Rate()
		{
			int result = 0;
			if (OpHeroHp < 1)
				return Int32.MaxValue;

			if (HeroHp < 1)
				return Int32.MinValue;

			result = MyState() - OpState();

			return result;
		}

		private int MyState()
		{
			int result = 0;

			result += Convert.ToInt32(2 * Math.Sqrt(HeroHp));

			for (int i = 0; i < HandCnt; i++)
			{
				result += i < 3 ? 3 : 2;
			}

			if (DeckCnt > 0)
			{
				result += Convert.ToInt32(Math.Sqrt(DeckCnt));
			}

			result += MinionTotAtk + MinionTotHealth;

			foreach(Minion m in BoardZone)
			{
				// add value of minion's passive effects etc..
			}

			return result;
		}

		private int OpState()
		{
			int result = 0;

			result += Convert.ToInt32(2 * Math.Sqrt(OpHeroHp));

			for (int i = 0; i < OpHandCnt; i++)
			{
				result += i < 3 ? 3 : 2;
			}

			if (OpDeckCnt > 0)
			{
				result += Convert.ToInt32(Math.Sqrt(OpDeckCnt));
			}

			result += OpMinionTotAtk + OpMinionTotHealth;

			foreach (Minion m in OpBoardZone)
			{
				// add value of minion's passive effects etc..
			}

			return result;
		}

		public override Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}
	}
}
