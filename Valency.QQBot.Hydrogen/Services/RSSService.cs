using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using NapPlana.Core.Bot;
using NapPlana.Core.Bot.BotInstance;
using NapPlana.Core.Data.API;

namespace Valency.QQBot.Hydrogen.Services
{
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
									foreach (var gid in ConfigManager.Config.RSSRegGroupIds.ToList())
										await _bot.SendGroupMessageAsync(new GroupMessageSend { GroupId = gid, Message = msg });
								}
							}
						}
					}
					catch (Exception ex) { Logger.WriteLog("RSS-ERROR", $"{url} -> {ex.Message}", ConsoleColor.Red); }
				}
				isFirstRun = false;
				await Task.Delay(TimeSpan.FromMinutes(5), token);
			}
		}
	}
}
