using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMain.GameLogic.Network
{
    /// <summary>
    /// Isolated UnityWebRequest config demo with local fallback. It does not write combat state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RemoteConfigFetchDemo : MonoBehaviour
    {
        [SerializeField] private string configUrl = "http://127.0.0.1:8080/client_demo_config.json";
        [SerializeField] [Min(1)] private int timeoutSeconds = 3;
        [SerializeField] private bool fetchOnStart = false;
        [SerializeField] private bool showPanel = true;
        [SerializeField] private KeyCode togglePanelKey = KeyCode.F9;
        [SerializeField] private Rect panelRect = new Rect(12f, 230f, 420f, 230f);
        [SerializeField] [TextArea(4, 8)]
        private string fallbackJson =
            "{\"version\":\"local-fallback\",\"banner\":\"本地 fallback 配置\",\"enemyWavePreview\":2,\"enableRemoteBanner\":false}";

        private RemoteDemoConfig currentConfig = RemoteDemoConfig.Fallback;
        private bool isFetching;
        private string status = "未拉取";

        public RemoteDemoConfig CurrentConfig => currentConfig;

        private void Start()
        {
            if (fetchOnStart)
            {
                StartCoroutine(FetchConfigRoutine());
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(togglePanelKey))
            {
                showPanel = !showPanel;
            }
        }

        private void OnGUI()
        {
            if (!showPanel)
            {
                return;
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("UnityWebRequest 配置拉取 Demo");
            GUILayout.Label("URL: " + configUrl);
            GUILayout.Label("状态: " + status);
            GUILayout.Label("版本: " + currentConfig.version);
            GUILayout.Label("横幅: " + currentConfig.banner);
            GUILayout.Label("波次预览: " + currentConfig.enemyWavePreview + "  远端开关: " + currentConfig.enableRemoteBanner);

            GUI.enabled = !isFetching;
            if (GUILayout.Button("拉取配置"))
            {
                StartCoroutine(FetchConfigRoutine());
            }

            if (GUILayout.Button("使用本地 fallback"))
            {
                ApplyFallback("手动切换到本地 fallback");
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private IEnumerator FetchConfigRoutine()
        {
            if (isFetching)
            {
                yield break;
            }

            isFetching = true;
            status = "正在请求远端配置...";

            using (var request = UnityWebRequest.Get(configUrl))
            {
                request.timeout = Mathf.Max(1, timeoutSeconds);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (TryApplyJson(request.downloadHandler.text, "远端配置加载成功"))
                    {
                        isFetching = false;
                        yield break;
                    }

                    ApplyFallback("远端 JSON 解析失败，已使用本地 fallback");
                    isFetching = false;
                    yield break;
                }

                ApplyFallback("远端请求失败，已使用本地 fallback: " + request.error);
            }

            isFetching = false;
        }

        private bool TryApplyJson(string json, string successStatus)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                var parsed = JsonUtility.FromJson<RemoteDemoConfig>(json);
                if (!parsed.IsValid)
                {
                    return false;
                }

                currentConfig = parsed;
                status = successStatus;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("RemoteConfigFetchDemo failed to parse config json: " + exception.Message, this);
                return false;
            }
        }

        private void ApplyFallback(string reason)
        {
            if (!TryApplyJson(fallbackJson, reason))
            {
                currentConfig = RemoteDemoConfig.Fallback;
                status = reason + "；fallback JSON 无效，使用内置默认值";
            }
        }
    }

    [Serializable]
    public struct RemoteDemoConfig
    {
        public string version;
        public string banner;
        public int enemyWavePreview;
        public bool enableRemoteBanner;

        public static RemoteDemoConfig Fallback => new RemoteDemoConfig
        {
            version = "embedded-fallback",
            banner = "本地默认配置",
            enemyWavePreview = 1,
            enableRemoteBanner = false,
        };

        public bool IsValid => !string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(banner);
    }
}
