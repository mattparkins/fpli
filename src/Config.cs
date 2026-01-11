using System.ComponentModel;

namespace fpli {

    public enum Intent {
        DisplayHelp,
        DisplayVersion,
        ExecuteMiniLeagueAnalysis,
        ExecuteFixtureAnalysis,
        ExecuteExportForAI
    }

    public class Config {

        public Intent intent                { get; private set; } = Intent.DisplayHelp;
        public float callrate               { get; private set; } = 1f;     // maximum calls per second

        // League analysis options
        public int maxManagers              { get; private set; } = 50;     // maximum number of managers to retrieve
        public int leagueId                 { get; private set; } = 0;      // the minileagueId to analyse
        public bool incManagersInCaptaincy  { get; private set; } = false;  // include managers in captaincy summary
        public bool facebookFormat          { get; private set; } = false;  // format output for Facebook posts
        public bool whatsappFormat          { get; private set; } = false;  // format output for WhatsApp

        // Fixture analysis options
        public int gameweek                 { get; private set; } = 0;      // Which gameweek to target
        public int fixtureCount             { get; private set; } = 0;      // Number of fixtures to analyse
        public List<string> fixturePicks    { get; set;} = new List<string>();  // fixture analysis, teams have already picked successfully
        public List<string> excludeTeams    { get; set;} = new List<string>();  // fixture analysis, which teams to exclude


        public static void displayHelp() {
			Console.WriteLine("usage: fpli <option>");
			Console.WriteLine("options:");
			Console.WriteLine(" --showHelp                              display this help");
			Console.WriteLine(" --showVersion                           display the version number");
			Console.WriteLine(" --leagueId <int>                        analyse this mini-league for stats");
            Console.WriteLine(" --executeFixtureAnalysis <int> <int>    analyse from this GW, this many fixtures for max win rate");
            Console.WriteLine(" --executeExportForAI <int>              export data for AI analysis up to this GW");

            Console.WriteLine("\nGeneral settings");
            Console.WriteLine(" --callrate <float, default 1.0>         maxiumum calls per second");
            
			Console.WriteLine("\nLeague analysis options");
			Console.WriteLine(" --maxManagers <int, default 50>         maxiumum number of managers to retrieve");
            Console.WriteLine(" --incManagersInCaptaincy                include manager names in captaincy summary.");
            Console.WriteLine(" --facebook                              format output for Facebook posts (bold headers, no emoji triggers)");
            Console.WriteLine(" --whatsapp                              format output for WhatsApp (*bold* headers)");

            Console.WriteLine("\nFixture analysis options");
			Console.WriteLine(" --previousPicks <string>...             picks so far, one per gameweek ");
            Console.WriteLine(" --excludeTeams <string>...              teams to exclude from analysis");
		}
        

		public Config(string[] args) {

			switch (args.Length > 0 ? args[0] : "") {
				case "--showVersion":               intent = Intent.DisplayVersion;            	break;
				case "--leagueId":                  intent = Intent.ExecuteMiniLeagueAnalysis; 	break;
                case "--executeFixtureAnalysis":    intent = Intent.ExecuteFixtureAnalysis;     break;
                case "--exportForAI":               intent = Intent.ExecuteExportForAI;         break;
			}

            // General options
            callrate = _retrieveOption(args, "--callrate", callrate);

            // Retrieve further settings
			if (intent == Intent.ExecuteMiniLeagueAnalysis) {
                leagueId = _retrieveOption(args, "--leagueId", 0);
                maxManagers = _retrieveOption(args, "--maxManagers", maxManagers);
                incManagersInCaptaincy = _retrieveOptionExists(args, "--incManagersInCaptaincy");
                facebookFormat = _retrieveOptionExists(args, "--facebook");
                whatsappFormat = _retrieveOptionExists(args, "--whatsapp");
            }

            // Retrieve further settings
			if (intent == Intent.ExecuteFixtureAnalysis) {
                List<int> p = _retrieveVariableOptions<int>(args, "--executeFixtureAnalysis");
                gameweek = (p.Count >= 1) ? p[0] : 0;
                fixtureCount = (p.Count >= 2) ? p[1] : 0;
                fixturePicks = _retrieveVariableOptions<string>(args, "--previousPicks");
                excludeTeams = _retrieveVariableOptions<string>(args, "--excludeTeams");
            }

            // Retrieve further settings
			if (intent == Intent.ExecuteExportForAI) {
                List<int> p = _retrieveVariableOptions<int>(args, "--exportForAI");
                gameweek = (p.Count >= 1) ? p[0] : 0;
            }
		}


        // RetrieveOption return the element following the option found in the args array
        // or the default option for that type.

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


        // RetrieveVariableOptions returns a List<T> of each of this option's element 
        // until it reaches either the end of the array passed in or a new option beginning --
        // Returns an empty list if the option isn't found or has no elements

        static private List<T> _retrieveVariableOptions<T>(string[] args, string option) {
            
            var r = new List<T>();

            bool found = false;
            foreach (string arg in args) {
                
                if (found) {
                    // We're traversing the found options, have we hit a new option?
                    if (arg.Length > 2 && arg.Substring(0,2)=="--") {
                        break;
                    }

                    var converter = TypeDescriptor.GetConverter(typeof(T));
                    if (converter != null) {                            
                        r.Add((T) converter.ConvertFromString(arg));
                    }
                }

                found |= String.Equals(arg, option, StringComparison.OrdinalIgnoreCase);
            }

            return r;
        }
    

        static private Boolean _retrieveOptionExists(string[] args, string option) {
            foreach (string arg in args) {
                if (String.Equals(arg, option, StringComparison.OrdinalIgnoreCase)) { 
                    return true;
                }
            }

            return false;
        }


        
    }    
}