using System.Collections;
using System.IO;
using UnityEngine;

namespace GameMain.GameLogic.ResourceLoading
{
    /// <summary>
    /// Independent AssetBundle loading demo. Attach it only in a test scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetBundleLoadDemoController : MonoBehaviour
    {
        [SerializeField] private string bundleRelativePath = "AssetBundleDemo/client_demo_assets";
        [SerializeField] private string assetName = "AssetBundleDemo_CharacterPlaceholder";
        [SerializeField] private bool loadOnStart = false;
        [SerializeField] private bool showPanel = true;
        [SerializeField] private KeyCode togglePanelKey = KeyCode.F8;
        [SerializeField] private Rect panelRect = new Rect(12f, 12f, 380f, 210f);
        [SerializeField] private Transform spawnParent;
        [SerializeField] private Vector3 spawnPosition = Vector3.zero;

        private AssetBundle loadedBundle;
        private GameObject spawnedInstance;
        private bool isLoading;
        private string status = "未加载";

        private void Start()
        {
            if (loadOnStart)
            {
                StartCoroutine(LoadDemoAssetRoutine());
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
            GUILayout.Label("AssetBundle 资源加载 Demo");
            GUILayout.Label("Bundle: " + bundleRelativePath);
            GUILayout.Label("Asset: " + assetName);
            GUILayout.Label("状态: " + status);

            GUI.enabled = !isLoading;
            if (GUILayout.Button("加载角色占位 Prefab"))
            {
                StartCoroutine(LoadDemoAssetRoutine());
            }

            if (GUILayout.Button("卸载实例"))
            {
                DestroySpawnedInstance();
            }

            if (GUILayout.Button("卸载 Bundle"))
            {
                UnloadBundle(false);
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            DestroySpawnedInstance();
            UnloadBundle(false);
        }

        private IEnumerator LoadDemoAssetRoutine()
        {
            if (isLoading)
            {
                yield break;
            }

            isLoading = true;
            DestroySpawnedInstance();

            var bundlePath = GetBundlePath();
            if (!File.Exists(bundlePath))
            {
                status = "未找到 Bundle，请先执行 Tools/GameMain/AssetBundle Demo/Build Demo Bundle";
                isLoading = false;
                yield break;
            }

            if (loadedBundle == null)
            {
                status = "正在异步加载 Bundle...";
                var bundleRequest = AssetBundle.LoadFromFileAsync(bundlePath);
                yield return bundleRequest;

                loadedBundle = bundleRequest.assetBundle;
                if (loadedBundle == null)
                {
                    status = "Bundle 加载失败: " + bundlePath;
                    isLoading = false;
                    yield break;
                }
            }

            status = "正在异步加载资源...";
            var assetRequest = loadedBundle.LoadAssetAsync<GameObject>(assetName);
            yield return assetRequest;

            var prefab = assetRequest.asset as GameObject;
            if (prefab == null)
            {
                status = "资源不存在或类型错误: " + assetName;
                isLoading = false;
                yield break;
            }

            spawnedInstance = Instantiate(prefab, spawnPosition, Quaternion.identity, spawnParent);
            spawnedInstance.name = assetName + "_LoadedFromAssetBundle";
            status = "加载成功: " + spawnedInstance.name;
            isLoading = false;
        }

        private string GetBundlePath()
        {
            return Path.Combine(Application.streamingAssetsPath, bundleRelativePath).Replace("\\", "/");
        }

        private void DestroySpawnedInstance()
        {
            if (spawnedInstance == null)
            {
                return;
            }

            Destroy(spawnedInstance);
            spawnedInstance = null;
            status = loadedBundle == null ? "未加载" : "已卸载实例，Bundle 保持加载";
        }

        private void UnloadBundle(bool unloadAllLoadedObjects)
        {
            if (loadedBundle == null)
            {
                status = "Bundle 未加载";
                return;
            }

            loadedBundle.Unload(unloadAllLoadedObjects);
            loadedBundle = null;
            status = "Bundle 已卸载";
        }
    }
}
