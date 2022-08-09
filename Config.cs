using System.ComponentModel;

namespace fpli {

    public enum Intent {
        DisplayHelp,
        DisplayVersion,
        ExecuteMiniLeagueAnalysis,
    }

    public class Config {

        public Intent intent                { get; set; } = Intent.DisplayHelp;
        public int maxManagers              { get; set; } = 50; // maximum number of managers to retrieve
        public int leagueId                 { get; set; } = 0;  // the minileagueId to analyse
        public float callrate               { get; set; } = 1f; // maximum calls per second


        public static void displayHelp() {
			Console.WriteLine("usage: fpli <option>");
			Console.WriteLine("options:");
			Console.WriteLine(" --showHelp                          display this help");
			Console.WriteLine(" --showVersion                       display the version number");
			Console.WriteLine(" --leagueId <int>                    analyse this mini-league for stats");
			Console.WriteLine("\nSettings if doing analysis");
			Console.WriteLine(" --maxManagers <int, default 50>     maxiumum number of managers to retrieve");
			Console.WriteLine(" --callrate <float, default 1.0>     maxiumum calls per second");
		}


        public Config() {}

		public Config(string[] args) {

			switch (args.Length > 0 ? args[0] : "") {
				case "--showVersion":       intent = Intent.DisplayVersion;            	break;
				case "--leagueId":          intent = Intent.ExecuteMiniLeagueAnalysis; 	break;
			}

            // Retrieve further settings
			if (intent == Intent.ExecuteMiniLeagueAnalysis) {
                leagueId = _retrieveOption(args, "--leagueId", 0);
                maxManagers = _retrieveOption(args, "--maxManagers", maxManagers);
                callrate = _retrieveOption(args, "--callrate", callrate);
            }
		}


        static private T _retrieveOption<T>(string[] args, string option, T def) {
            
            bool found = false;
            foreach (string arg in args) {
                
                if (found) {
                    var converter = TypeDescriptor.GetConverter(typeof(T));
                    if (converter != null) {                            
                        return (T) converter.ConvertFromString(arg);
                    }

                    return default(T);
                }

                found = String.Equals(arg, option, StringComparison.OrdinalIgnoreCase);
            }

            return def;
        }
    }    
}