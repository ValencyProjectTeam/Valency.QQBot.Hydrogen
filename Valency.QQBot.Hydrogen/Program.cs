using System.Net;
using System.Net.Sockets;
using NapPlana.Core.Bot;
using NapPlana.Core.Data;
using NapPlana.Core.Data.API;
using NapPlana.Core.Event.Handler;
using Valency.QQBot.Hydrogen.Services;

namespace Valency.QQBot.Hydrogen
{
	public class Program
	{
		internal const long BotSelfId = 3946388948;
		private const string BotToken = "ym_8d5Wpr9NnEJ~J";
		internal const long AdminId = 3433559280;
		internal const string AdminGroup = "1071939984";
#if RELEASE
		private const string HostName = "localhost";
		private const string FallbackIp = "127.0.0.1";
		private const int BotPort = 3001;
#endif

#if DEBUG
		private const string HostName = "classicbyte.asia";
		private const string FallbackIp = "160.202.254.36";
		private const int BotPort = 10290;
#endif
		


		private static readonly HttpClient HttpClt = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

		public static async Task Main(string[] args)
		{
			ConfigManager.Load();

			var bot = PlanaBotFactory.Create()
				.SetSelfId(BotSelfId)
				.SetConnectionType(BotConnectionType.WebSocketClient)
				.SetIp(GetResolvedIp(HostName, FallbackIp))
				.SetPort(BotPort)
				.SetToken(BotToken)
				.Build();


			var cmdHandler = new CommandHandler(bot);
			var rssService = new RssService(bot, HttpClt);
			var steamService = new SteamService(bot, HttpClt);

			BotEventHandler.OnLogReceived += (level, message) =>
				Logger.WriteLog("CORE", $"[{level}] {message}", ConsoleColor.Gray);

			BotEventHandler.OnGroupMessageReceived += async (messageEvent) =>
			{
				if (messageEvent.UserId == bot.SelfId) return;

				Logger.WriteLog("MSG", $"收到群[{messageEvent.GroupId}]内[{messageEvent.Sender.Nickname}({messageEvent.UserId})]的消息: {messageEvent.RawMessage}", ConsoleColor.Cyan);

				_ = MessageStore.SaveMessageAsync(messageEvent);

				if (await cmdHandler.HandleCommandAsync(messageEvent)) return;

				if (messageEvent.RawMessage.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase))
				{
					await cmdHandler.SendReply(messageEvent, "hi");
				}
			};
			BotEventHandler.OnGroupPokeNoticeReceived += async (pokeEvent) =>
			{
				if (!ConfigManager.Config.TargetGroupIds.Contains(pokeEvent.GroupId.ToString()))
				{
					//return;
				}
				if (pokeEvent.UserId == bot.SelfId) return;
				if (pokeEvent.TargetId != bot.SelfId) return;
				Logger.WriteLog("POKE", $"收到好友[{pokeEvent.UserId}]的戳一戳", ConsoleColor.Magenta);

				await bot.SendPokeAsync(new PokeMessageSend() { TargetId = pokeEvent.UserId.ToString(), GroupId = pokeEvent.GroupId.ToString() });
			};

			await bot.StartAsync();

			await bot.SendGroupForwardMessageAsync(new GroupForwardMessageSend
			{
				GroupId = AdminGroup,
				Messages = MessageChainBuilder.Create().AddTextMessage($"来自{Environment.MachineName}的机器人已启动~欢迎和我聊天呀~").Build()
			});

			using var cts = new CancellationTokenSource();
			_ = Task.Run(() => rssService.StartMonitorAsync(cts.Token), cts.Token);
			_ = Task.Run(() => steamService.StartMonitorAsync(cts.Token), cts.Token);

			Logger.WriteLog("SYSTEM", "所有监控服务已启动。按下 Ctrl+C 退出。", ConsoleColor.Green);

			Console.CancelKeyPress += async (s, e) =>
			{
				e.Cancel = true;
				await bot.SendGroupForwardMessageAsync(new GroupForwardMessageSend
				{
					GroupId = AdminGroup,
					Messages = MessageChainBuilder.Create().AddTextMessage($"来自{Environment.MachineName}的机器人正在关闭~希望下次还能遇到你~").Build()
				});
				Logger.WriteLog("SYSTEM", "正在关闭服务...", ConsoleColor.Yellow);
				await bot.StopAsync();
				cts.Cancel();
			};

			try { await Task.Delay(-1, cts.Token); } catch (OperationCanceledException) { }
		}

		private static string GetResolvedIp(string host, string fallback)
		{
			try { return Dns.GetHostEntry(host).AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? fallback; }
			catch { return fallback; }
		}
	}
}