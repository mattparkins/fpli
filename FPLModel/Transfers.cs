namespace fpli {
	
	// Requires a wrapper as this file is an array of these items - could attach it to the entry id

	public class Transfer
    {
        public int element_in { get; set; }
        public int element_in_cost { get; set; }
        public int element_out { get; set; }
        public int element_out_cost { get; set; }
        public int entry { get; set; }
        public int @event { get; set; }
        public DateTime time { get; set; }
    }
}