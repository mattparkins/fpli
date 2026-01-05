using System.Text.RegularExpressions;

namespace fpli {
	public class History {
		public Dictionary<int, Bootstrap> Bootstrap 					{ get; private set; } = new();
		public Dictionary<int, Dictionary<int, List<Fixture>>> Fixtures	{ get; private set; } = new();

		public History() {}

		private string _historyPath;
		private string _api;

		public void Load(string path) {
			_historyPath = path+"historic/";
			
			List<string> dirs = Directory.GetDirectories(_historyPath, "????").ToList();

			dirs.Sort();
			foreach (var dir in dirs) {
				//Sanity check - dir name must be able to be converted to an int between 2000 and 2100
				int season = 0;
				if (dir.Length >=4 && Int32.TryParse(dir.Substring(dir.Length -4,4), out season) && season >= 2000 && season <= 2100) {
					LoadSeason(_historyPath, season); 
				}
			};
		}

		private void LoadSeason(string path, int season) {
			string fullpath = path + season;

			// Load bootstrap
			Bootstrap[season] = Utils.DeserializeFromFile<Bootstrap>(fullpath+"/bootstrap-static.json");

			// Load fixtures
			Fixtures[season] = new Dictionary<int, List<Fixture>>();
			List<string> files = Directory.GetFiles(fullpath, "fixtures_GW*_complete.json").ToList();
			files.Sort();
			foreach (var fn in files) {
				//Extract GW id from filename
				int gw = 0;
				if (Int32.TryParse(Regex.Match(fn, @"\d+", RegexOptions.RightToLeft).Value, out gw)) {
					Fixtures[season][gw] = Utils.DeserializeFromFile<List<Fixture>>(fn);
				}
			}
		}

		// Sync completed gameweeks for the current season from FPL API to historic folder
		public async Task SyncCurrentSeason(fpli.Bootstrap bootstrap, string api, string cachePath) {
			_api = api;

			// Determine current season from bootstrap (use the year from the first gameweek deadline)
			var firstEvent = bootstrap.events.FirstOrDefault();
			if (firstEvent == null) return;

			int season = firstEvent.deadline_time.Year;
			string seasonPath = _historyPath + season + "/";

			// Create season directory if it doesn't exist
			if (!Directory.Exists(seasonPath)) {
				Console.WriteLine($"Creating historic folder for {season}");
				Directory.CreateDirectory(seasonPath);
			}

			// Ensure we have a Fixtures dictionary for this season
			if (!Fixtures.ContainsKey(season)) {
				Fixtures[season] = new Dictionary<int, List<Fixture>>();
			}

			// Find all completed gameweeks
			int currentGw = bootstrap.GetCurrentGameweekId();
			int syncedCount = 0;

			for (int gw = 1; gw <= bootstrap.events.Count; gw++) {
				var gameweek = bootstrap.events[gw - 1];
				bool isComplete = gw < currentGw || (gw == currentGw && gameweek.finished);

				if (!isComplete) continue;

				string historicFile = $"{seasonPath}fixtures_GW{gw}_complete.json";

				// Skip if already exists in historic folder
				if (File.Exists(historicFile)) continue;

				// Download to cache first
				string cacheFile = $"{cachePath}fixtures_GW{gw}_complete.json";
				Console.WriteLine($"Syncing GW{gw} to historic/{season}/");
				var fixtures = await Fetcher.FetchAndDeserialise<List<Fixture>>(
					cacheFile,
					$"{_api}fixtures/?event={gw}",
					Utils.DaysAsSeconds(30));

				// Copy from cache to historic folder
				File.Copy(cacheFile, historicFile);

				Fixtures[season][gw] = fixtures;
				syncedCount++;
			}

			if (syncedCount > 0) {
				Console.WriteLine($"Synced {syncedCount} gameweek(s) to historic/{season}/");
			}
		}
	}
}