namespace fpli {
	public class Elo {
		public double Rating 	{ get; private set; }
		public double K 		{ get; private set; }

		private double _initialRating;
		
		const double _formEroder = 0.96;

		public Elo(double initialRating = 1200.0) {
			Rating = _initialRating = initialRating;
			K = EloManager.K * 2;
		}

		public double NewMatch(double opponentRating, double outcome) {
			double diff = opponentRating - Rating;
			double ratio = diff / 400.0;
			double hxs = 1.0 /(1.0 + Math.Pow(10.0, ratio));
			double hdiff = (K * (outcome - hxs));
			Rating += hdiff;

			if (K > EloManager.K) {
				K *= 0.85;
			}

			if (K < EloManager.K) {
				K = EloManager.K;
			}

			// if (outcome >= 1.0) {
			// 	WinForm += 1.0;
			// 	WinForm *= _formEroder;
			// 	DrawForm *= _formEroder;
			// }

			// if (outcome >= 0.4 && outcome <= 0.6) {
			// 	WinForm *= _formEroder;
			// 	DrawForm *= _formEroder;
			// }

			return hdiff;
		}

		// Team will have an artificially low ELO having been relegated so we need to account 
		// for the rebuild that got them promoted and the initial energy that promoted teams bring
		public void Promoted(double initialRating = 1200.0) {
			
			// Boost the initial rating and their current rating
			Rating = _initialRating = initialRating;

			// Increase the K to account for uncertainty
			K = EloManager.K;
		}
	}

	public static class EloManager {
		
		public static Dictionary<Venue, Dictionary<int, Elo>> TeamElo {get; private set; } = new();		// Persistent club code, elo
		public static double K {get; private set;}
		private static int _highestHistoricalSeason = 0, _lowestHistoricalSeason = 99999999;
		
		public static void Initialise(double k) {
			TeamElo[Venue.HOME] = new();
			TeamElo[Venue.AWAY] = new();

			K = k;
			History history = FPLData.Instance.History;
			foreach (var kv in history.Bootstrap) {
				Console.WriteLine($"TeamElo processing {kv.Key} season");

				if (kv.Key > _highestHistoricalSeason) {
					_highestHistoricalSeason = kv.Key;
				}

				if (kv.Key < _lowestHistoricalSeason) {
					_lowestHistoricalSeason = kv.Key;
				}

				
				_processSeason(kv.Key);
			}

			//_processSeason(0);	
		}


		// Process season.  The passed season refers to the year in which the competition
		// finished, or zero for the current season

		private static void _processSeason(int season) {

			int skipped = 0;
			int processed = 0;

			Bootstrap bootstrap = FPLData.Instance.Bootstrap;
			Bootstrap previousBootstrap = (_highestHistoricalSeason > 0) ? FPLData.Instance.History.Bootstrap[_highestHistoricalSeason] : null;
			Dictionary<int, List<Fixture>> seasonFixtures = FPLData.Instance.Fixtures;

			// If the season passed is a historical one
			if (season != 0) {
				History history = FPLData.Instance.History;
				
				bootstrap = history.Bootstrap[season];
				seasonFixtures = history.Fixtures[season];
			}

			bool isFirstSeasonOfData = (season == _lowestHistoricalSeason);

			// Add teams new to the PL
			bootstrap.teams.ForEach(team => {
				bool wasPromoted = (previousBootstrap?.teams.Find(t => t.code == team.code) == null);

				if (!TeamElo[Venue.HOME].ContainsKey(team.code)) {
					Console.WriteLine($"    Adding {team.name}");
					TeamElo[Venue.HOME][team.code] = new Elo(isFirstSeasonOfData ? 1200 : 1000);
					TeamElo[Venue.AWAY][team.code] = new Elo(isFirstSeasonOfData ? 1200 : 900);
				} else if (wasPromoted) {
					Console.WriteLine($"    Adjusting promoted old team {team.name}");
					TeamElo[Venue.HOME][team.code].Promoted(isFirstSeasonOfData ? 1200 : 1050);
					TeamElo[Venue.AWAY][team.code].Promoted(isFirstSeasonOfData ? 1200 : 950);
				}
			});

			// For each gameweek (key = gw, value = list of fixtures)
			foreach(var kv in seasonFixtures.OrderBy(f => f.Key)) {
				List<Fixture> fixtures = kv.Value;

				Console.WriteLine($"{season}.GW{kv.Key}");

				fixtures.OrderBy(f => f.id).ToList().ForEach(fix => {
					Team homeTeam = bootstrap.GetTeamFromId(fix.team_h);
					Team awayTeam = bootstrap.GetTeamFromId(fix.team_a);

					if (fix.team_h_score != null) {
						//Console.WriteLine($"    {fix.kickoff_time}    {homeTeam.short_name} {fix.team_h_score} - {fix.team_a_score} {awayTeam.short_name}");
						_processResult(season, homeTeam.code, awayTeam.code, (int) fix.team_h_score, (int) fix.team_a_score);
						processed++;
					} else {
						skipped++;
					}
				});
			}

			Console.WriteLine($"    processed {processed} fixtures, skipped {skipped}");
		}

		private static void _processResult(int season, int hcode, int acode, int hscore, int ascore) {
			double hr = TeamElo[Venue.HOME][hcode].Rating;
			double ar = TeamElo[Venue.AWAY][acode].Rating;
			double hsc = hscore > ascore ? 1.0 : 0.0;

			double hdiff = TeamElo[Venue.HOME][hcode].NewMatch(ar, 	hsc);
			double adiff = TeamElo[Venue.AWAY][acode].NewMatch(hr, 1.0-hsc);

			Team homeTeam = FPLData.Instance.Bootstrap.GetTeamFromCode(hcode) ?? FPLData.Instance.History.Bootstrap[season].GetTeamFromCode(hcode);
			Team awayTeam = FPLData.Instance.Bootstrap.GetTeamFromCode(acode) ?? FPLData.Instance.History.Bootstrap[season].GetTeamFromCode(acode);

			Console.WriteLine($"    (K:{TeamElo[Venue.HOME][hcode].K:##.#}) {hdiff,5:+#0.0;-#0.0} {TeamElo[Venue.HOME][hcode].Rating:0000} {homeTeam.short_name} {hscore} - {ascore} {awayTeam.short_name} {TeamElo[Venue.AWAY][acode].Rating:0000} {adiff,5:+#0.0;-#0.0} (K:{TeamElo[Venue.AWAY][acode].K:##.#})");
		}
	}
}