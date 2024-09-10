using System.Text.RegularExpressions;

namespace fpli {
	public class History {
		public Dictionary<int, Bootstrap> Bootstrap 					{ get; private set; } = new();
		public Dictionary<int, Dictionary<int, List<Fixture>>> Fixtures	{ get; private set; } = new();

		public History() {}

		private string _historyPath;

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
	}
}