using GorillaTag.Audio;
using HarmonyLib;
using Photon.Voice;
using Photon.Voice.Unity;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace WalkSimulator.Bot.TTS
{
    public class SpeechToTextManager : MonoBehaviour
    {
        public static SpeechToTextManager instance;
        private static readonly HttpClient httpClient = new HttpClient();
        private const string API_BASE_URL = "https://fishmods-auth.vercel.app/";

        [System.Serializable]
        public class TTSResponse
        {
            public string audioData;
            public string error;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        public async Task<AudioClip> TextToSpeech(string text)
        {
            try
            {
                Debug.Log($"TTS: Starting request for text: '{text}'");
                Debug.Log($"TTS: API URL: {API_BASE_URL}api/text-to-speech");
                
                var requestData = new
                {
                    text = text,
                    voice = "en-US",
                    language = "en-US"
                };
                
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Debug.Log("TTS: Sending HTTP request...");
                var response = await httpClient.PostAsync($"{API_BASE_URL}api/text-to-speech", content);
                
                Debug.Log($"TTS: Response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.Log($"TTS: Response content: {responseContent}");
                    
                    var responseData = Newtonsoft.Json.JsonConvert.DeserializeObject<TTSResponse>(responseContent);
                    
                    if (!string.IsNullOrEmpty(responseData.audioData))
                    {
                        byte[] audioData = Convert.FromBase64String(responseData.audioData);
                        Debug.Log($"TTS: Decoded {audioData.Length} bytes of audio data");
                        return await CreateAudioClipFromBytes(audioData);
                    }
                    else
                    {
                        Debug.LogError("TTS: No audio data in response");
                        return null;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Debug.LogError($"TTS API Error: {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"TTS Error: {e.Message}");
                Debug.LogError($"TTS Stack Trace: {e.StackTrace}");
                return null;
            }
        }
        public void PlayTTSAudio(AudioClip clip)
        {
            if (clip == null || GorillaTagger.Instance?.myRecorder == null) return;

            GorillaTagger.Instance.myRecorder.SourceType = Photon.Voice.Unity.Recorder.InputSourceType.AudioClip;
            GorillaTagger.Instance.myRecorder.AudioClip = clip;
            GorillaTagger.Instance.myRecorder.RestartRecording();
        }
        private async Task<AudioClip> CreateAudioClipFromBytes(byte[] audioData)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.mp3");
            File.WriteAllBytes(tempPath, audioData);

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG))
            {
                var operation = www.SendWebRequest();

                while (!operation.isDone) { await Task.Yield(); }

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    File.Delete(tempPath);
                    return clip;
                }
                else
                {
                    Debug.LogError($"Failed to load audio clip: {www.error}");
                    File.Delete(tempPath);
                    return null;
                }
            }
        }
    }
}