namespace fpli {
	
    public class ValueElement
    {
        public int value { get; set; }
        public int element { get; set; }
    }

	public class FixtureStat
    {
        public string identifier { get; set; }
        public List<ValueElement> a { get; set; }
        public List<ValueElement> h { get; set; } 
    }

    public class Fixture: ICSVExportable 
    {
        public int code { get; set; }
        public int @event { get; set; }
        public bool finished { get; set; }
        public bool finished_provisional { get; set; }
        public int id { get; set; }
        public DateTime kickoff_time { get; set; }
        public int minutes { get; set; }
        public bool provisional_start_time { get; set; }
        public bool started { get; set; }
        public int team_a { get; set; }
        public int? team_a_score { get; set; }
        public int team_h { get; set; }
        public int? team_h_score { get; set; }
        public List<FixtureStat> stats { get; set; }
        public int team_h_difficulty { get; set; }
        public int team_a_difficulty { get; set; }
        public int pulse_id { get; set; }

        // If the fixture is completed then it is a result with statistics, rather than just a fixture

		public void ToCSV(StreamWriter file, bool prependHeader = false) {
            if (finished) {
                if (prependHeader) {
                    file.WriteLine("home_team,home_goals,home_yellow_cards,home_red_cards,home_saves,away_team,away_goals,away_yellow_cards,away_red_cards,away_saves");
                }

                Team home = FPLData.Instance.Bootstrap.teams.Find(t => t.id == team_h);
                Team away = FPLData.Instance.Bootstrap.teams.Find(t => t.id == team_a);

                FixtureStat yc = stats.Find(s => s.identifier == "yellow_cards");
                FixtureStat rc = stats.Find(s => s.identifier == "red_cards");
                FixtureStat saves = stats.Find(s => s.identifier == "saves");

                file.WriteLine($"{home.name},{team_h_score},{yc.h.Count},{rc.h.Count},{saves.h.Count},{away.name},{team_a_score},{yc.a.Count},{rc.a.Count},{saves.a.Count}");
            } else {
                if (prependHeader) {
                    file.WriteLine("home_team,away_team");
                }

                string teamHome = FPLData.Instance.Bootstrap.teams.Find(t => t.id == team_h).name;
                string teamAway = FPLData.Instance.Bootstrap.teams.Find(t => t.id == team_a).name;

                file.WriteLine($"{teamHome},{teamAway}");
            }
		}
	}
}