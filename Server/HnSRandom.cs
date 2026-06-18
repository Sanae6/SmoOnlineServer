using Shared.Packet.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
	public static class HnSRandom
	{
		public static Server server;
		public static List<string> selectedPlayers = new List<string>();
		/// <summary>
		/// Function for private server hosts to start a random game of Hide & Seek
		/// </summary>
		/// <param name="arg">Integer expected for number of seekers</param>
		/// <returns>Returns a string with the selected stage and seekers to display in the console</returns>
		public static string StartRandomGame(string arg)
		{
			string stage = RandomStage();
			int num = 0;
			try { num = int.Parse(arg); } // checks if arg is a valid integer, if not return that the number is invalid
			catch { return "Invalid number"; }
			if (num > server.Clients.Count)
				return "Number is higher than the amount of players";
			else if (num == server.Clients.Count)
				return "Please have at least one hider available";
			int spacesLeft = server.Clients.Count - selectedPlayers.Count; // checks how many players have currently been unselected.
			server.Logger.Warn(spacesLeft.ToString());
			if (spacesLeft < num) // If spaces left is lower than the number of seekers wanted then the selectedPlayers list gets cleared
				selectedPlayers.Clear();
			List<string> players = new List<string>();
			for (int i = 0; i < num; i++)
			{
				string n = RandomPlayer();
				if (players.Contains(n) || selectedPlayers.Contains(n)) { i--; continue; } // if either list players or selectedPlayers contain the currently selected player, then redo the randomisation (if something happens where selectedPlayers doesnt get cleared this may cause an infinite loop)
				players.Add(n);
				selectedPlayers.Add(n);
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
			string play = string.Join(',', players);
			return $"Starting Random Game on {stage} with seeker(s): {play}";
		}
		/// <summary>
		/// Selects a stage based on a list in Settings
		/// </summary>
		/// <returns>Returns a string of a random stage</returns>
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
				server.Logger.Warn("Something has gone wrong");
				return stageNames[i - 1];
			}
		}
		/// <summary>
		/// Selects a random player currently connected to the server
		/// </summary>
		/// <returns></returns>
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

		public static string DebugMessage()
		{
			string s = string.Join(',', selectedPlayers);
			return $"Currently selected players: {s}";
		}
	}
}