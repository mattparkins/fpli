using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace fpli {

    public static class Utils {

        public static readonly JsonSerializerOptions JSONConfig = new JsonSerializerOptions { 
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) 
            }
        };

        static Regex _regexAlphaNumeric = new Regex("[^a-zA-Z0-9]");

        public static T DeserializeFromFile<T>(string filePath) {
            string jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(jsonString, JSONConfig);
        }

        public static void SerializeToFile<T>(string filePath, T t) {
            string jsonString = JsonSerializer.Serialize(t, JSONConfig);
            File.WriteAllText(filePath, jsonString);
        }

        public static string SanitizeFilename(string name) {
            return _regexAlphaNumeric.Replace(name, "_");
        }

        public static int DaysAsSeconds(float days) {
            return (int) (days * 24 * 60 * 60);
        }

        public static int HoursAsSeconds(float hours) {
            return (int) (hours * 60 * 60);
        }

        public static bool PathIsOutsideFolderStructure(string filePath) {
            var fullRoot = Path.GetFullPath(".");
            var fullPathToVerify = Path.GetFullPath(filePath);
            return !fullPathToVerify.StartsWith(fullRoot);
        }

        public static double SafeDivide(double top, double dividedBy) {
            if (dividedBy == 0.0) {
                return 0;
            } else {
                return (double) top / dividedBy;
            }
        }

        // Count the number of digits in an int
        public static int NumDigits(this int n) => n == 0 ? 1 : (n > 0 ? 1 : 2) + (int) Math.Log10(Math.Abs((double) n));


        // Helper function to execute non-blocking async tasks where we don't care about the return
        // Usage: myAsyncFunction().RunConcurrent();
        public static void ExecuteConcurrently(this Task task) { 
            if (task.Status == TaskStatus.Created) {
                task.Start(); 
            }
        } 

        // Hamming weight - count set bits
        public static int HammingWeight(int v) {
            v = v - ((v>>1) & 0x55555555);
            v = (v & 0x33333333) + ((v>>2) & 0x33333333);
            return ((v + (v>>4) & 0xF0F0F0F) * 0x1010101) >> 24;
        }

        // Standardise Name - Capitalize first letter of each word
        public static string StandardiseName(string name) {
            // Use the TextInfo class for culture-specific casing
            TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;

            // Split the name into words, standardize each word and join them back
            string[] words = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                // Capitalize the first letter, and make the rest lowercase
                words[i] = textInfo.ToTitleCase(words[i].ToLower());
            }

            // Join the words back into a single string
            return string.Join(" ", words);
        }
}
}