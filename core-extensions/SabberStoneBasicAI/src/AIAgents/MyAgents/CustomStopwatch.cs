using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SabberStoneBasicAI.AIAgents.MyAgents
{
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
