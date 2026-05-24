using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;
using Subsystems;
using UnityEngine.SceneManagement;
using SimpleJSON;
using ChatGPTUtility;
using wAIfuBackend;

namespace AI2U_UltimateFix
{
    [BepInPlugin("com.omni.ai2ufix", "AI2U Ultimate Protocol", "8.0.0")]
    public class UltimateFixPlugin : BaseUnityPlugin
    {
        // ── Config from JSON ──
        private static string cfgBaseURL   = "https://openrouter.ai/api/v1/chat/completions";
        private static string cfgAPIKey    = "";
        private static string cfgModel     = "openai/gpt-4o-mini";
        private static string cfgSystemPrompt = "";
        private static string cfgPostHistoryPrompt = "";
        private static float  cfgTemperature = 0.7f;
        private static float  cfgTopP        = 0.95f;
        private static int    cfgTopK        = 0;
        private static int    cfgMaxTokens   = 800;
        private static float  cfgFreqPenalty = 0.03f;
        private static float  cfgPresPenalty = 0.03f;

        public static bool   cfgTTSEnable   = false;
        public static string cfgTTSProvider = "Azure";
        public static string cfgTTSBaseURL  = "";
        public static string cfgTTSAPIKey   = "";
        public static string cfgTTSModel    = "en-US-JaneNeural";
        public static string cfgTTSRegion   = "eastus";

        public static string pluginDir;
        private static string configPath;
        private static string logPath;

        public static void LogDebug(string msg)
        {
            try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n"); }
            catch { }
        }

        public static string EscapeJsonString(string s)
        {
            if (s == null) return "";
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private void LoadJsonConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    LogDebug("Config JSON not found at: " + configPath + ". Using defaults. Please run AI2U_Configurator to set up.");
                    return;
                }

                string jsonText = File.ReadAllText(configPath);
                JSONNode cfg = JSON.Parse(jsonText);
                if (cfg == null) { LogDebug("Failed to parse config JSON."); return; }

                if (cfg["base_url"] != null)          cfgBaseURL          = cfg["base_url"].Value;
                if (cfg["api_key"] != null)            cfgAPIKey           = cfg["api_key"].Value;
                if (cfg["model"] != null)              cfgModel            = cfg["model"].Value;
                if (cfg["system_prompt"] != null)      cfgSystemPrompt     = cfg["system_prompt"].Value;
                if (cfg["post_history_prompt"] != null) cfgPostHistoryPrompt = cfg["post_history_prompt"].Value;
                if (cfg["temperature"] != null)        cfgTemperature      = cfg["temperature"].AsFloat;
                if (cfg["top_p"] != null)              cfgTopP             = cfg["top_p"].AsFloat;
                if (cfg["top_k"] != null)              cfgTopK             = cfg["top_k"].AsInt;
                if (cfg["max_tokens"] != null)         cfgMaxTokens        = cfg["max_tokens"].AsInt;
                if (cfg["frequency_penalty"] != null)  cfgFreqPenalty      = cfg["frequency_penalty"].AsFloat;
                if (cfg["presence_penalty"] != null)   cfgPresPenalty      = cfg["presence_penalty"].AsFloat;

                if (cfg["tts_enable"] != null)         cfgTTSEnable        = cfg["tts_enable"].AsBool;
                if (cfg["tts_provider"] != null)       cfgTTSProvider      = cfg["tts_provider"].Value;
                if (cfg["tts_base_url"] != null)       cfgTTSBaseURL       = cfg["tts_base_url"].Value;
                if (cfg["tts_api_key"] != null)        cfgTTSAPIKey        = cfg["tts_api_key"].Value;
                if (cfg["tts_model"] != null)          cfgTTSModel         = cfg["tts_model"].Value;
                if (cfg["tts_region"] != null)         cfgTTSRegion        = cfg["tts_region"].Value;

