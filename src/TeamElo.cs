namespace fpli {
	public static class TeamElo {
		
		public static Dictionary<int, double> Elo {get; private set; } = new();		// Persistent club code, elo
		public static double K {get; private set;} = 20.0;
		
		public static void Initialise(double k) {
			K = k;
			History history = FPLData.Instance.History;
			foreach (var kv in history.Bootstrap) {
				Console.WriteLine($"TeamElo processing {kv.Key} season");
				_processSeason(kv.Key);
			}
		}

		private static void _processSeason(int season) {
			History history = FPLData.Instance.History;

			Bootstrap bootstrap = history.Bootstrap[season];

			bootstrap.teams.ForEach(team => {
				if (!Elo.ContainsKey(team.code)) {
					Console.WriteLine($"    Adding {team.name}");
					Elo[team.code] = 1200.0;
				}
			});

			int skipped = 0;
			int processed = 0;

			// For each gameweek (key = gw, value = list of fixtures)
			foreach(var kv in history.Fixtures[season]) {
				List<Fixture> fixtures = kv.Value;

				Console.WriteLine($"    GW {kv.Key}");

				fixtures.ForEach(fix => {
					Team homeTeam = bootstrap.GetTeamFromId(fix.team_h);
					Team awayTeam = bootstrap.GetTeamFromId(fix.team_a);

					Console.WriteLine($"        {fix.kickoff_time} {homeTeam.short_name} v {awayTeam.short_name} ");

					if (fix.team_h_score != null) {
						_processResult(homeTeam.code, awayTeam.code, fix.team_h_score, fix.team_a_score);
						processed++;
					} else {
						skipped++;
					}
				});
			}

			Console.WriteLine($"    processed {processed} fixtures, skipped {skipped}");
		}

		private static void _processResult(int hcode, int acode, int? hscore, int? ascore) {

		}
	}
}