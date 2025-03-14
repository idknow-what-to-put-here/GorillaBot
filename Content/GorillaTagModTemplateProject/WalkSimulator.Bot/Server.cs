using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GorillaTagScripts;
using NAudio.Wave;
using Newtonsoft.Json;
using UnityEngine;

namespace GorillaBot.WalkSimulator.Bot
{
    class Server
    {
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
    public class DashboardServer
    {
        private const string ApiBaseUrl = "http://localhost:3000/api";
        private readonly HttpClient client;
        private readonly PlayerFollower follower;
        private ClientWebSocket wsClient;
        private CancellationTokenSource cts = new CancellationTokenSource();

        public DashboardServer(PlayerFollower follower)
        {
            this.follower = follower;
            this.client = Server.client;
        }

        public async Task Initialize()
        {
            await ConnectWebSocketAsync();
            Log("Path Destinations Server initialized with WebSocket sync.");
        }

        public void Shutdown()
        {
            cts.Cancel();
            if (wsClient != null)
            {
                wsClient.Abort();
                wsClient.Dispose();
            }
            Log("Path Destinations Server shut down.");
        }

        private async Task ConnectWebSocketAsync()
        {
            wsClient = new ClientWebSocket();
            try
            {
                await wsClient.ConnectAsync(new Uri("ws://localhost:3000"), CancellationToken.None);
                Log("WebSocket connected to dashboard server.");
                _ = Task.Run(ListenWebSocketMessages);
            }
            catch (Exception ex)
            {
                Log($"WebSocket connection error: {ex.Message}");
            }
        }

        private async Task ListenWebSocketMessages()
        {
            var buffer = new byte[4096];
            while (wsClient.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"WebSocket receive error: {ex.Message}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }

                var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    var msg = JsonConvert.DeserializeObject<DashboardMessage>(messageJson);
                    if (msg != null)
                    {
                        await HandleDashboardMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error parsing WebSocket message: {ex.Message}");
                }
            }
        }
        private async Task HandleDashboardMessage(DashboardMessage msg)
        {
            if (msg.type == "state_update")
            {
                try
                {
                    var botState = JsonConvert.DeserializeObject<BotState>(msg.data.ToString());
                    if (botState != null)
                    {
                        if (botState.followingPath != follower.followPathEnabled)
                        {
                            follower.followPathEnabled = botState.followingPath;
                            Log($"Path following {(botState.followingPath ? "started" : "stopped")} from dashboard");
                        }
                        if (botState.pathPositions != null && botState.pathPositions.Count > 0)
                        {
                            UpdatePathPositions(botState.pathPositions);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error updating state: {ex.Message}");
                }
            }
            else if (msg.type == "path_update")
            {
                try
                {
                    var data = msg.data as Newtonsoft.Json.Linq.JObject;
                    if (data != null)
                    {
                        var pathPositions = data["pathPositions"].ToObject<List<Vector3Json>>();
                        UpdatePathPositions(pathPositions);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error updating path positions: {ex.Message}");
                }
            }
            else if (msg.type == "save_path")
            {
                var data = msg.data as Newtonsoft.Json.Linq.JObject;
                if (data != null)
                {
                    var pathName = data["name"].ToString();

                    Log($"Path '{pathName}' saved successfully.");
                }
            }
            else if (msg.type == "load_path")
            {
                var data = msg.data as Newtonsoft.Json.Linq.JObject;
                if (data != null)
                {
                    var pathName = data["name"].ToString();

                    Log($"Path '{pathName}' loaded successfully.");
                }
            }
        }
        public async Task UpdateServerState()
        {
            try
            {
                var currentState = new BotState
                {
                    followingPath = follower.followPathEnabled,
                    pathPositions = ConvertPathPositionsToVectors(follower.lineRenderers.pathPositions)
                };

                var content = new StringContent(JsonConvert.SerializeObject(currentState), Encoding.UTF8, "application/json");
                await client.PostAsync($"{ApiBaseUrl}/botstate", content);
            }
            catch (Exception ex)
            {
                Log($"Error updating server state: {ex.Message}");
            }
        }
        private void UpdatePathPositions(List<Vector3Json> serverPositions)
        {
            if (serverPositions == null) return;

            var positions = new List<Vector3>();
            foreach (var pos in serverPositions)
            {
                positions.Add(new Vector3(pos.x, pos.y, pos.z));
            }

            if (!ArePathsEqual(follower.lineRenderers.pathPositions, positions))
            {
                follower.lineRenderers.pathPositions = positions;
                follower.lineRenderers.UpdatePathLineRenderer();
                Log($"Updated path positions from dashboard: {positions.Count} waypoints");
            }
        }
        private bool ArePathsEqual(List<Vector3> path1, List<Vector3> path2)
        {
            if (path1.Count != path2.Count) return false;
            for (int i = 0; i < path1.Count; i++)
            {
                if (Vector3.Distance(path1[i], path2[i]) > 0.01f) return false;
            }
            return true;
        }
        public async Task AddWaypoint(Vector3 position)
        {
            follower.lineRenderers.pathPositions.Add(position);
            follower.lineRenderers.UpdatePathLineRenderer();
            await UpdateServerState();
            Log($"Added waypoint at {position}");
        }
        public async Task RemoveLastWaypoint()
        {
            var positions = follower.lineRenderers.pathPositions;
            if (positions.Count > 0)
            {
                positions.RemoveAt(positions.Count - 1);
                follower.lineRenderers.UpdatePathLineRenderer();
                await UpdateServerState();
                Log("Removed last waypoint");
            }
        }
        public async Task ClearWaypoints()
        {
            follower.lineRenderers.pathPositions.Clear();
            follower.lineRenderers.UpdatePathLineRenderer();
            await UpdateServerState();
            Log("Cleared all waypoints");
        }
        public async Task StartPathFollowing()
        {
            if (follower.lineRenderers.pathPositions.Count > 0)
            {
                follower.followPathEnabled = true;
                await UpdateServerState();
                Log("Started path following");
            }
            else
            {
                Log("Cannot start path following: No waypoints defined");
            }
        }
        public async Task StopPathFollowing()
        {
            follower.StopPathing();
            await UpdateServerState();
            Log("Stopped path following");
        }
        private List<Vector3Json> ConvertPathPositionsToVectors(List<Vector3> positions)
        {
            var result = new List<Vector3Json>();
            foreach (var pos in positions)
            {
                result.Add(new Vector3Json { x = pos.x, y = pos.y, z = pos.z });
            }
            return result;
        }
        private void Log(string message)
        {
            follower.logger.LogInfo($"[DashboardServer] {message}");
            PlayerFollowerGUI.logMessages.Add($"[DashboardServer] {message}");
        }

        private class LoadPathResponse
        {
            public List<Vector3Json> pathPositions { get; set; }
        }
        public class BotState
        {
            public bool followingPath { get; set; }
            public List<Vector3Json> pathPositions { get; set; } = new List<Vector3Json>();
        }
        public class Vector3Json
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }
        }
        public class DashboardMessage
        {
            public string type { get; set; }
            public object data { get; set; }
        }
    }
}
