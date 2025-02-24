namespace fpli {
	
    public class Chip {
        public string name { get; set; }
        public DateTime time { get; set; }
        public int @event { get; set; }
    }

    public class Current {
        public int @event { get; set; }
        public int points { get; set; }
        public int total_points { get; set; }
        public int? rank { get; set; }
        public int? rank_sort { get; set; }
        public int overall_rank { get; set; }
        public int bank { get; set; }
        public int value { get; set; }
        public int event_transfers { get; set; }
        public int event_transfers_cost { get; set; }
        public int points_on_bench { get; set; }
    }

    public class Past {
        public string season_name { get; set; }
        public int total_points { get; set; }
        public int rank { get; set; }
    }

    public class ManagerHistory {
        public List<Current> current { get; set; }      // Current season
        public List<Past> past { get; set; }            // Past seasons
        public List<Chip> chips { get; set; }           // When the chips were used in this season
    }
}