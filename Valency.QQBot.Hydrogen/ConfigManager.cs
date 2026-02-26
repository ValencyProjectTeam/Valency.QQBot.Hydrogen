using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Valency.QQBot.Hydrogen.DataModels;

namespace Valency.QQBot.Hydrogen
{
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
}
