using Shared.Packet.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
	public class HnSRandom
	{
		public static Server server;
		public static string StartRandomGame(string arg)
		{
			string stage = RandomStage();
			int num = 0;
			try { num = int.Parse(arg); }
			catch { return "Invalid number"; }
			if (num > server.Clients.Count)
				return "Number is higher than the amount of players";
			else if (num == server.Clients.Count)
				return "Please have at least one hider available";
			List<string> players = new List<string>();
			for (int i = 0; i < num; i++)
			{
				string n = RandomPlayer();
				if (players.Contains(n)) { i--; continue; }
				players.Add(n);
			}
			foreach (Client client in server.Clients)
			{

#pragma warning disable CS4014
				client.Send(new ChangeStagePacket
				{
					Stage = stage,
					Scenario = -1,
				});
#pragma warning restore CS4014
			}
			// To-do: Fake Player System to display messages, debug command to: Save Player Locations, Test the Fake Player.
			string play = string.Join(',', players.ToArray());
			return $"Starting Random Game on {stage} with seeker(s): {play}";
		}

		private static string RandomStage()
		{
			Random r = new Random();

			List<string> stageNames = new List<string>();
			foreach (var stage in Settings.Instance.StageSelection)
			{
				if (stage.enabled == true)
					stageNames.Add(stage.stageName);
			}
			int i = r.Next(0, stageNames.Count);
			try
			{
				return stageNames[i];
			}
			catch (IndexOutOfRangeException ex)
			{
				server.Logger.Warn("Please tell Luwuna that she doesnt know how Random works and that the array overflowed");
				return stageNames[i - 1];//I honestly dont remember how random works so I just have this incase it does go out of bounds
			}
		}

		private static string RandomPlayer()
		{
			Random r = new Random();
			int i = r.Next(0, server.Clients.Count);
			try
			{
				return server.Clients[i].Name;
			}
			catch
			{
				return server.Clients[i - 1].Name;
			}
		}
	}
}