                LogDebug("Config loaded: URL=" + cfgBaseURL + " Model=" + cfgModel + " Temp=" + cfgTemperature);
                if (string.IsNullOrEmpty(cfgAPIKey))
                    LogDebug("WARNING: API Key is empty! Please set it in the Configurator.");
            }
            catch (Exception ex)
            {
                LogDebug("Error loading config: " + ex.Message);
            }
        }

        private void Awake()
        {
            pluginDir  = Path.GetDirectoryName(typeof(UltimateFixPlugin).Assembly.Location);
            string bepinExDir = Directory.GetParent(pluginDir).FullName;
            configPath = Path.Combine(bepinExDir, "config", "AI2U_Config.json");
            logPath    = Path.Combine(bepinExDir, "UltimateFix_Debug.txt");

            File.WriteAllText(logPath, "=== AI2U Ultimate Protocol v8 Started ===\n");
            LogDebug("Plugin dir: " + pluginDir);
            LogDebug("Config path: " + configPath);

            LoadJsonConfig();

            Communicator.AIModel = ChatGPTConversation.Model.ChatGPTAzure;
            LogDebug("AIModel = ChatGPTAzure");

            GameObject hunter = new GameObject("OmniUIHunter");
            DontDestroyOnLoad(hunter);
            hunter.AddComponent<UIHunter>();

            GameObject ttsManagerObj = new GameObject("AI2UTTSManager");
            DontDestroyOnLoad(ttsManagerObj);
            ttsManagerObj.AddComponent<TTSManager>();

            var harmony = new Harmony("com.omni.ai2ufix");
            
            // Manual patch for internal class wAIfuBackend.Prefs
            try
            {
                var prefsType = AccessTools.TypeByName("wAIfuBackend.Prefs");
                if (prefsType != null)
                {
                    var getMethod = AccessTools.PropertyGetter(prefsType, "PlayFabId");
                    var prefix = new HarmonyMethod(typeof(UltimateFixPlugin), "PrefixPlayFabId");
                    harmony.Patch(getMethod, prefix: prefix);
                    LogDebug("Manually patched wAIfuBackend.Prefs.PlayFabId getter");
                }
            }
            catch (Exception ex)
            {
                LogDebug("Failed to patch wAIfuBackend.Prefs: " + ex.Message);
            }

            // Infinite Gems for offline shop
            PlayerPrefs.SetInt("gems", 999999999);
            LogDebug("Offline Shop: Granted 999,999,999 Gems!");

            harmony.PatchAll(typeof(UltimateFixPlugin));
            LogDebug("Harmony Patches Applied!");
        }

        // ─────────────────────────────────────────────────────────────────────
        // LOGIN BYPASS
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(LoginLoadingManager), "StartLoading")]
        [HarmonyPrefix]
        public static bool BypassLoading()
        {
            LogDebug("Bypassing Loading -> MenuState");
            SceneManager.LoadScene("MenuState");
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 1: Infinite TK tokens
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(wAIfuPlayFab), "ConsumeToken")]
        [HarmonyPrefix]
        public static bool InfiniteTokens(ref bool __result)
        {
            __result = true;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 2: Redirect URL to OpenRouter
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(ChatGPTConversation), "SendToChatGPT",
            new Type[] { typeof(string), typeof(Action<string, int>) })]
        [HarmonyPrefix]
        public static void RedirectRequest2(ChatGPTConversation __instance)
        {
            try
            {
                var uriField = AccessTools.Field(typeof(ChatGPTConversation), "_uri");
                if (uriField != null) uriField.SetValue(__instance, new Uri(cfgBaseURL));
                LogDebug("Redirected _uri -> " + cfgBaseURL);
            }
            catch (Exception ex) { LogDebug("Error RedirectRequest2: " + ex.Message); }
        }

        [HarmonyPatch(typeof(ChatGPTConversation), "SendToChatGPT",
            new Type[] { typeof(string), typeof(Action<string, int>), typeof(string), typeof(EnvisionType) })]
        [HarmonyPrefix]
        public static void RedirectRequest4(ChatGPTConversation __instance)
        {
            try
            {
                var uriField = AccessTools.Field(typeof(ChatGPTConversation), "_uriEnvision");
                if (uriField != null) uriField.SetValue(__instance, new Uri(cfgBaseURL));
            }
            catch (Exception ex) { LogDebug("Error RedirectRequest4: " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 2B: Clean null headers + inject auth AFTER UpdateRequestHeaders
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(ChatGPTConversation), "UpdateRequestHeaders")]
        [HarmonyPostfix]
        public static void FixNullHeaders(ChatGPTConversation __instance)
        {
            try
            {
                var headersField = AccessTools.Field(typeof(ChatGPTConversation), "_reqHeaders");
                if (headersField == null) return;
                var headers = headersField.GetValue(__instance) as Dictionary<string, string>;
                if (headers == null) return;

                var keys = new List<string>(headers.Keys);
                foreach (string key in keys)
                    if (headers[key] == null) headers[key] = "";

                headers["Authorization"] = "Bearer " + cfgAPIKey;
                headers["Content-Type"]  = "application/json";
            }
            catch (Exception ex) { LogDebug("Error FixNullHeaders: " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 2C: Safe SetHeaders (skip null values)
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(Requests), "SetHeaders")]
        [HarmonyPrefix]
        public static bool SafeSetHeaders(ref UnityWebRequest req, Dictionary<string, string> headers)
        {
            try
            {
                if (headers == null) return false;
                foreach (var kvp in headers)
                    if (kvp.Key != null && kvp.Value != null)
                        req.SetRequestHeader(kvp.Key, kvp.Value);
                return false;
            }
            catch (Exception ex) { LogDebug("Error SafeSetHeaders: " + ex.Message); return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 3: Inject model, system prompt, post-history prompt, remove stop
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(UnityWebRequest), "SendWebRequest")]
        [HarmonyPrefix]
        public static void TransformPayload(UnityWebRequest __instance)
        {
            try
            {
                if (__instance == null) return;
                string url = __instance.url;
                LogDebug("SendWebRequest: " + url);

                if (!url.Contains("openrouter.ai") && !url.Contains(cfgBaseURL.Replace("https://", "").Split('/')[0]))
                    return;

                if (__instance.uploadHandler == null) return;
                byte[] data = __instance.uploadHandler.data;
                if (data == null || data.Length == 0) return;

                string json = Encoding.UTF8.GetString(data);

                if (!json.Contains("\"messages\"")) return;

                LogDebug("Original payload length: " + json.Length);

                // Parse JSON to modify it
                JSONNode root = JSON.Parse(json);
                if (root == null) return;

                // Inject model
                root["model"] = cfgModel;

                // Override parameters from config
                root["temperature"]      = cfgTemperature;
                root["max_tokens"]        = cfgMaxTokens;
                root["top_p"]             = cfgTopP;
                root["frequency_penalty"] = cfgFreqPenalty;
                root["presence_penalty"]  = cfgPresPenalty;

                if (cfgTopK > 0) root["top_k"] = cfgTopK;

                // Remove empty stop field
                if (root["stop"] != null)
                {
                    string stopVal = root["stop"].Value;
                    if (string.IsNullOrEmpty(stopVal))
                        root.Remove("stop");
                }

                // Inject system prompt if configured
                JSONArray messages = root["messages"].AsArray;
                if (messages != null && messages.Count > 0 && !string.IsNullOrEmpty(cfgSystemPrompt))
                {
                    // Check if first message is system role
                    if (messages[0]["role"].Value == "system")
                    {
                        messages[0]["content"] = cfgSystemPrompt;
                        LogDebug("Injected system prompt into existing system message.");
                    }
                    else
                    {
                        // Prepend system message
                        JSONNode sysMsg = JSON.Parse("{}");
                        sysMsg["role"]    = "system";
                        sysMsg["content"] = cfgSystemPrompt;
                        // Shift all messages and insert at 0
                        JSONArray newMessages = new JSONArray();
                        newMessages.Add(sysMsg);
                        for (int i = 0; i < messages.Count; i++)
                            newMessages.Add(messages[i]);
                        root["messages"] = newMessages;
                        LogDebug("Prepended system prompt.");
                    }
                }

                // Inject post-history prompt (append as last user message)
                if (!string.IsNullOrEmpty(cfgPostHistoryPrompt))
                {
                    JSONArray msgs = root["messages"].AsArray;
                    JSONNode postMsg = JSON.Parse("{}");
                    postMsg["role"]    = "system";
                    postMsg["content"] = cfgPostHistoryPrompt;
                    msgs.Add(postMsg);
                    LogDebug("Appended post-history prompt.");
                }

                json = root.ToString();
                LogDebug("Final payload length: " + json.Length);

                __instance.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex) { LogDebug("Error TransformPayload: " + ex.Message + "\n" + ex.StackTrace); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 4: Transform OpenRouter response -> game JSON
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(DownloadHandler), "text", MethodType.Getter)]
        [HarmonyPostfix]
        public static void TransformResponse(ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result)) return;
                if (!__result.Contains("\"choices\"") || !__result.Contains("\"content\"")) return;

                LogDebug("TransformResponse: Intercepted.");

                JSONNode resp = JSON.Parse(__result);
                if (resp == null || resp["choices"] == null || resp["choices"].Count == 0) return;

                string content = resp["choices"][0]["message"]["content"].Value;
                int compTokens  = resp["usage"] != null ? resp["usage"]["completion_tokens"].AsInt : 50;
                int totalTokens = resp["usage"] != null ? resp["usage"]["total_tokens"].AsInt : 100;

                LogDebug("Raw AI content: " + content);

                // Strip markdown code fences
                Match mdMatch = Regex.Match(content, @"```(?:json)?\s*([\s\S]+?)\s*```");
                if (mdMatch.Success) content = mdMatch.Groups[1].Value.Trim();

                string gameJson = null;

                // Try to parse JSON from content
                int firstBrace = content.IndexOf('{');
                int lastBrace  = content.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    string candidate = content.Substring(firstBrace, lastBrace - firstBrace + 1);
                    try
                    {
                        JSONNode parsed = JSON.Parse(candidate);
                        if (parsed != null)
                        {
                            if (parsed["npc_reactions"] != null)
                            {
                                parsed["completion"] = compTokens;
                                parsed["total"]      = totalTokens;
                                gameJson = parsed.ToString();
                                LogDebug("Found full game JSON format.");
                            }
                            else if (parsed["npc_reply_to_player"] != null)
                            {
                                JSONNode wrapper = JSON.Parse("{}");
                                wrapper["npc_reactions"] = parsed;
                                wrapper["completion"]    = compTokens;
                                wrapper["total"]         = totalTokens;
                                gameJson = wrapper.ToString();
                                LogDebug("Found inner npc_reactions JSON.");
                            }
                        }
                    }
                    catch { }
                }

                // Plain text fallback
                if (gameJson == null)
                {
                    LogDebug("AI returned plain text. Wrapping.");
                    string rawContent = resp["choices"][0]["message"]["content"].Value;
                    string escaped = EscapeJsonString(rawContent);

                    gameJson = "{\"npc_reactions\":{"
                        + "\"npc_reply_to_player\":\"" + escaped + "\","
                        + "\"npc_body_animation\":\"idle\","
                        + "\"npc_face_expression\":\"smile\","
                        + "\"npc_emotion_type\":\"happy\","
                        + "\"npc_emotion_score\":\"5\","
                        + "\"angry_level\":\"0\","
                        + "\"favorability_change\":\"0\","
                        + "\"npc_action\":\"standing\","
                        + "\"npc_target_location\":\"\","
                        + "\"giving_to_player\":\"\","
                        + "\"character\":0"
                        + "},\"completion\":" + compTokens
                        + ",\"total\":" + totalTokens + "}";
                }

                __result = gameJson;
                LogDebug("Game JSON ready.");
            }
            catch (Exception ex)
            {
                LogDebug("Error TransformResponse: " + ex.Message + "\n" + ex.StackTrace);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Keep AIModel = ChatGPTAzure
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(Communicator), "Start")]
        [HarmonyPrefix]
        public static void ForceAzureOnStart()
        {
            Communicator.AIModel = ChatGPTConversation.Model.ChatGPTAzure;
        }

        [HarmonyPatch(typeof(ChatGPTConversation), "Init")]
        [HarmonyPostfix]
        public static void ForceAzureAfterInit(ChatGPTConversation __instance)
        {
            try
            {
                var modelField = AccessTools.Field(typeof(ChatGPTConversation), "_model");
                if (modelField != null)
                    modelField.SetValue(__instance, ChatGPTConversation.Model.ChatGPTAzure);
            }
            catch (Exception ex) { LogDebug("Error ForceAzureAfterInit: " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 4: Force Voice TTS locally since OpenRouter has no audio
        // Patches AzureVoiceManager.Speak → uses RT-Voice (Windows SAPI)
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(AzureVoiceManager), "Speak")]
        [HarmonyPrefix]
        public static bool PrefixVoiceSpeak(AzureVoiceManager __instance, JSONNode jsonVoice, Character characterId, float delayPlayTime)
        {
            try
            {
                // If jsonVoice has valid base64 audio data, let original handle it
                if (jsonVoice != null && !string.IsNullOrEmpty(jsonVoice.Value) && jsonVoice.Value.Length > 100)
                    return true;

                LogDebug("VoiceSpeak: No server audio, using local TTS...");

                // Get reply text from Communicator.currentJSON
                var communicator = UnityEngine.Object.FindObjectOfType<Communicator>();
                if (communicator == null) { LogDebug("VoiceSpeak: Communicator not found"); return false; }

                var currentJSONField = AccessTools.Field(typeof(Communicator), "currentJSON");
                if (currentJSONField == null) { LogDebug("VoiceSpeak: currentJSON field not found"); return false; }

                JSONNode currentJSON = currentJSONField.GetValue(communicator) as JSONNode;
                if (currentJSON == null) { LogDebug("VoiceSpeak: currentJSON is null"); return false; }

                string replyText = currentJSON["npc_reply_to_player"].Value;
                if (string.IsNullOrEmpty(replyText)) { LogDebug("VoiceSpeak: replyText is empty"); return false; }

                // Get AudioSource from AzureVoiceManager.VoiceMap
                var voiceMap = __instance.VoiceMap;
                if (voiceMap == null || voiceMap.Count == 0)
                {
                    LogDebug("VoiceSpeak: VoiceMap is null or empty");
                    return false;
                }
                
                AudioSource audioSource = null;
                if (voiceMap.ContainsKey(characterId))
                {
                    audioSource = voiceMap[characterId];
                }
                else if (voiceMap.ContainsKey(__instance.mainCharacter))
                {
                    audioSource = voiceMap[__instance.mainCharacter];
                    LogDebug("VoiceSpeak: Used mainCharacter fallback: " + __instance.mainCharacter);
                }
                else
                {
                    // Last resort: grab any available AudioSource
                    foreach (var kvp in voiceMap)
                    {
                        if (kvp.Value != null) { audioSource = kvp.Value; break; }
                    }
                    LogDebug("VoiceSpeak: Used first available AudioSource");
                }
                
                if (audioSource == null)
                {
                    LogDebug("VoiceSpeak: No AudioSource found at all. VoiceMap keys: " + string.Join(", ", new List<Character>(voiceMap.Keys).ConvertAll(k => k.ToString()).ToArray()));
                    return false;
                }

                if (cfgTTSEnable)
                {
                    LogDebug("VoiceSpeak: Custom TTS is ENABLED. Provider: " + cfgTTSProvider);
                    
                    TTSManager mgr = TTSManager.Instance;
                    if (mgr == null)
                    {
                        mgr = UnityEngine.Object.FindObjectOfType<TTSManager>();
                        LogDebug("VoiceSpeak: Instance was null, FindObjectOfType found: " + (mgr != null));
                    }
                    if (mgr == null)
                    {
                        // Last resort: create it on the fly
                        LogDebug("VoiceSpeak: Creating TTSManager on the fly...");
                        GameObject go = new GameObject("AI2UTTSManager_Runtime");
                        UnityEngine.Object.DontDestroyOnLoad(go);
                        mgr = go.AddComponent<TTSManager>();
                    }
                    
                    mgr.StartTTSCoroutine(replyText, audioSource);
                    LogDebug("VoiceSpeak: Coroutine started!");
                    return false; // Skip original
                }

                LogDebug("VoiceSpeak: Custom TTS is disabled. Skipping audio generation.");
                return false; // Skip original
            }
            catch (Exception ex)
            {
                LogDebug("Error VoiceSpeak: " + ex.Message
                    + (ex.InnerException != null ? "\nInner: " + ex.InnerException.Message : "")
                    + "\n" + ex.StackTrace);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // OFFLINE SAVE/LOAD SYSTEM
        // ─────────────────────────────────────────────────────────────────────

        public static bool PrefixPlayFabId(ref string __result)
        {
            __result = "OFFLINE_USER";
            return false;
        }

        [HarmonyPatch(typeof(PlayerDataSubsystem), "LoadAllData")]
        [HarmonyPrefix]
        public static bool PrefixLoadAllData(PlayerDataSubsystem __instance, Action loadSuccess)
        {
            try
            {
                string text = "savedata/global.yags";
                string text2 = "OFFLINE_USER_savedData";
                Dictionary<string, string> savedData = null;
                if (ES3.FileExists(text))
                {
                    savedData = ES3.Load<Dictionary<string, string>>(text2, text);
                    LogDebug("Offline Load: Successfully loaded " + savedData.Count + " key(s) from local storage.");
                }
                if (savedData == null) savedData = new Dictionary<string, string>();

                AccessTools.Field(typeof(PlayerDataSubsystem), "SavedData").SetValue(__instance, savedData);
                AccessTools.Field(typeof(PlayerDataSubsystem), "UploadedData").SetValue(__instance, new Dictionary<string, string>());
                
                // SaveGlobalDataToLocals
                AccessTools.Method(typeof(PlayerDataSubsystem), "SaveGlobalDataToLocals").Invoke(__instance, null);
                
                // HandleGeneralData
                AccessTools.Method(typeof(PlayerDataSubsystem), "HandleGeneralData").Invoke(__instance, null);
                
                GameRuntime.GetSubsystem<AchievementSubsystem>().HandleAchievementData();
                LevelData_HubWorld.LoadLevelData();
                EventManager.NPCDataUpdated();
                
                if (loadSuccess != null) loadSuccess();
            }
            catch (Exception ex)
            {
                LogDebug("Error in PrefixLoadAllData: " + ex.ToString());
            }
            return false; // Skip original
        }

        [HarmonyPatch(typeof(PlayerDataSubsystem), "SaveAndUpload")]
        [HarmonyPrefix]
        public static bool PrefixSaveAndUpload(PlayerDataSubsystem __instance)
        {
            try
            {
                AccessTools.Method(typeof(PlayerDataSubsystem), "SaveGlobalDataToLocals").Invoke(__instance, null);
                GameRuntime.GetSubsystem<AchievementSubsystem>().SaveAchievementRelated();
                LogDebug("Offline Save: Local save complete, cloud upload skipped.");
            }
            catch (Exception ex)
            {
                LogDebug("Error in PrefixSaveAndUpload: " + ex.ToString());
            }
            return false; // Skip original
        }

        [HarmonyPatch(typeof(PlayerDataSubsystem), "Save")]
        [HarmonyPrefix]
        public static bool PrefixSave()
        {
            return false; // Skip original
        }
        
        [HarmonyPatch(typeof(PlayerDataSubsystem), "UploadData")]
        [HarmonyPrefix]
        public static bool PrefixUploadData()
        {
            return false; // Skip original
        }

        [HarmonyPatch(typeof(PlayerDataSubsystem), "UploadDataAfterCheck")]
        [HarmonyPrefix]
        public static bool PrefixUploadDataAfterCheck()
        {
            return false; // Skip original
        }

        [HarmonyPatch(typeof(Shop), "PurchaseItemFromShop")]
        [HarmonyPrefix]
        public static bool PrefixPurchaseItemFromShop(Shop __instance, Item _item, int count)
        {
            try
            {
                LogDebug("Offline Shop: Bypassing PlayFab purchase...");
                AccessTools.Field(typeof(Shop), "m_purchasingItem").SetValue(__instance, _item);
                AccessTools.Field(typeof(Shop), "m_purchasingItemCount").SetValue(__instance, count);
                
                var loading = AccessTools.Field(typeof(Shop), "loadingPurchaseContainer").GetValue(__instance) as GameObject;
                if (loading != null) loading.SetActive(true);
                
                AccessTools.Field(typeof(Shop), "success").SetValue(__instance, false);
                AccessTools.Field(typeof(Shop), "fail").SetValue(__instance, false);

                // Save to OfflineItems in ES3
                List<string> offlineItems = new List<string>();
                if (ES3.KeyExists("OfflineItems", "savedata/global.yags"))
                {
                    offlineItems = ES3.Load<List<string>>("OfflineItems", "savedata/global.yags");
                }
                LogDebug("Offline Shop: Purchasing item " + _item.name + " x" + count);

                // Add to our offline items list
                for (int i = 0; i < count; i++)
                {
                    offlineItems.Add(_item.name);
                }

                // Save immediately
                ES3.Save("OfflineItems", offlineItems, "savedata/global.yags");

                // Original logic: add to inventory
                Inventory inventory = Inventory.FindInventory("PlayerInventory");
                if (_item.IsNPCTag)
                {
                    if (inventory != null) inventory.AddItem(_item, inventory.InventoryName, false);
                    
                    LevelManager_HubWorld hub = LevelManager_HubWorld.Instance;
                    if (hub != null)
                    {
                        var tagsField = typeof(LevelManager_HubWorld).GetField("m_NPCPersonalityTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (tagsField != null)
                        {
                            NPCPersonalityTags tagsObj = tagsField.GetValue(hub) as NPCPersonalityTags;
                            if (tagsObj != null)
                            {
                                string tType = _item.m_TagType.ToString();
                                if (tType == "Personality" && !tagsObj.personality.Contains(_item.EnumIndex)) tagsObj.personality.Add(_item.EnumIndex);
                                else if (tType == "Hobby" && !tagsObj.hobby.Contains(_item.EnumIndex)) tagsObj.hobby.Add(_item.EnumIndex);
                                else if (tType == "SpeakingTone" && !tagsObj.speakingTone.Contains(_item.EnumIndex)) tagsObj.speakingTone.Add(_item.EnumIndex);
                            }
                        }
                    }
                }

                // Call PurchaseItem directly with null
                AccessTools.Method(typeof(Shop), "PurchaseItem").Invoke(__instance, new object[] { null });

                // Start Coroutine
                var coroutine = AccessTools.Method(typeof(Shop), "UpdateConfirmPage").Invoke(__instance, null) as System.Collections.IEnumerator;
                if (coroutine != null)
                {
                    __instance.StartCoroutine(coroutine);
                }
            }
            catch (Exception ex)
            {
                LogDebug("Error in PrefixPurchaseItemFromShop: " + ex.ToString());
            }
            return false; // Skip original
        }

        [HarmonyPatch(typeof(LevelManager_HubWorld), "LoadPlayfabInventory")]
        [HarmonyPrefix]
        public static bool PrefixLoadPlayfabInventory(LevelManager_HubWorld __instance)
        {
            try
            {
                LogDebug("Offline Shop: Loading inventory from ES3...");
                List<string> offlineItems = new List<string>();
                if (ES3.KeyExists("OfflineItems", "savedata/global.yags"))
                {
                    offlineItems = ES3.Load<List<string>>("OfflineItems", "savedata/global.yags");
                }
                LogDebug("Offline Shop: Found " + offlineItems.Count + " items in save.");
                
                var rewardDictField = AccessTools.Field(typeof(LevelManager_HubWorld), "rewardItemDictionary");
                var rewardDict = rewardDictField != null ? rewardDictField.GetValue(__instance) as ItemIDList : null;
                
                if (rewardDict == null) LogDebug("Offline Shop: rewardItemDictionary is NULL!");
                else LogDebug("Offline Shop: rewardItemDictionary has " + rewardDict.Items.Count + " items.");

                if (rewardDict != null)
                {
                    Inventory inventory = Inventory.FindInventory("PlayerInventory");
                    if (inventory == null) LogDebug("Offline Shop: PlayerInventory is NULL!");
                    
                    foreach (string itemId in offlineItems)
                    {
                        Item item = null;
                        foreach (var kvp in rewardDict.Items)
                        {
                            if (kvp.Key.Equals(itemId, StringComparison.OrdinalIgnoreCase) ||
                                (kvp.Value != null && kvp.Value.name.Equals(itemId, StringComparison.OrdinalIgnoreCase)) ||
                                (kvp.Value != null && kvp.Value.ItemID != null && kvp.Value.ItemID.Equals(itemId, StringComparison.OrdinalIgnoreCase)))
                            {
                                item = kvp.Value;
                                break;
                            }
                        }

                        if (item != null)
                        {
                            LogDebug("Offline Shop: Granting item " + item.name);
                            if (item.IsNPCTag)
                            {
                                if (inventory != null) inventory.AddItem(item, inventory.InventoryName, false);
                                
                                LevelManager_HubWorld hub = LevelManager_HubWorld.Instance;
                                if (hub != null)
                                {
                                    var tagsField = typeof(LevelManager_HubWorld).GetField("m_NPCPersonalityTags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    if (tagsField != null)
                                    {
                                        NPCPersonalityTags tagsObj = tagsField.GetValue(hub) as NPCPersonalityTags;
                                        if (tagsObj != null)
                                        {
                                            string tType = item.m_TagType.ToString();
                                            if (tType == "Personality" && !tagsObj.personality.Contains(item.EnumIndex)) tagsObj.personality.Add(item.EnumIndex);
                                            else if (tType == "Hobby" && !tagsObj.hobby.Contains(item.EnumIndex)) tagsObj.hobby.Add(item.EnumIndex);
                                            else if (tType == "SpeakingTone" && !tagsObj.speakingTone.Contains(item.EnumIndex)) tagsObj.speakingTone.Add(item.EnumIndex);
                                        }
                                    }
                                }
                            }
                            else if (item.IsNPCAppearance)
                            {
                                if (item.NPCAppearance is NPCAppearance_L1) Appearance.Instance.Appearances_L1.Add(item.NPCAppearance);
                                else if (item.NPCAppearance is NPCAppearance_L2) Appearance.Instance.Appearances_L2.Add(item.NPCAppearance);
                                else if (item.NPCAppearance is NPCAppearance_L3) Appearance.Instance.Appearances_L3.Add(item.NPCAppearance);
                                else if (item.NPCAppearance is NPCAppearance_L4) Appearance.Instance.Appearances_L4.Add(item.NPCAppearance);
                            }
                            else
                            {
                                if (inventory != null) inventory.AddItem(item, inventory.InventoryName, false);
                            }
                        }
                        else
                        {
                            LogDebug("Offline Shop: Item " + itemId + " not found in dictionary!");
                        }
                    }
                    
                    var toggleMethod = AccessTools.Method(typeof(LevelManager_HubWorld), "TogglePlayerDialogueUIInAtrium");
                    if (toggleMethod != null) toggleMethod.Invoke(__instance, new object[] { false });
                    
                    var listenerField = AccessTools.Field(typeof(LevelManager_HubWorld), "_inventoryActionListener");
                    var listener = listenerField != null ? listenerField.GetValue(__instance) : null;
                    if (listener != null)
                    {
                        var evField = AccessTools.Field(listener.GetType(), "toggleInventoryBoolEvent");
                        var ev = evField != null ? evField.GetValue(listener) as UnityEngine.Events.UnityEvent<bool> : null;
                        if (ev != null) ev.Invoke(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug("Error in PrefixLoadPlayfabInventory: " + ex.ToString());
            }
            return false; // Skip original
        }

        [HarmonyPatch(typeof(StateMachine), "SwitchToPreviousState")]
        [HarmonyPrefix]
        public static bool PrefixSwitchToPreviousState(StateMachine __instance, ref bool __result)
        {
            // ... (keep existing SwitchToPreviousState patch)
            try
            {
                var prevField = AccessTools.Field(typeof(StateMachine), "previousState");
                var prev = prevField != null ? prevField.GetValue(__instance) : null;
                if (prev == null)
                {
                    LogDebug("SwitchToPreviousState: previousState is null. Applying fallback...");
                    if (__instance is HubWorldStateMachine)
                    {
                        var hubSM = (HubWorldStateMachine)__instance;
                        hubSM.ChangeToDefaultState();
                        __result = true;
                        return false; // Skip original
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug("Error in PrefixSwitchToPreviousState: " + ex.Message);
            }
            return true; // Run original
        }

        [HarmonyPatch(typeof(FavorMeter), "HaveFinished")]
        [HarmonyPrefix]
        public static bool PrefixHaveFinished(ref bool __result)
        {
            __result = true;
            return false; // Skip original
        }

        [HarmonyPatch(typeof(UIManager_HubWorld), "Start")]
        [HarmonyPostfix]
        public static void PostfixUIManager_HubWorld_Start(UIManager_HubWorld __instance)
        {
            try
            {
                var container = __instance.LevelSelectionContainer;
                if (container != null)
                {
                    LogDebug("Unlocking all chapters...");
                    UnityEngine.UI.Button[] buttons = container.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    LogDebug("Found " + buttons.Length + " buttons in LevelSelectionContainer.");
                    
                    int chapterIndex = 1; // Start from 1
                    foreach (UnityEngine.UI.Button btn in buttons)
                    {
                        string btnName = btn.name.ToLower();
                        if (btnName.Contains("back") || btnName.Contains("close") || btnName.Contains("quit") || btnName.Contains("exit")) continue;

                        // Enable the button
                        btn.interactable = true;
                        
                        // Disable padlocks (Images or GameObjects named "Lock" etc)
                        foreach (Transform child in btn.transform)
                        {
                            string childName = child.name.ToLower();
                            if (childName.Contains("lock") || childName.Contains("soon") || childName.Contains("reveal"))
                            {
                                child.gameObject.SetActive(false);
                            }
                        }

                        // Add listener if missing
                        int levelToSelect = chapterIndex;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => {
                            LogDebug("Clicked level " + levelToSelect);
                            try
                            {
                                __instance.SelectLevel(levelToSelect);
                            }
                            catch (Exception ex)
                            {
                                LogDebug("SelectLevel threw exception (probably unfinished UI arrays). Forcing direct load! Error: " + ex.Message);
                                var field = typeof(UIManager_HubWorld).GetField("currentSelectingLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (field != null) field.SetValue(__instance, levelToSelect);
                                __instance.ButtonPressed_GameStart();
                            }
                        });
                        
                        chapterIndex++;
                        if (chapterIndex > 4) break; // Only 4 chapters
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug("Error in PostfixUIManager_HubWorld_Start: " + ex.Message);
            }
        }
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(NPCMasterBehavior_Main_L1), "SetServerContext")]
        [HarmonyPrefix]
        public static void PrefixL1(object __instance) { EnsureCharacterConfig(__instance); }

        [HarmonyPatch(typeof(NPCMasterBehavior_Main_L2), "SetServerContext")]
        [HarmonyPrefix]
        public static void PrefixL2(object __instance) { EnsureCharacterConfig(__instance); }

        [HarmonyPatch(typeof(NPCMasterBehavior_Main_L3), "SetServerContext")]
        [HarmonyPrefix]
        public static void PrefixL3(object __instance) { EnsureCharacterConfig(__instance); }

        [HarmonyPatch(typeof(NPCMasterBehavior_Main_L4), "SetServerContext")]
        [HarmonyPrefix]
        public static void PrefixL4(object __instance) { EnsureCharacterConfig(__instance); }

        public static void EnsureCharacterConfig(object __instance)
        {
            try
            {
                var field = AccessTools.Field(__instance.GetType(), "characterConfig");
                if (field == null) return;
                var config = field.GetValue(__instance) as CharacterConfig;
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<CharacterConfig>();
                    config.personality  = new List<int>();
                    config.speakingTone = new List<int>();
                    config.hobby        = new List<int>();
                    config.NPCCurrentAppearanceConfig = ScriptableObject.CreateInstance<NPCAppearance>();
                    config.NPCCurrentAppearanceConfig.prompt_Description = "A friendly anime girl";
                    field.SetValue(__instance, config);
                }
                else if (config.NPCCurrentAppearanceConfig == null)
                {
                    config.NPCCurrentAppearanceConfig = ScriptableObject.CreateInstance<NPCAppearance>();
                    config.NPCCurrentAppearanceConfig.prompt_Description = "A friendly anime girl";
                }
            }
            catch (Exception ex) { LogDebug("Error EnsureCharacterConfig: " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEBUG: Log + catch Communicator.SendToChatGPT
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(Communicator), "SendToChatGPT")]
        [HarmonyPrefix]
        public static bool DebugSendToChatGPT(Communicator __instance, string message)
        {
            try
            {
                LogDebug(">>> Communicator.SendToChatGPT MSG=" + message);
                var presetField = AccessTools.Field(typeof(Communicator), "_presetNPCAI");
                if (presetField != null)
                {
                    PresetNPCAI preset = (PresetNPCAI)presetField.GetValue(__instance);
                    if (preset != null && preset.isAIReplyDisabled) preset.isAIReplyDisabled = false;
                }
                if (__instance.noReplyTimer <= 2f) __instance.noReplyTimer = 10f;
            }
            catch (Exception e) { LogDebug("Error DebugSend: " + e.Message); }
            return true;
        }

        [HarmonyPatch(typeof(Communicator), "SendToChatGPT")]
        [HarmonyFinalizer]
        public static Exception CatchSendException(Exception __exception)
        {
            if (__exception != null)
                LogDebug("!!! EXCEPTION SendToChatGPT: " + __exception.ToString());
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // NPC CHAT SUBMIT
        // ─────────────────────────────────────────────────────────────────────

        [HarmonyPatch(typeof(NPCMasterBehavior_MainCharacter), "OnSubmitChatMessage")]
        [HarmonyPrefix]
        public static bool ForceSubmitMessage(NPCMasterBehavior_MainCharacter __instance, string message, Character characterId)
        {
            try
            {
                var comm = AccessTools.Field(typeof(NPCMasterBehavior_MainCharacter), "communicator").GetValue(__instance) as Communicator;
                var npcDisabledField = AccessTools.Field(typeof(NPCMasterBehavior_MainCharacter), "npcDisabled");
                bool isDisabled = (bool)npcDisabledField.GetValue(__instance);
                if (isDisabled) npcDisabledField.SetValue(__instance, false);
                if (comm != null && comm.noReplyTimer <= 2f) comm.noReplyTimer = 10f;
            }
            catch (Exception e) { LogDebug("Error ForceSubmit: " + e.Message); }
            return true;
        }
    }

    public class TTSManager : MonoBehaviour
    {
        public static TTSManager Instance;

        void Awake()
        {
            Instance = this;
        }

        public void StartTTSCoroutine(string text, AudioSource source)
        {
            StartCoroutine(DownloadAndPlayTTS(text, source));
        }

        public System.Collections.IEnumerator DownloadAndPlayTTS(string text, AudioSource source)
        {
            UltimateFixPlugin.LogDebug("TTS Coroutine: Started execution");
            
            if (string.IsNullOrEmpty(UltimateFixPlugin.cfgTTSAPIKey))
            {
                UltimateFixPlugin.LogDebug("TTS Error: API Key is empty! Please configure TTS API Key.");
                yield break;
            }

            string url = UltimateFixPlugin.cfgTTSBaseURL;
            byte[] bodyData = null;
            Dictionary<string, string> headers = new Dictionary<string, string>();

            if (UltimateFixPlugin.cfgTTSProvider == "OpenAI Compatible")
            {
                if (string.IsNullOrEmpty(url)) url = "https://api.openai.com/v1/audio/speech";
                
                string escapedText = UltimateFixPlugin.EscapeJsonString(text);
                string voiceModel = string.IsNullOrEmpty(UltimateFixPlugin.cfgTTSModel) ? "alloy" : UltimateFixPlugin.cfgTTSModel;
                
                string json = "{\"model\":\"tts-1\",\"input\":\"" + escapedText + "\",\"voice\":\"" + voiceModel + "\",\"response_format\":\"mp3\"}";
                bodyData = Encoding.UTF8.GetBytes(json);

                headers["Authorization"] = "Bearer " + UltimateFixPlugin.cfgTTSAPIKey;
                headers["Content-Type"] = "application/json";
            }
            else // Azure
            {
                if (string.IsNullOrEmpty(url)) url = "https://" + UltimateFixPlugin.cfgTTSRegion + ".tts.speech.microsoft.com/cognitiveservices/v1";
                
                string voiceModel = string.IsNullOrEmpty(UltimateFixPlugin.cfgTTSModel) ? "en-US-JaneNeural" : UltimateFixPlugin.cfgTTSModel;
                string escapedText = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                
                string ssml = "<speak version='1.0' xml:lang='en-US'><voice xml:lang='en-US' xml:gender='Female' name='" + voiceModel + "'>" 
                    + escapedText 
                    + "</voice></speak>";
                
                bodyData = Encoding.UTF8.GetBytes(ssml);

                headers["Ocp-Apim-Subscription-Key"] = UltimateFixPlugin.cfgTTSAPIKey;
                headers["Content-Type"] = "application/ssml+xml";
                headers["X-Microsoft-OutputFormat"] = "audio-16khz-32kbitrate-mono-mp3";
            }

            UltimateFixPlugin.LogDebug("TTS Request URL: " + url);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(bodyData);
                www.downloadHandler = new DownloadHandlerBuffer();
                foreach (var kvp in headers)
                {
                    www.SetRequestHeader(kvp.Key, kvp.Value);
                }

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                {
                    UltimateFixPlugin.LogDebug("TTS Network Error: " + www.error + "\n" + www.downloadHandler.text);
                }
                else
                {
                    byte[] audioBytes = www.downloadHandler.data;
                    if (audioBytes != null && audioBytes.Length > 0)
                    {
                        UltimateFixPlugin.LogDebug("TTS Success! Downloaded " + audioBytes.Length + " bytes.");
                        string tempFile = Path.Combine(UltimateFixPlugin.pluginDir, "temp_tts.mp3");
                        File.WriteAllBytes(tempFile, audioBytes);
                        
                        string fileUrl = "file:///" + tempFile.Replace("\\", "/");
                        using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
                        {
                            yield return audioReq.SendWebRequest();
                            if (audioReq.result == UnityWebRequest.Result.ConnectionError || audioReq.result == UnityWebRequest.Result.ProtocolError)
                            {
                                UltimateFixPlugin.LogDebug("TTS Audio Load Error: " + audioReq.error);
                            }
                            else
                            {
                                AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
                                if (clip != null)
                                {
                                    // Wait until clip is fully loaded
                                    while (clip.loadState == AudioDataLoadState.Loading)
                                    {
                                        yield return null;
                                    }

                                    if (clip.loadState == AudioDataLoadState.Loaded)
                                    {
                                        source.clip = clip;
                                        source.volume = 1f;
                                        source.mute = false;
                                        source.enabled = true;
                                        source.Play();
                                        UltimateFixPlugin.LogDebug("TTS Audio Playing!");
                                        
                                        // Wait for it to finish
                                        while (source.isPlaying)
                                        {
                                            yield return null;
                                        }
                                        UnityEngine.Object.Destroy(clip);
                                    }
                                    else
                                    {
                                        UltimateFixPlugin.LogDebug("TTS Audio Load Error: Clip load state is " + clip.loadState.ToString());
                                    }
                                }
                                else
                                {
                                    UltimateFixPlugin.LogDebug("TTS Audio Load Error: Clip is null.");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public class UIHunter : MonoBehaviour
    {
        void Start() { InvokeRepeating("Hunt", 0.5f, 0.5f); }
        void Hunt()
        {
            if (SceneManager.GetActiveScene().name == "MenuState")
            {
                Canvas[] canvases = FindObjectsOfType<Canvas>();
                foreach (Canvas c in canvases)
                    foreach (Transform child in c.transform)
                    {
                        string name = child.name.ToLower();
                        if ((name.Contains("loading") || name.Contains("fade") || name.Contains("black") || name.Contains("splash"))
                            && child.gameObject.activeInHierarchy)
                            child.gameObject.SetActive(false);
                    }
            }
        }
    }
}