using System.Text;

namespace fpli {
	public class AnalyserExportForAI: Analyser {

		public override bool RequiresHistory { get { return false; } }
		public override void Preprocess() {}


		public AnalyserExportForAI(FPLData fpl, Config config): base(fpl, config) {
			if (_config.gameweek <= 0) {
				Program.Quit("Gameweek is invalid or not set");
			}
		}

		public override async Task PreFetch() {
			if (_config.gameweek >=  _fpl.Bootstrap.events.Count) {
				Program.Quit("Gameweek beyond end of the season");
			}

			int i = _config.gameweek +1;
			while (i-- > 0) {
				await _fpl.LoadFixtures(i);
			}
		}

		public override void Analyse() {
			string folder = "export/";
			Console.WriteLine($"Exporting data for AI to {folder}");

			Directory.CreateDirectory(folder);

			// Export
			ExportToCSV($"{folder}teams.csv", _fpl.Bootstrap.teams);
			ExportToCSV($"{folder}upcomingFixtures.csv", _fpl.Fixtures[_config.gameweek]);
			ExportToCSVMulti($"{folder}results.csv", _fpl.Fixtures, 1, _config.gameweek -1);
			
		}

		void ExportToCSV<T>(string filename, List<T> list) where T: ICSVExportable {
			Console.Write($"Exporting {filename}, ");

    		using (StreamWriter sw = new StreamWriter(filename)) {
				for (int index = 0; index < list.Count; index++) {
					T item = list[index];
					item.ToCSV(sw, index == 0);
				}
			}

			Console.WriteLine($"ok, {list.Count} rows written");
		}

		void ExportToCSVMulti<T>(string filename, Dictionary<int, List<T>> dicList, int startIndex, int endIndex) where T: ICSVExportable {
			Console.Write($"Exporting {filename}, ");

			int li = startIndex;
			int ctr = 0;

    		using (StreamWriter sw = new StreamWriter(filename)) {

				do {

					List<T> list = dicList[li];

					for (int index = 0; index < list.Count; index++) {
						T item = list[index];
						item.ToCSV(sw, index == 0 && ctr == 0);
					}

					ctr += list.Count;
				} while (++li <= endIndex);
			}

			Console.WriteLine($"ok, {ctr} rows written");
		}
	}
}