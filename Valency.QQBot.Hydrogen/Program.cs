using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Syndication;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using NapPlana.Core.Bot;
using NapPlana.Core.Bot.BotInstance;
using NapPlana.Core.Data;
using NapPlana.Core.Data.API;
using NapPlana.Core.Data.Event.Message;
using NapPlana.Core.Event.Handler;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;

namespace Valency.QQBot.Hydrogen
{
	public class BotConfig
	{
		public List<string> RssUrls { get; set; } = new();
		public List<string> TargetGroupIds { get; set; } = new();
		public string SteamApiKey { get; set; } = "";
		public List<ulong> MonitorSteamIds { get; set; } = new();
	}

	// 1. 日志解耦
	public static class Logger
	{
		public static void WriteLog(string category, string message, ConsoleColor color)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
			Console.ForegroundColor = color;
			Console.Write($"[{category.PadRight(8)}] ");
			Console.ResetColor();
			Console.WriteLine(message);
		}
	}

	// 2. 配置管理解耦
	public static class ConfigManager
	{
		private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
		public static BotConfig Config { get; private set; } = new();

		public static void Load()
		{
			if (File.Exists(ConfigPath))
				Config = JsonSerializer.Deserialize<BotConfig>(File.ReadAllText(ConfigPath)) ?? new();
		}

		public static void Save()
		{
			var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
			File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, options));
		}
	}

	// 3. 消息存储解耦
	public static class MessageStore
	{
		private static readonly string BaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "msgbox");

		public static async Task SaveMessageAsync(GroupMessageEvent ev)
		{
			try
			{
				if (!Directory.Exists(BaseDir)) Directory.CreateDirectory(BaseDir);

				string fileName = $"msg_{DateTime.Now:yyyyMMdd}.jsonl";
				string filePath = Path.Combine(BaseDir, fileName);

				var record = new
				{
					Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
					ev.GroupId,
					ev.UserId,
					RawMsg = ev.RawMessage,
					MsgId = ev.MessageId,
					Sender = ev.Sender?.Nickname
				};

				string jsonLine = JsonSerializer.Serialize(record, new JsonSerializerOptions
				{
					Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
				}) + Environment.NewLine;

				await File.AppendAllTextAsync(filePath, jsonLine);
			}
			catch (Exception ex)
			{
				Logger.WriteLog("STORAGE", $"保存消息至本地失败: {ex.Message}", ConsoleColor.Red);
			}
		}
	}

	// 4. 指令处理解耦
	public class CommandHandler
	{
		private readonly NapBot _bot;
		public CommandHandler(NapBot bot) => _bot = bot;

		private const string HelloMsg = "你好！我是一个普通的机器人~";

		public async Task<bool> HandleCommandAsync(GroupMessageEvent ev)
		{
			// 意思是：匹配一段数字，但这段数字前面必须紧跟着 "qq="
			string pattern = @"(?<=qq=)\d+";
			// 获取@的人的qq号
			string atTarget = Regex.Match(ev.RawMessage, pattern).Value;

			if (atTarget == Program.BotSelfId.ToString())
			{
				if (ev.RawMessage.Contains("你好") || ev.RawMessage.Contains("你是谁"))
				{
					await SendReply(ev, HelloMsg);
				}
			}

			var args = ev.RawMessage.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (args.Length == 0) return false;

			string root = args[0].ToLower();

			// --- 新增：全局 help 命令 ---
			if (root == "help" || root == "帮助")
			{
				return await SendGlobalHelp(ev);
			}

			// 如果只有根命令（如只发了 "rss"），则显示该模块的帮助
			if (args.Length < 2)
			{
				if (root == "rss") return await SendRssHelp(ev);
				if (root == "steam") return await SendSteamHelp(ev);
				return false;
			}

			string action = args[1].ToLower();
			string value = args.Length > 2 ? args[2] : string.Empty;

			if (root == "rss")
			{
				Logger.WriteLog("CMD", $"执行 RSS 命令: {action} {value}", ConsoleColor.Magenta);
				switch (action)
				{
					case "help": return await SendRssHelp(ev); // rss help
					case "add":
						if (string.IsNullOrEmpty(value)) return await SendReply(ev, "❌ 用法: rss add [URL]");
						ConfigManager.Config.RssUrls.Add(value);
						ConfigManager.Save();
						return await SendReply(ev, $"✅ 已添加 RSS: {value}");
					case "list":
						return await SendReply(ev, $"当前有 {ConfigManager.Config.RssUrls.Count} 个 RSS 源。");
					case "add-group":
						if (string.IsNullOrEmpty(value)) return await SendReply(ev, "❌ 用法: rss add-group [ID/this]");
						string targetId = value == "this" ? ev.GroupId.ToString() : value;

						if (!ConfigManager.Config.TargetGroupIds.Contains(targetId))
						{
							ConfigManager.Config.TargetGroupIds.Add(targetId);
							ConfigManager.Save();
							return await SendReply(ev, $"✅ 已添加 RSS 推送目标: {targetId}");
						}
						return await SendReply(ev, $"ℹ️ 群组 {targetId} 已经在推送列表中。");
					default:
						return await SendRssHelp(ev);
				}
			}
			else if (root == "steam")
			{
				Logger.WriteLog("CMD", $"执行 Steam 命令: {action} {value}", ConsoleColor.Magenta);
				switch (action)
				{
					case "help": return await SendSteamHelp(ev); // steam help
					case "set-key":
						if (string.IsNullOrEmpty(value)) return await SendReply(ev, "❌ 用法: steam set-key [Key]");
						ConfigManager.Config.SteamApiKey = value;
						ConfigManager.Save();
						return await SendReply(ev, "✅ Steam API Key 已更新");
					case "add":
						if (ulong.TryParse(value, out ulong sid))
						{
							ConfigManager.Config.MonitorSteamIds.Add(sid);
							ConfigManager.Save();
							return await SendReply(ev, $"✅ 已添加 SteamID: {sid}");
						}
						return await SendReply(ev, "❌ 无效的 SteamID64 格式");
					default:
						return await SendSteamHelp(ev);
				}
			}
			else if (root == "reg")
			{
				Logger.WriteLog("CMD",$"添加群聊：{action}",ConsoleColor.Blue);
				if (string.IsNullOrEmpty(action)) return await SendReply(ev, "❌ 用法: rss add-group [ID/this]");
				string targetId = action == "this" ? ev.GroupId.ToString() : action;

				if (!ConfigManager.Config.TargetGroupIds.Contains(targetId))
				{
					ConfigManager.Config.TargetGroupIds.Add(targetId);
					ConfigManager.Save();
					return await SendReply(ev, $"✅ 已添加群目标: {targetId}");
				}
				return await SendReply(ev, $"ℹ️ 群组 {targetId} 已经在推送列表中。");
			}
				return false;
		}

		// --- 帮助信息模板 ---

		private async Task<bool> SendGlobalHelp(GroupMessageEvent ev)
		{
			string help = "🤖 机器人指令帮助\n" +
						  "━━━━━━━━━━━━━━\n" +
						  "📡 [RSS 订阅管理]\n" +
						  "输入: rss help\n\n" +
						  "🎮 [Steam 状态监控]\n" +
						  "输入: steam help\n" +
						  "━━━━━━━━━━━━━━\n" +
						  "直接输入根命令也可查看详情";
			return await SendReply(ev, help);
		}

		private async Task<bool> SendRssHelp(GroupMessageEvent ev)
		{
			string help = "📡 RSS 命令列表:\n" +
						  "• rss add [URL] - 添加订阅源\n" +
						  "• rss list - 查看所有订阅源\n" +
						  "• rss add-group this - 推送到本群\n" +
						  "• rss add-group [ID] - 推送到指定群";
			return await SendReply(ev, help);
		}

		private async Task<bool> SendSteamHelp(GroupMessageEvent ev)
		{
			string help = "🎮 Steam 命令列表:\n" +
						  "• steam set-key [Key] - 设置 API 密钥\n" +
						  "• steam add [ID64] - 添加监控玩家\n" +
						  "提示: 需确保玩家隐私设置为公开";
			return await SendReply(ev, help);
		}

		public async Task<bool> SendReply(GroupMessageEvent ev, string text)
		{
			var msg = MessageChainBuilder.Create().AddReplyMessage(ev.MessageId.ToString()).AddTextMessage(" " + text).Build();
			await _bot.SendGroupMessageAsync(new GroupMessageSend { GroupId = ev.GroupId.ToString(), Message = msg });
			return true;
		}
	}

	// 5. RSS 业务解耦
	public class RssService
	{
		private readonly NapBot _bot;
		private readonly HttpClient _httpClt;

		public RssService(NapBot bot, HttpClient httpClt)
		{
			_bot = bot;
			_httpClt = httpClt;
		}

		public async Task StartMonitorAsync(CancellationToken token)
		{
			var seenLinks = new HashSet<string>();
			bool isFirstRun = true;

			while (!token.IsCancellationRequested)
			{
				var urls = ConfigManager.Config.RssUrls.ToList();
				foreach (var url in urls)
				{
					try
					{
						Logger.WriteLog("RSS", $"正在抓取: {url}", ConsoleColor.DarkGray);
						using var response = await _httpClt.GetAsync(url, token);
						using var reader = XmlReader.Create(await response.Content.ReadAsStreamAsync(token));
						var feed = SyndicationFeed.Load(reader);

						foreach (var item in feed.Items.OrderBy(i => i.PublishDate))
						{
							string link = item.Links.FirstOrDefault()?.Uri.ToString() ?? item.Id;
							if (seenLinks.Add(link))
							{
								string summary = item.Summary?.Text ?? "无摘要";
								string cleanSummary = Regex.Replace(summary, "<.*?>", string.Empty).Replace("\n", " ");
								Logger.WriteLog("RSS-NEW", $"[{feed.Title.Text}] {item.Title.Text} | {cleanSummary.Take(50)}...", ConsoleColor.Yellow);

								if (!isFirstRun)
								{
									var msg = MessageChainBuilder.Create()
										.AddTextMessage($"📢 RSS 订阅更新\n源：{feed.Title.Text}\n标题：{item.Title.Text}\n链接：{link}")
										.Build();

									// 此处逻辑会自动应用 ConfigManager.Config.TargetGroupIds 中的新 ID
									foreach (var gid in ConfigManager.Config.TargetGroupIds.ToList())
										await _bot.SendGroupMessageAsync(new GroupMessageSend { GroupId = gid, Message = msg });
								}
							}
						}
					}
					catch (Exception ex) { Logger.WriteLog("RSS-ERROR", $"{url} -> {ex.Message}", ConsoleColor.Red); }
				}
				isFirstRun = false;
				await Task.Delay(TimeSpan.FromMinutes(1), token);
			}
		}
	}

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
			foreach (var gid in ConfigManager.Config.TargetGroupIds.ToList())
				await _bot.SendGroupMessageAsync(new GroupMessageSend { GroupId = gid, Message = msg });
		}
	}

	// 7. 主程序入口
	public class Program
	{
		internal const long BotSelfId = 3946388948;
		private const string BotToken = "ym_8d5Wpr9NnEJ~J";
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

				Logger.WriteLog("MSG", $"收到群[{messageEvent.GroupId}]内[{messageEvent.UserId}]的消息: {messageEvent.RawMessage}", ConsoleColor.Cyan);

				_ = MessageStore.SaveMessageAsync(messageEvent);

				if (await cmdHandler.HandleCommandAsync(messageEvent)) return;

				if (messageEvent.RawMessage.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase))
				{
					await cmdHandler.SendReply(messageEvent, "hi");
				}
			};

			await bot.StartAsync();
			await bot.SendGroupForwardMessageAsync(new GroupForwardMessageSend
			{
				GroupId = "1071939984",
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
					GroupId = "1071939984",
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