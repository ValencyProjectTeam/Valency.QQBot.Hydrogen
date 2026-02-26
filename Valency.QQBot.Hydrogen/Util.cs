using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valency.QQBot.Hydrogen
{
	internal class Util
	{
	}
	public class Logger
	{
		// 线程锁，防止多线程同时写入同一个文件导致冲突
		private static readonly object _fileLock = new object();

		public static void WriteLog(string category, string message, ConsoleColor color)
		{
			DateTime now = DateTime.Now;

			// 1. 格式化控制台显示
			string timeStr = now.ToString("HH:mm:ss");
			string categoryFixed = category.PadRight(8);

			// --- 控制台输出逻辑 ---
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write($"[{timeStr}] ");
			Console.ForegroundColor = color;
			Console.Write($"[{categoryFixed}] ");
			Console.ResetColor();
			Console.WriteLine(message);

			// --- 文件写入逻辑 (实现每天一个文件) ---
			try
			{
				// 确定文件夹路径 (程序运行目录/logs)
				string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

				// 关键：文件名按日期命名，例如 "2023-10-27.log"
				// 只要日期不变，Path.Combine 得到的就是同一个文件路径
				string fileName = $"{now:yyyy-MM-dd}.log";
				string filePath = Path.Combine(logDirectory, fileName);

				// 构造写入文件的纯文本行（不带颜色代码）
				string logLine = $"[{timeStr}] [{categoryFixed}] {message}{Environment.NewLine}";

				lock (_fileLock)
				{
					// 如果文件夹不存在，则创建
					if (!Directory.Exists(logDirectory))
					{
						Directory.CreateDirectory(logDirectory);
					}

					// AppendAllText 的逻辑：
					// 1. 如果文件(filePath)不存在，它会自动创建一个新文件
					// 2. 如果文件已存在，它会打开文件并在末尾追加内容
					// 3. 写入完成后自动关闭文件释放资源
					File.AppendAllText(filePath, logLine, Encoding.UTF8);
				}
			}
			catch (Exception ex)
			{
				// 防止因为权限或磁盘空间问题导致整个程序崩溃
				Console.WriteLine($"[LOG ERROR] 无法写入文件: {ex.Message}");
			}
		}
	}
}
