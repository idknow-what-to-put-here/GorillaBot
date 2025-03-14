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

namespace WalkSimulator.Bot
{
    public class TextToSpeechService
    {
        public static HttpClient _client;
        private const string ApiBaseUrl = "http://localhost:3000/api";
        private static readonly SemaphoreSlim _audioPlaybackSemaphore = new SemaphoreSlim(1, 1);

        public class TextToSpeechResponse
        {
            public string AudioData { get; set; }
        }

        public class ErrorResponse
        {
            public string Error { get; set; }
            public string Details { get; set; }
            public string Suggestion { get; set; }
        }

        public TextToSpeechService()
        {
            _client.Timeout = TimeSpan.FromSeconds(30);
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> ConvertTextToSpeechAsync(string text, string voice = "en-us", string language = "en-US")
        {
            if (string.IsNullOrWhiteSpace(text)) { throw new ArgumentException("Text cannot be empty", nameof(text)); }
            try
            {
                var healthResponse = await _client.GetAsync($"{ApiBaseUrl}/health");
                if (!healthResponse.IsSuccessStatusCode)
                {
                    LogMessage("API server is not available. Please make sure it's running.");
                    return false;
                }

                var requestData = new
                {
                    text,
                    voice,
                    language
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

                LogMessage("Sending request to text-to-speech service...");
                var response = await _client.PostAsync($"{ApiBaseUrl}/text-to-speech", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TextToSpeechResponse>(responseContent);

                    if (result?.AudioData != null)
                    {
                        byte[] audioBytes = Convert.FromBase64String(result.AudioData);
                        string outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"tts_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
                        await File.WriteAllBytesAsync(outputPath, audioBytes);

                        LogMessage($"Audio saved to: {outputPath}");
                        await PlayAudioAsync(outputPath);
                        return true;
                    }
                    else
                    {
                        LogMessage("Received empty audio data from API");
                        return false;
                    }
                }
                else
                {
                    LogMessage($"API request failed: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    LogMessage($"Error details: {errorContent}");

                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<ErrorResponse>(errorContent);
                        if (!string.IsNullOrEmpty(errorObj?.Suggestion))
                        {
                            LogMessage($"Suggestion: {errorObj.Suggestion}");
                        }
                    }
                    catch (Exception)
                    {

                    }

                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                LogMessage($"Network error: {ex.Message}");
                return false;
            }
            catch (TaskCanceledException)
            {
                LogMessage("The request timed out. The server might be processing a large request or be unavailable.");
                return false;
            }
            catch (Exception ex)
            {
                LogMessage($"Unexpected error: {ex.Message}");
                return false;
            }
        }
        private async Task PlayAudioAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogMessage($"Audio file not found: {filePath}");
                return;
            }

            try
            {
                await _audioPlaybackSemaphore.WaitAsync();

                using (var audioFile = new AudioFileReader(filePath))
                using (var outputDevice = new WaveOutEvent())
                {
                    var playbackCompletionSource = new TaskCompletionSource<bool>();

                    outputDevice.PlaybackStopped += (s, e) =>
                    {
                        playbackCompletionSource.TrySetResult(true);
                    };

                    outputDevice.Init(audioFile);
                    outputDevice.Play();

                    LogMessage("Playing audio... Press any key to stop.");

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(audioFile.TotalTime.TotalSeconds + 2)))
                    {
                        var keyPressTask = Task.Run(() => Console.ReadKey(true));
                        var completedTask = await Task.WhenAny(keyPressTask, playbackCompletionSource.Task, Task.Delay(-1, cts.Token));

                        outputDevice.Stop();
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error playing audio: {ex.Message}");
            }
            finally
            {
                _audioPlaybackSemaphore.Release();
            }
        }

        private void LogMessage(string message)
        {
            Console.WriteLine($"[TextToSpeech] {message}");
        }
    }
    public class DashboardServer : IDisposable
    {
        private const string ApiBaseUrl = "http://localhost:3000/api";
        private readonly HttpClient client;
        private readonly PlayerFollower follower;
        private ClientWebSocket wsClient;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool isDisposed = false;
        private Task webSocketListenerTask;
        private readonly object syncLock = new object();
        private bool isConnecting = false;

        public DashboardServer(PlayerFollower follower)
        {
            this.follower = follower ?? throw new ArgumentNullException(nameof(follower));
            this.client = TextToSpeechService._client ?? new HttpClient();
        }

