using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NapPlana.Core.Bot;
using NapPlana.Core.Bot.BotInstance;
using NapPlana.Core.Data.API;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;

namespace Valency.QQBot.Hydrogen.Services
{
	// 6. Steam 业务解耦
	public class SteamService
	{
		private readonly NapBot _bot;
		private readonly HttpClient _httpClt;

		public SteamService(NapBot bot, HttpClient httpClt)
		{
			_bot = bot;
			_httpClt = httpClt;
		}

		public async Task StartMonitorAsync(CancellationToken token)
		{
			if (string.IsNullOrEmpty(ConfigManager.Config.SteamApiKey))
			{
				Logger.WriteLog("STEAM", "SteamApiKey is empty, skipping...", ConsoleColor.Yellow);
				return;
			}

			var steamFactory = new SteamWebInterfaceFactory(ConfigManager.Config.SteamApiKey);
			var steamUser = steamFactory.CreateSteamWebInterface<SteamUser>(_httpClt);
			var lastStates = new Dictionary<ulong, (string Status, string Game)>();

			Logger.WriteLog("STEAM", "Steam monitor started.", ConsoleColor.Green);

			while (!token.IsCancellationRequested)
			{
				try
				{
					if (ConfigManager.Config.MonitorSteamIds == null || !ConfigManager.Config.MonitorSteamIds.Any())
					{
						await Task.Delay(TimeSpan.FromSeconds(30), token);
						continue;
					}

					var response = await steamUser.GetPlayerSummariesAsync(ConfigManager.Config.MonitorSteamIds);
					if (response?.Data != null)
					{
						foreach (var player in response.Data)
						{
							ulong sid = player.SteamId;
							string currentStatus = player.ProfileState.ToString();
							string currentGame = player.PlayingGameName ?? "";
							string name = player.Nickname ?? "Unknown";

							if (!lastStates.ContainsKey(sid))
							{
								lastStates[sid] = (currentStatus, currentGame);
								continue;
							}

							var (oldStatus, oldGame) = lastStates[sid];

							if (currentStatus != oldStatus)
							{
								Logger.WriteLog("STEAM-EVT", $"{name} status: {oldStatus} -> {currentStatus}", ConsoleColor.Blue);
								await NotifyGroups($"[Steam] {name} is now {currentStatus}");
							}

							if (currentGame != oldGame)
							{
								if (!string.IsNullOrEmpty(currentGame))
								{
									string action = string.IsNullOrEmpty(oldGame) ? "started playing" : "is now playing";
									Logger.WriteLog("STEAM-EVT", $"{name} {action}: {currentGame}", ConsoleColor.Blue);
									await NotifyGroups($"[Steam] {name} {action} {currentGame}");
								}
								else
								{
									Logger.WriteLog("STEAM-EVT", $"{name} stopped playing: {oldGame}", ConsoleColor.DarkBlue);
									await NotifyGroups($"[Steam] {name} stopped playing {oldGame}");
								}
							}
							lastStates[sid] = (currentStatus, currentGame);
						}
					}
				}
				catch (Exception ex) { Logger.WriteLog("STEAM-ERROR", $"Polling failed: {ex.Message}", ConsoleColor.Red); }
				await Task.Delay(TimeSpan.FromSeconds(45), token);
			}
		}

		private async Task NotifyGroups(string text)
		{
			var msg = MessageChainBuilder.Create().AddTextMessage(text).Build();
			foreach (var gid in ConfigManager.Config.RSSRegGroupIds.ToList())
				await _bot.SendGroupMessageAsync(new GroupMessageSend { GroupId = gid, Message = msg });
		}
	}
}
