namespace fpli {
	public class FixtureAnalyser : Analyser {

		public FixtureAnalyser(FPLData fpl, Config config): base(fpl, config) {
			if (_config.gameweek <= 0) {
				Program.Quit("Gameweek is invalid or not set");
			}
			
			if (_config.fixtureCount <= 0) {
				Program.Quit("FixtureCount is invalid or not set");
			}

			if (_config.fixturePicks.Count >= _config.fixtureCount) {
				Program.Quit("FixturesAlreadyPicked count >= Gameweeks to analyse");
			}
		}

		public override async Task PreFetch() {
			if (_config.gameweek + _config.fixtureCount -1 >  _fpl.Bootstrap.events.Count) {
				Program.Quit("Gameweek and Analysis window extend beyond end of the season");
			}

			int i = _config.fixtureCount;
			while (i-- > 0) {
				await _fpl.LoadFixtures(_config.gameweek + i);
			}
		}


		public override void Preprocess() {}


		public override void Analyse() {
		}
	}
}