        public async Task Initialize()
        {
            await ConnectWebSocketAsync();
            Log("Path Destinations Server initialized with WebSocket sync.");
        }
        public void Shutdown()
        {
            Dispose();
        }
        public void Dispose()
        {
            if (isDisposed) return;

            try
            {
                cts.Cancel();

                if (wsClient?.State == WebSocketState.Open)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None);
                        }
                        catch { }
                    }).Wait(1000);
                }

                wsClient?.Dispose();
                wsClient = null;

                Log("Path Destinations Server shut down.");
            }
            catch (Exception ex)
            {
                Log($"Error during shutdown: {ex.Message}");
            }
            finally
            {
                isDisposed = true;
            }
        }
        private async Task ConnectWebSocketAsync()
        {
            lock (syncLock)
            {
                if (isConnecting || isDisposed) return;
                isConnecting = true;
            }

            try
            {
                if (wsClient != null)
                {
                    try { wsClient.Dispose(); } catch { }
                }

                wsClient = new ClientWebSocket();
                wsClient.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await wsClient.ConnectAsync(new Uri("ws://localhost:3000"), CancellationToken.None);
                Log("WebSocket connected to dashboard server.");
                webSocketListenerTask = Task.Run(ListenWebSocketMessages);
            }
            catch (Exception ex)
            {
                Log($"WebSocket connection error: {ex.Message}");
                _ = Task.Delay(5000).ContinueWith(_ => ConnectWebSocketAsync());
            }
            finally
            {
                lock (syncLock)
                {
                    isConnecting = false;
                }
            }
        }
        private async Task ListenWebSocketMessages()
        {
            var buffer = new byte[8192];
            using var memoryStream = new System.IO.MemoryStream();

            while (wsClient?.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result = null;
                memoryStream.SetLength(0);

                try
                {
                    do
                    {
                        result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                            break;
                        }

                        await memoryStream.WriteAsync(buffer, 0, result.Count, cts.Token);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    memoryStream.Seek(0, System.IO.SeekOrigin.Begin);
                    var messageJson = Encoding.UTF8.GetString(memoryStream.ToArray());

                    try
                    {
                        var msg = JsonConvert.DeserializeObject<DashboardMessage>(messageJson);
                        if (msg != null)
                        {
                            await HandleDashboardMessage(msg, ActionSource.Server);
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log($"Error parsing WebSocket message: {ex.Message}");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    Log($"WebSocket error: {ex.Message}");
                    if (!cts.Token.IsCancellationRequested)
                    {
                        _ = Task.Delay(5000).ContinueWith(_ => ConnectWebSocketAsync());
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Unexpected error in WebSocket listener: {ex.Message}");
                    if (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(5000);
                        _ = ConnectWebSocketAsync();
                    }
                    break;
                }
            }
        }
        #region Handle
        private async Task HandleDashboardMessage(DashboardMessage msg, ActionSource source)
        {
            if (string.IsNullOrEmpty(msg?.type)) return;

            try
            {
                switch (msg.type)
                {
                    case "state_update":
                        await HandleStateUpdate(msg.data, source);
                       // await UpdateServerState();
                        break;
                    case "path_update":
                        await HandlePathUpdate(msg.data, source);
                        //await UpdateServerState();
                        break;
                    case "save_path":
                        await HandleSavePath(msg.data, source);
                        //await UpdateServerState();
                        break;
                    case "load_path":
                        await HandleLoadPath(msg.data, source);
                        //await UpdateServerState();
                        break;
                    default:
                        Log($"Unknown message type: {msg.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Error handling message '{msg.type}': {ex.Message}");
            }
        }
        private async Task HandleStateUpdate(object data, ActionSource source)
        {
            var botState = DeserializeData<BotState>(data);
            if (botState != null)
            {
                if (source == ActionSource.Server)
                {
                    if (botState.followingPath != follower.followPathEnabled)
                    {
                        follower.followPathEnabled = botState.followingPath;
                        Log($"Path following {(botState.followingPath ? "started" : "stopped")} from dashboard");
                    }

                    if (botState.pathPositions?.Count > 0)
                    {
                        await UpdatePathPositions(botState.pathPositions, source);
                    }
                }
                else
                {
                    await UpdateServerState();
                }
            }
        }
        private async Task HandlePathUpdate(object data, ActionSource source)
        {
            var pathData = DeserializeData<PathUpdateData>(data);
            if (pathData?.pathPositions != null)
            {
                if (source == ActionSource.Server)
                {
                    await UpdatePathPositions(pathData.pathPositions, source);
                }
                else
                {
                    await UpdateServerState();
                }
            }
        }
        private async Task HandleSavePath(object data, ActionSource source)
        {
            var saveData = DeserializeData<SavePathData>(data);
            if (saveData != null)
            {
                Log($"Path '{saveData.name}' saved successfully.");
            }
        }
        private async Task HandleLoadPath(object data, ActionSource source)
        {
            var loadData = DeserializeData<LoadPathData>(data);
            if (loadData != null)
            {
                Log($"Path '{loadData.name}' loaded successfully.");
            }
        }
        #endregion
        private T DeserializeData<T>(object data) where T : class
        {
            try
            {
                if (data is Newtonsoft.Json.Linq.JObject jObject)
                {
                    return jObject.ToObject<T>();
                }
                else
                {
                    return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(data));
                }
            }
            catch (Exception ex)
            {
                Log($"Error deserializing data to {typeof(T).Name}: {ex.Message}");
                return null;
            }
        }
        public async Task UpdateServerState()
        {
            if (isDisposed) return;

            try
            {
                var currentState = new BotState
                {
                    followingPath = follower.followPathEnabled,
                    pathPositions = ConvertPathPositionsToVectors(follower.lineRenderers.pathPositions)
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(currentState),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{ApiBaseUrl}/botstate", content);
                if (!response.IsSuccessStatusCode)
                {
                    Log($"Server returned error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log($"Error updating server state: {ex.Message}");
            }
        }
        private async Task UpdatePathPositions(List<Vector3Json> serverPositions, ActionSource source)
        {
            if (serverPositions == null || serverPositions.Count == 0) return;
            if (source == ActionSource.Server)
            {
                var positions = new List<Vector3>(serverPositions.Count);
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
            else
            {
                await UpdateServerState();
            }
        }
        private bool ArePathsEqual(List<Vector3> path1, List<Vector3> path2)
        {
            if (path1 == null || path2 == null || path1.Count != path2.Count)
                return false;

            for (int i = 0; i < path1.Count; i++)
            {
                if (Vector3.Distance(path1[i], path2[i]) > 0.01f)
                    return false;
            }

            return true;
        }
        public async Task AddWaypoint(Vector3 position, ActionSource source)
        {
            if (isDisposed) return;

            follower.lineRenderers.pathPositions.Add(position);
            follower.lineRenderers.UpdatePathLineRenderer();
            await UpdateServerState();
            Log($"Added waypoint at {position}");
        }
        public async Task RemoveLastWaypoint(ActionSource source)
        {
            if (isDisposed) return;

            if (source == ActionSource.Server)
            {
                var positions = follower.lineRenderers.pathPositions;
                if (positions.Count > 0)
                {
                    positions.RemoveAt(positions.Count - 1);
                    follower.lineRenderers.UpdatePathLineRenderer();
                    Log("Removed last waypoint");
                }
            }
            else
            {
                await UpdateServerState();
            }
        }
        public async Task ClearWaypoints(ActionSource source)
        {
            if (isDisposed) return;

            if (source == ActionSource.Server)
            {
                follower.lineRenderers.pathPositions.Clear();
                follower.lineRenderers.UpdatePathLineRenderer();
                Log("Cleared all waypoints");
            }
            else
            {
                await UpdateServerState();
            }
        }
        public async Task StartPathFollowing(ActionSource source)
        {
            if (isDisposed) return;

            if (source == ActionSource.Server)
            {
                if (follower.lineRenderers.pathPositions.Count > 0)
                {
                    follower.followPathEnabled = true;
                    Log("Started path following");
                }
                else
                {
                    Log("Cannot start path following: No waypoints defined");
                }
            }
            else
            {
                await UpdateServerState();
            }
        }
        public async Task StopPathFollowing(ActionSource source)
        {
            if (isDisposed) return;

            if (source == ActionSource.Server)
            {
                follower.StopPathing();
                Log("Stopped path following");
            }
            else
            {
                await UpdateServerState();
            }
        }
        private List<Vector3Json> ConvertPathPositionsToVectors(List<Vector3> positions)
        {
            if (positions == null) return new List<Vector3Json>();

            var result = new List<Vector3Json>(positions.Count);
            foreach (var pos in positions)
            {
                result.Add(new Vector3Json { x = pos.x, y = pos.y, z = pos.z });
            }

            return result;
        }
        private void Log(string message)
        {
            try
            {
                follower.logger.LogInfo($"[DashboardServer] {message}");
                PlayerFollowerGUI.logMessages.Add($"[DashboardServer] {message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DashboardServer] Logging error: {ex.Message}. Original message: {message}");
            }
        }

        private class PathUpdateData
        {
            public List<Vector3Json> pathPositions { get; set; }
        }

        private class SavePathData
        {
            public string name { get; set; }
        }

        private class LoadPathData
        {
            public string name { get; set; }
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