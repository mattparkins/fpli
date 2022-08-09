namespace fpli {
	class Program {

		static readonly int _versionMajor = 1, _versionMinor = 0;

		public static void Quit(string msg) {
			Console.WriteLine(msg);
			Environment.Exit(-1);
		}

		static void _displayVersion() {
			Console.WriteLine($"fpli v{_versionMajor}.{_versionMinor}");
		}

		static async Task Main(string[] args) {
			Config config = new Config(args);
			switch (config.intent) {
				case Intent.ExecuteMiniLeagueAnalysis:	await _execute(config);               	break;
				case Intent.DisplayVersion:         	_displayVersion();                    	break;
				case Intent.DisplayHelp:        
				default:                            	Config.displayHelp();					break;
			}
		}

		static async Task _execute(Config config) {
			await Engine.Execute(config);
		}

		
	}
}