using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valency.QQBot.Hydrogen.DataModels
{
	public class BotConfig
	{
		public List<string> RssUrls { get; set; } = new();
		public List<string> TargetGroupIds { get; set; } = new();

		public List<string> RSSRegGroupIds { get; set; } = new();
		public string SteamApiKey { get; set; } = "";
		public List<ulong> MonitorSteamIds { get; set; } = new();
	}
}
