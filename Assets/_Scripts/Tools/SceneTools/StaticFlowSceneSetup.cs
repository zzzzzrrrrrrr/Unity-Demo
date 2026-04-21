// Path: Assets/_Scripts/Tools/SceneTools/StaticFlowSceneSetup.cs
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARPGDemo.Tools.SceneTools
{
    public static class StaticFlowSceneSetup
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.scene";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.scene";
        private const string SampleScene02Path = "Assets/Scenes/SampleScene_02.scene";
        private const string MainMenuSimpleControllerTypeName = "ARPGDemo.UI.MainMenuSimpleController";
        private const string ActorStatsTypeName = "ARPGDemo.Core.ActorStats";
        private const string PlayerSkillCasterTypeName = "ARPGDemo.Game.PlayerSkillCaster";
        private const string PlayerControllerTypeName = "ARPGDemo.Game.PlayerController";
        private const string AttackHitbox2DTypeName = "ARPGDemo.Game.AttackHitbox2D";

        [MenuItem("Tools/ARPG/SceneTools/Apply Static Flow Setup", false, 2410)]
        public static void ApplyStaticFlowSetup()
        {
            SetupMainMenuScene();
            SetupSampleScene02();
            SetupSampleScenePlayerSkill();

            AssetDatabase.SaveAssets();
            Debug.Log("[StaticFlowSceneSetup] Static flow scenes updated.");
        }

        public static void SetupMainMenuScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[StaticFlowSceneSetup] Failed to open MainMenu.scene");
                return;
            }

            EnsureRootObjectActive(scene, "GameRoot", true);
            EnsureRootObjectActive(scene, "Main Camera", true);
            EnsureRootObjectActive(scene, "EventSystem", true);
            EnsureRootObjectActive(scene, "Canvas", true);
            EnsureRootObjectActive(scene, "Player", false);
            EnsureRootObjectActive(scene, "Ground", false);
            EnsureRootObjectActive(scene, "Enemy_01", false);
            EnsureRootObjectActive(scene, "Enemy_02", false);
            EnsureRootObjectActive(scene, "Enemy_03", false);
            EnsureRootObjectActive(scene, "Enemy_04", false);
            EnsureRootObjectActive(scene, "FinishZone", false);
            EnsureRootObjectActive(scene, "FinishZone_Visual", false);
            EnsureRootObjectActive(scene, "FinishZone_Glow", false);
            EnsureRootObjectActive(scene, "FinishZone_Label", false);
            EnsureRootObjectActive(scene, "HUD", false);

            Camera camera = FindInScene<Camera>(scene, "Main Camera");
            if (camera != null)
            {
                camera.enabled = true;
                camera.orthographic = true;
                camera.targetDisplay = 0;
                if (!camera.CompareTag("MainCamera"))
                {
                    camera.tag = "MainCamera";
                }
            }

            EnsureEventSystem(scene);
            Canvas canvas = EnsureCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError("[StaticFlowSceneSetup] MainMenu missing Canvas.");
                return;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.pivot = new Vector2(0.5f, 0.5f);
                canvasRect.anchoredPosition = Vector2.zero;
                canvasRect.sizeDelta = Vector2.zero;
            }

            GameObject panel = EnsureUiPanel(canvas.transform);
            Text titleText = EnsureTitleText(panel.transform);
            Button startButton = EnsureButton(panel.transform, "StartButton", "Start Game", new Vector2(0f, 20f));
            Button quitButton = EnsureButton(panel.transform, "QuitButton", "Quit Game", new Vector2(0f, -66f));

            GameObject controllerGo = EnsureChild(panel.transform, "MainMenuController");
            Component controller = EnsureComponentByName(controllerGo, MainMenuSimpleControllerTypeName);
            ConfigureMainMenuController(controller, panel, startButton, quitButton, titleText);

            DisableLegacyMainMenuControllers(scene, controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void SetupSampleScene02()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScene02Path, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[StaticFlowSceneSetup] Failed to open SampleScene_02.scene");
                return;
            }

            EnsureRootObjectActive(scene, "GameRoot", true);
            EnsureRootObjectActive(scene, "Main Camera", true);
            EnsureRootObjectActive(scene, "Canvas", true);
            EnsureRootObjectActive(scene, "EventSystem", true);
            EnsureRootObjectActive(scene, "Player", true);
            EnsureRootObjectActive(scene, "Ground", true);
            EnsureRootObjectActive(scene, "Boundary_Left", true);
            EnsureRootObjectActive(scene, "Boundary_Right", true);
            EnsureRootObjectActive(scene, "Enemy_01", false);
            EnsureRootObjectActive(scene, "Enemy_02", false);
            EnsureRootObjectActive(scene, "Enemy_03", false);
            EnsureRootObjectActive(scene, "Enemy_04", false);
            EnsureRootObjectActive(scene, "FinishZone", false);
            EnsureRootObjectActive(scene, "FinishZone_Visual", false);
            EnsureRootObjectActive(scene, "FinishZone_Glow", false);
            EnsureRootObjectActive(scene, "FinishZone_Label", false);
            EnsureRootObjectActive(scene, "HUD", true);

            Camera camera = FindInScene<Camera>(scene, "Main Camera");
            if (camera != null)
            {
                camera.enabled = true;
                camera.orthographic = true;
                camera.targetDisplay = 0;
                if (!camera.CompareTag("MainCamera"))
                {
                    camera.tag = "MainCamera";
                }
            }

            Canvas canvas = EnsureCanvas(scene);
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null)
                {
                    canvasRect.localScale = Vector3.one;
                    canvasRect.anchorMin = Vector2.zero;
                    canvasRect.anchorMax = Vector2.one;
                    canvasRect.pivot = new Vector2(0.5f, 0.5f);
                    canvasRect.anchoredPosition = Vector2.zero;
                    canvasRect.sizeDelta = Vector2.zero;
                }
            }

            GameObject ground = FindInScene<GameObject>(scene, "Ground");
            if (ground != null)
            {
                BoxCollider2D groundCollider = EnsureComponent<BoxCollider2D>(ground);
                groundCollider.enabled = true;
                groundCollider.isTrigger = false;
                if (groundCollider.size.x < 10f)
                {
                    groundCollider.size = new Vector2(30f, 1f);
                }
            }

            EnsureBoundaryCollider(scene, "Boundary_Left");
            EnsureBoundaryCollider(scene, "Boundary_Right");
            EnsurePlayerAndSkill(scene);
            EnsureEventSystem(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void SetupSampleScenePlayerSkill()
        {
            Scene scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[StaticFlowSceneSetup] Failed to open SampleScene.scene");
                return;
            }

            EnsurePlayerAndSkill(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureMainMenuController(
            Component controller,
            GameObject panel,
            Button startButton,
            Button quitButton,
            Text titleText)
        {
            if (controller == null)
            {
                Debug.LogWarning("[StaticFlowSceneSetup] MainMenuSimpleController type is missing. Skip binding.");
                return;
            }

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
            so.FindProperty("gameplaySceneName").stringValue = "SampleScene";
            so.FindProperty("panelRoot").objectReferenceValue = panel;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("title").stringValue = "ARPG Demo";
            so.FindProperty("autoFindSceneReferences").boolValue = true;
            so.FindProperty("useOnGuiFallback").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Behaviour behaviour = controller as Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        private static void DisableLegacyMainMenuControllers(Scene scene, Component keep)
        {
            Type controllerType = ResolveTypeByName(MainMenuSimpleControllerTypeName);
            if (controllerType == null || !typeof(Component).IsAssignableFrom(controllerType))
            {
                return;
            }

            UnityEngine.Object[] allSimple = Resources.FindObjectsOfTypeAll(controllerType);
            for (int i = 0; i < allSimple.Length; i++)
            {
                Component c = allSimple[i] as Component;
                if (c == null || c == keep || c.gameObject.scene != scene)
                {
                    continue;
                }

                Behaviour behaviour = c as Behaviour;
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void EnsureBoundaryCollider(Scene scene, string name)
        {
            GameObject boundary = FindInScene<GameObject>(scene, name);
            if (boundary == null)
            {
                return;
            }

            BoxCollider2D col = EnsureComponent<BoxCollider2D>(boundary);
            col.enabled = true;
            col.isTrigger = false;
            if (col.size.x < 0.4f || col.size.y < 4f)
            {
                col.size = new Vector2(Mathf.Max(1f, col.size.x), Mathf.Max(8f, col.size.y));
            }
        }

        private static void EnsurePlayerAndSkill(Scene scene)
        {
            GameObject player = FindInScene<GameObject>(scene, "Player");
            if (player == null)
            {
                return;
            }

            player.SetActive(true);

            Component playerController = FindComponentByName(player, PlayerControllerTypeName);
            Behaviour playerControllerBehaviour = playerController as Behaviour;
            if (playerControllerBehaviour != null)
            {
                playerControllerBehaviour.enabled = true;
            }

            Component stats = FindComponentByName(player, ActorStatsTypeName);
            Behaviour statsBehaviour = stats as Behaviour;
            if (statsBehaviour != null)
            {
                statsBehaviour.enabled = true;
            }

            Component hitbox = FindComponentInChildrenByName(player, AttackHitbox2DTypeName);
            Behaviour hitboxBehaviour = hitbox as Behaviour;
            if (hitboxBehaviour != null)
            {
                hitboxBehaviour.enabled = true;
            }

            Component skill = EnsureComponentByName(player, PlayerSkillCasterTypeName);
            Behaviour skillBehaviour = skill as Behaviour;
            if (skillBehaviour != null)
            {
                skillBehaviour.enabled = true;
            }

            if (skill != null)
            {
                SerializedObject so = new SerializedObject(skill);
                SerializedProperty logProp = so.FindProperty("logCast");
                if (logProp != null)
                {
                    logProp.boolValue = true;
                }

                SerializedProperty drawProp = so.FindProperty("drawGizmos");
                if (drawProp != null)
                {
                    drawProp.boolValue = true;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject ground = FindInScene<GameObject>(scene, "Ground");
            if (ground != null)
            {
                BoxCollider2D groundCol = ground.GetComponent<BoxCollider2D>();
                if (groundCol != null && groundCol.enabled)
                {
                    float topY = groundCol.bounds.max.y;
                    Vector3 pos = player.transform.position;
                    if (pos.y < topY + 0.5f)
                    {
                        player.transform.position = new Vector3(pos.x, topY + 0.95f, pos.z);
                    }
                }
            }
        }

        private static EventSystem EnsureEventSystem(Scene scene)
        {
            EventSystem eventSystem = null;
            EventSystem[] all = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene == scene)
                {
                    eventSystem = all[i];
                    break;
                }
            }

            if (eventSystem == null)
            {
                GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(go, scene);
                eventSystem = go.GetComponent<EventSystem>();
            }

            if (eventSystem != null)
            {
                eventSystem.gameObject.SetActive(true);
                EnsureComponent<StandaloneInputModule>(eventSystem.gameObject);
            }

            return eventSystem;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            Canvas canvas = FindInScene<Canvas>(scene, "Canvas");
            if (canvas == null)
            {
                GameObject go = new GameObject(
                    "Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(go, scene);

                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            canvas.gameObject.SetActive(true);
            canvas.enabled = true;

            RectTransform rect = canvas.transform as RectTransform;
            if (rect != null)
            {
                rect.localScale = Vector3.one;
            }

            return canvas;
        }

        private static GameObject EnsureUiPanel(Transform parent)
        {
            GameObject panel = EnsureChild(parent, "MainMenuPanel");
            RectTransform rect = EnsureComponent<RectTransform>(panel);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 320f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            EnsureComponent<CanvasRenderer>(panel);
            Image image = EnsureComponent<Image>(panel);
            image.color = new Color(0.09f, 0.11f, 0.15f, 0.9f);
            panel.SetActive(true);
            return panel;
        }

        private static Text EnsureTitleText(Transform panel)
        {
            GameObject title = EnsureChild(panel, "MainMenuTitle");
            RectTransform rect = EnsureComponent<RectTransform>(title);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(420f, 64f);
            rect.localScale = Vector3.one;

            EnsureComponent<CanvasRenderer>(title);
            Text text = EnsureComponent<Text>(title);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 42;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "ARPG Demo";
            return text;
        }

        private static Button EnsureButton(Transform panel, string name, string label, Vector2 anchoredPos)
        {
            GameObject buttonGo = EnsureChild(panel, name);
            RectTransform rect = EnsureComponent<RectTransform>(buttonGo);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(360f, 64f);
            rect.localScale = Vector3.one;

            EnsureComponent<CanvasRenderer>(buttonGo);
            Image image = EnsureComponent<Image>(buttonGo);
            image.color = new Color(0.2f, 0.3f, 0.42f, 0.96f);

            Button button = EnsureComponent<Button>(buttonGo);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.3f, 0.42f, 0.96f);
            colors.highlightedColor = new Color(0.3f, 0.42f, 0.58f, 1f);
            colors.pressedColor = new Color(0.14f, 0.22f, 0.33f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            button.colors = colors;

            GameObject textGo = EnsureChild(buttonGo.transform, "Text");
            RectTransform textRect = EnsureComponent<RectTransform>(textGo);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.localScale = Vector3.one;

            EnsureComponent<CanvasRenderer>(textGo);
            Text text = EnsureComponent<Text>(textGo);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            return button;
        }

        private static GameObject EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child.gameObject;
            }

            GameObject go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void EnsureRootObjectActive(Scene scene, string objectName, bool active)
        {
            GameObject go = FindInScene<GameObject>(scene, objectName);
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        private static T FindInScene<T>(Scene scene, string name) where T : UnityEngine.Object
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindTransformRecursive(roots[i].transform, name);
                if (found == null)
                {
                    continue;
                }

                if (typeof(T) == typeof(GameObject))
                {
                    return found.gameObject as T;
                }

                T component = found.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransformRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Type ResolveTypeByName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return null;
            }

            Type direct = Type.GetType(fullTypeName, false);
            if (direct != null)
            {
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                Type candidate = assembly.GetType(fullTypeName, false);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Component EnsureComponentByName(GameObject go, string fullTypeName)
        {
            if (go == null)
            {
                return null;
            }

            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogWarning("[StaticFlowSceneSetup] Cannot resolve type: " + fullTypeName);
                return null;
            }

            Component component = go.GetComponent(type);
            if (component == null)
            {
                component = go.AddComponent(type);
            }

            return component;
        }

        private static Component FindComponentByName(GameObject go, string fullTypeName)
        {
            if (go == null)
            {
                return null;
            }

            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                return null;
            }

            return go.GetComponent(type);
        }

        private static Component FindComponentInChildrenByName(GameObject go, string fullTypeName)
        {
            if (go == null)
            {
                return null;
            }

            Type type = ResolveTypeByName(fullTypeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
            {
                return null;
            }

            return go.GetComponentInChildren(type, true);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }

            return component;
        }
    }
}
#endif
