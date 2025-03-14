using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using Newtonsoft.Json;

namespace GorillaBot.WalkSimulator.Bot
{
    class Server
    {
        public static readonly HttpClient client = new HttpClient();
        private const string ApiBaseUrl = "http://localhost:3000/api";

        public Server()
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task ConvertTextToSpeechAsync(string text)
        {
            try
            {
                var healthResponse = await client.GetAsync($"{ApiBaseUrl}/health");
                if (!healthResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("API server is not available. Please make sure it's running.");
                    return;
                }

                var requestData = new
                {
                    text = text,
                    voice = "en-us",
                    language = "en-US"
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

                Console.WriteLine("Sending request to server...");
                var response = await client.PostAsync($"{ApiBaseUrl}/text-to-speech", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TextToSpeechResponse>(responseContent);

                    if (result?.AudioData != null)
                    {
                        byte[] audioBytes = Convert.FromBase64String(result.AudioData);
                        string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "output.mp3");
                        File.WriteAllBytes(outputPath, audioBytes);

                        Console.WriteLine($"Audio saved to: {outputPath}");
                        PlayAudio(outputPath);
                    }
                }
                else
                {
                    Console.WriteLine($"API request failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {errorContent}");
                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<ErrorResponse>(errorContent);
                        if (!string.IsNullOrEmpty(errorObj?.Suggestion))
                        {
                            Console.WriteLine($"Suggestion: {errorObj.Suggestion}");
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");

                if (ex is TaskCanceledException)
                {
                    Console.WriteLine("The request timed out. The server might be processing a large request or be unavailable.");
                }
            }
        }

        private void PlayAudio(string filePath)
        {
            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                using (var outputDevice = new WaveOutEvent())
                {
                    outputDevice.Init(audioFile);
                    outputDevice.Play();

                    Console.WriteLine("Playing audio... Press any key to stop.");
                    Console.ReadKey();

                    outputDevice.Stop();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing audio: {ex.Message}");
            }
        }
    }

    class TextToSpeechResponse
    {
        public string AudioData { get; set; }
    }

    class ErrorResponse
    {
        public string Error { get; set; }
        public string Details { get; set; }
        public string Suggestion { get; set; }
    }
}
