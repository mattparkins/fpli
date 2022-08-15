using System.Text.Json;

namespace fpli {
	public static class Fetcher {

		static DateTime _nextFetchStamp = DateTime.MinValue;
        static HttpClient _client = new HttpClient();
        
        public static float Callrate { get; set; } = 1f;   // Maximum calls per second

		public static async Task<T> FetchAndDeserialise<T>(string filename, string endpoint, int cacheExpiryInSeconds) {
			string text = await Fetcher.Fetch(filename, endpoint, cacheExpiryInSeconds);
			return JsonSerializer.Deserialize<T>(text, Utils.JSONConfig);
		}

		public static async Task<string> Fetch(string filename, string uri, int cacheExpiryInSeconds) {

            // Convert uri to a filename
            string json = "";

            // If the file exists, and isn't past its expiry then we can try loading a deserialising it
            if (File.Exists(filename)) {

                // Check age - has it expired?
                DateTime lastWriteTime = File.GetLastWriteTimeUtc(filename);
                DateTime expiry = lastWriteTime.AddSeconds(cacheExpiryInSeconds);

                if (expiry > DateTime.UtcNow) {
                
                    json = File.ReadAllText(filename);

                    // If the object isn't null then return it
                    if (json.Length >= 2) {
                        return json;
                    } 
                }   
            }

            // Either the file doesn't exist, or exists but has expired or didn't deserialize correctly,
            // download and save a fresh copy.  First ensure that the timestamp has expired.

            if (Callrate > 0) {

                DateTime now = DateTime.Now;
                if (now < _nextFetchStamp) {
                    TimeSpan delay = _nextFetchStamp - now;                    
                    await Task.Delay((int) delay.TotalMilliseconds);
                }
                
                _nextFetchStamp = now.AddSeconds(1f / Callrate);
            }
            
            // Download file, store in cache and return the body

            Console.WriteLine($"fetching {uri}, ");

            HttpRequestMessage request = new HttpRequestMessage {
                Method = HttpMethod.Get,
                RequestUri = new Uri(uri)
            };
            
            string body;

            try {

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                body = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(filename, body);
                return body;   

            } catch (Exception e) {

                Console.WriteLine($"error: {e.Message}");
				Environment.Exit(-1);
            }

            return null;
        }
	}
}