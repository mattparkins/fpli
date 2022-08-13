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

    public class Fixture
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
    }
}