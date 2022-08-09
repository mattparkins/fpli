using System.Text.Json;

namespace fpli {
	public static class Fetcher {

		static DateTime _nextFetchStamp = DateTime.MinValue;
        static HttpClient _client = new HttpClient();
        
        public static float Callrate { get; set; } = 1f;   // Maximum calls per second

		public static async Task<T> FetchAndDeserialise<T>(string filename, string endpoint, int cacheExpiry) {
			string text = await Fetcher.Fetch(filename, endpoint);
			return JsonSerializer.Deserialize<T>(text, Utils.JSONConfig);
		}

		public static async Task<string> Fetch(string filename, string uri) {

            // Convert uri to a filename
            string json = "";

            Console.Write($"Loading {filename,-40} ");

            // If the file exists, and isn't past its expiry then we can try loading a deserialising it
            if (File.Exists(filename)) {

                Console.Write("found in cache, ");
                json = File.ReadAllText(filename);

                // If the object isn't null then return it
                if (json.Length >= 2) {
                    Console.WriteLine("ok!");
                    return json;
                } else {
                    Console.Write("file looks invalid, ");
                }
            }

            // Either the file doesn't exist, or exists but has expired or didn't deserialize correctly,
            // download and save a fresh copy.  First ensure that the timestamp has expired.

            if (Callrate > 0) {

                DateTime now = DateTime.Now;
                if (now < _nextFetchStamp) {
                    TimeSpan delay = _nextFetchStamp - now;
                    Console.Write($"awaiting, ");
                    
                    await Task.Delay((int) delay.TotalMilliseconds);
                }
                
                _nextFetchStamp = now.AddSeconds(1f / Callrate);
            }
            
            // Download file, store in cache and return the body

            Console.Write($"fetching {uri}, ");

            HttpRequestMessage request = new HttpRequestMessage {
                Method = HttpMethod.Get,
                RequestUri = new Uri(uri)
            };
            
            string body;

            try {

                HttpResponseMessage response = await _client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                body = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"writing {body.Length} bytes");

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