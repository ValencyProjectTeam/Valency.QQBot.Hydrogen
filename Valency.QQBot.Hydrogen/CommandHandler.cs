using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NapPlana.Core.Bot;
using NapPlana.Core.Bot.BotInstance;
using NapPlana.Core.Data.API;
using NapPlana.Core.Data.Event.Message;

namespace Valency.QQBot.Hydrogen
{

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

						if (!ConfigManager.Config.RSSRegGroupIds.Contains(targetId))
						{
							ConfigManager.Config.RSSRegGroupIds.Add(targetId);
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
						if (ev.UserId != Program.AdminId)
						{
							return await SendReply(ev, "你不是管理员喵~不能这么做喵~");
						}
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
				Logger.WriteLog("CMD", $"添加群聊：{action}", ConsoleColor.Blue);
				if (ev.UserId != Program.AdminId)
				{
					return await SendReply(ev, "你不是管理员喵~不能这么做喵~");
				}
				if (string.IsNullOrEmpty(action)) return await SendReply(ev, "❌ 用法: reg [ID/this]");
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
}
