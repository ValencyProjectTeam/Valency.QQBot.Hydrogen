using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using NapPlana.Core.Data.Event.Message;

namespace Valency.QQBot.Hydrogen
{


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
}
