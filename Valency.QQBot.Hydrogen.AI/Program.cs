using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using NapPlana.Core.Bot;
using NapPlana.Core.Bot.BotInstance;
using NapPlana.Core.Data;
using NapPlana.Core.Data.Event.Message;
using NapPlana.Core.Event.Handler;

namespace Valency.QQBot.Hydrogen.AI
{
	// 配置类：定义了默认值
	public class BotConfig
	{
		public long BotSelfId { get; set; } = 3946388948;
		public string BotToken { get; set; } = "ym_8d5Wpr9NnEJ~J";
		public string HostName { get; set; } = "SHIMIKOIHOMEVM5";
		public string FallbackIp { get; set; } = "192.168.183.128";
		public int BotPort { get; set; } = 3001;
	}

	// 配置管理类
	public static class ConfigManager
	{
		private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

		// 初始时即拥有默认值
		public static BotConfig Config { get; private set; } = new BotConfig();

		public static void Load()
		{
			if (File.Exists(ConfigPath))
			{
				try
				{
					var json = File.ReadAllText(ConfigPath);
					// 将读取到的内容反序列化并覆盖默认配置
					var loadedConfig = JsonSerializer.Deserialize<BotConfig>(json);
					if (loadedConfig != null)
					{
						Config = loadedConfig;
						Console.WriteLine("成功加载 config.json 配置。");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"加载 config.json 出错，将使用默认值。错误信息: {ex.Message}");
				}
			}
			else
			{
				Console.WriteLine("未找到 config.json，正在使用代码内置的默认配置启动...");
				// 可选：如果想在找不到文件时自动生成一个默认模板，可以取消下面这一行的注释
				SaveDefault(); 
			}
		}

		// 辅助方法：保存当前配置到文件
		public static void SaveDefault()
		{
			var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
			File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, options));
			Console.WriteLine($"已生成默认配置文件: {ConfigPath}");
		}
	}

	public class Program
	{
		public static async Task Main(string[] args)
		{
			// 1. 加载配置（若无文件则保留 BotConfig 类中的初始值）
			ConfigManager.Load();
			var cfg = ConfigManager.Config;

			// 2. 解析 IP 地址
			string resolvedIp = GetResolvedIp(cfg.HostName, cfg.FallbackIp);

			// 3. 使用 Factory 创建机器人实例
			var bot = PlanaBotFactory.Create()
				.SetSelfId(cfg.BotSelfId)
				.SetConnectionType(BotConnectionType.WebSocketClient)
				.SetIp(resolvedIp)
				.SetPort(cfg.BotPort)
				.SetToken(cfg.BotToken)
				.Build();

			// 4. 注册基础日志回调
			BotEventHandler.OnLogReceived += (level, message) =>
				Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [CORE] [{level}] {message}");

			BotEventHandler.OnPrivateMessageReceived += BotEventHandler_OnPrivateMessageReceived;

			// 5. 启动机器人
			Console.WriteLine($"正在启动机器人 {cfg.BotSelfId}");
			Console.WriteLine($"目标地址: {resolvedIp}:{cfg.BotPort}");

			await bot.StartAsync();

			Console.WriteLine("机器人已启动。按下 Ctrl+C 退出。");

			// 保持程序运行
			await Task.Delay(-1);
		}

		private static void BotEventHandler_OnPrivateMessageReceived(PrivateMessageEvent obj)
		{
			string username = obj.Sender?.Nickname ?? "未知用户";
			string message = obj.RawMessage ?? string.Empty;
			string prompt = $"我是‘{username}’，{message}";
			Console.WriteLine($"提示词: {prompt}");

		}

		private static string GetResolvedIp(string host, string fallback)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(host)) return fallback;
				var addresses = Dns.GetHostEntry(host).AddressList;
				return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? fallback;
			}
			catch
			{
				return fallback;
			}
		}
	}
}