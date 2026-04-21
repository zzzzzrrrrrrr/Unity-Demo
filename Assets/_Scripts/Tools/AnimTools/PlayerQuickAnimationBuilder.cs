// Path: Assets/_Scripts/Tools/AnimTools/PlayerQuickAnimationBuilder.cs
// Function: One-click build for Player sprite clips + Animator Controller + Player_View prefab.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ARPGDemo.Tools.AnimTools
{
    public static class PlayerQuickAnimationBuilder
    {
        private const string MenuPath = "Tools/ARPG/Build Player Animations";

        private const string PlayerAnimRoot = "Assets/_Anim/Player";
        private const string GeneratedFolder = "Assets/_Anim/Player/Generated";
        private const string ControllerPath = GeneratedFolder + "/Player_Auto.controller";
        private const string PrefabFolder = "Assets/_Prefabs";
        private const string PlayerViewPrefabPath = PrefabFolder + "/Player_View.prefab";

        private static readonly ActionRule[] Rules =
        {
            new ActionRule("Idle", true, 8),
            new ActionRule("Attack1", false, 12),
            new ActionRule("Attack2", false, 12),
            new ActionRule("Attack3", false, 12),
            new ActionRule("Hurt", false, 10),
            new ActionRule("Death", false, 10)
        };

        [MenuItem(MenuPath, false, 1001)]
        public static void Build()
        {
            try
            {
                EnsureFolder(PlayerAnimRoot);
                EnsureFolder(GeneratedFolder);
                EnsureFolder(PrefabFolder);

                List<string> warnings = new List<string>(16);
                Dictionary<string, AnimationClip> clips = BuildClips(warnings);

                List<Sprite> runSprites = CollectSortedSprites(PlayerAnimRoot + "/Run");
                bool hasRun = runSprites.Count > 0;
                if (hasRun)
                {
                    clips["Run"] = CreateOrUpdateClip("Run", runSprites, true, 8);
                }
                else
                {
                    warnings.Add("- Folder empty or missing: Assets/_Anim/Player/Run, skip Move state.");
                }

                AnimatorController controller = CreateOrUpdateController(clips, hasRun, warnings);
                GameObject prefab = CreateOrUpdatePlayerViewPrefab(controller, clips);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                StringBuilder summary = new StringBuilder(512);
                summary.AppendLine("[PlayerQuickAnimationBuilder] Build completed.");
                summary.AppendLine("Generated Controller:");
                summary.AppendLine("- " + AssetDatabase.GetAssetPath(controller));
                summary.AppendLine("Generated Clips:");

                foreach (KeyValuePair<string, AnimationClip> pair in clips)
                {
                    summary.AppendLine("- " + AssetDatabase.GetAssetPath(pair.Value));
                }

                summary.AppendLine("Generated Prefab:");
                summary.AppendLine("- " + AssetDatabase.GetAssetPath(prefab));
                summary.AppendLine("Usage:");
                summary.AppendLine("1) Open menu: Tools/ARPG/Build Player Animations");
                summary.AppendLine("2) Use Player_View prefab and assign it to your player visual root.");
                summary.AppendLine("3) Ensure gameplay scripts trigger: Attack1/Attack2/Attack3/Hurt/Die.");

                if (warnings.Count > 0)
                {
                    summary.AppendLine("Warnings:");
                    for (int i = 0; i < warnings.Count; i++)
                    {
                        summary.AppendLine(warnings[i]);
                    }
                }

                Debug.Log(summary.ToString());
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                EditorUtility.DisplayDialog("Build Player Animations", "Build complete. See Console for details.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlayerQuickAnimationBuilder] Build failed: " + ex);
                EditorUtility.DisplayDialog("Build Player Animations", "Build failed. Check Console.", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuild()
        {
            return AssetDatabase.IsValidFolder(PlayerAnimRoot);
        }

        private static Dictionary<string, AnimationClip> BuildClips(List<string> warnings)
        {
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(Rules.Length + 1);
            for (int i = 0; i < Rules.Length; i++)
            {
                ActionRule rule = Rules[i];
                string folder = PlayerAnimRoot + "/" + rule.ActionName;
                List<Sprite> sprites = CollectSortedSprites(folder);

                if (sprites.Count == 0)
                {
                    warnings.Add("- Folder empty or missing: " + folder + ", skip " + rule.ActionName + " clip.");
                    continue;
                }

                clips[rule.ActionName] = CreateOrUpdateClip(rule.ActionName, sprites, rule.Loop, rule.Samples);
            }

            return clips;
        }

        private static AnimationClip CreateOrUpdateClip(string actionName, List<Sprite> sprites, bool loop, int samples)
        {
            string clipPath = GeneratedFolder + "/Player_" + actionName + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = Mathf.Max(1, samples);

            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keys);
            SetLoop(clip, loop);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static List<Sprite> CollectSortedSprites(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return new List<Sprite>(0);
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<Sprite> sprites = new List<Sprite>(32);

            CollectFromFilter("t:Sprite", folder, seen, sprites);
            CollectFromFilter("t:Texture2D", folder, seen, sprites);

            sprites.Sort(SpriteNameComparer.Instance);
            return sprites;
        }

        private static void CollectFromFilter(string filter, string folder, HashSet<string> seen, List<Sprite> target)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int a = 0; a < assets.Length; a++)
                {
                    Sprite sprite = assets[a] as Sprite;
                    if (sprite == null)
                    {
                        continue;
                    }

                    string key = path + "::" + sprite.name;
                    if (seen.Add(key))
                    {
                        target.Add(sprite);
                    }
                }
            }
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            SerializedObject so = new SerializedObject(clip);
            SerializedProperty clipSettings = so.FindProperty("m_AnimationClipSettings");
            if (clipSettings != null)
            {
                SerializedProperty loopTime = clipSettings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null)
                {
                    loopTime.boolValue = loop;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static AnimatorController CreateOrUpdateController(
            IReadOnlyDictionary<string, AnimationClip> clips,
            bool hasRun,
            List<string> warnings)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack1", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack2", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Attack3", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            AnimatorState idle = sm.AddState("Idle", new Vector3(120f, 120f, 0f));
            idle.motion = ResolveIdleClip(clips);
            sm.defaultState = idle;

            if (hasRun && clips.TryGetValue("Run", out AnimationClip runClip))
            {
                AnimatorState move = sm.AddState("Move", new Vector3(320f, 120f, 0f));
                move.motion = runClip;

                AnimatorStateTransition idleToMove = idle.AddTransition(move);
                idleToMove.hasExitTime = false;
                idleToMove.duration = 0.05f;
                idleToMove.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

                AnimatorStateTransition moveToIdle = move.AddTransition(idle);
                moveToIdle.hasExitTime = false;
                moveToIdle.duration = 0.05f;
                moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            }

            AddActionStateWithAnyTransition(sm, clips, idle, "Attack1", "Attack1", new Vector3(560f, 20f, 0f), true, warnings);
            AddActionStateWithAnyTransition(sm, clips, idle, "Attack2", "Attack2", new Vector3(760f, 20f, 0f), true, warnings);
            AddActionStateWithAnyTransition(sm, clips, idle, "Attack3", "Attack3", new Vector3(960f, 20f, 0f), true, warnings);
            AddActionStateWithAnyTransition(sm, clips, idle, "Hurt", "Hurt", new Vector3(760f, 220f, 0f), true, warnings);
            AddActionStateWithAnyTransition(sm, clips, idle, "Death", "Die", new Vector3(980f, 240f, 0f), false, warnings);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddActionStateWithAnyTransition(
            AnimatorStateMachine sm,
            IReadOnlyDictionary<string, AnimationClip> clips,
            AnimatorState idle,
            string stateName,
            string triggerName,
            Vector3 position,
            bool returnToIdle,
            List<string> warnings)
        {
            if (!clips.TryGetValue(stateName, out AnimationClip clip) || clip == null)
            {
                warnings.Add("- Missing " + stateName + " clip, skip " + stateName + " state.");
                return;
            }

            AnimatorState state = sm.AddState(stateName, position);
            state.motion = clip;

            AnimatorStateTransition anyToState = sm.AddAnyStateTransition(state);
            anyToState.hasExitTime = false;
            anyToState.duration = 0f;
            anyToState.canTransitionToSelf = false;
            anyToState.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            if (!returnToIdle)
            {
                return;
            }

            AnimatorStateTransition backToIdle = state.AddTransition(idle);
            backToIdle.hasExitTime = true;
            backToIdle.exitTime = 0.98f;
            backToIdle.duration = 0.05f;
        }

        private static AnimationClip ResolveIdleClip(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            if (clips.TryGetValue("Idle", out AnimationClip idle) && idle != null)
            {
                return idle;
            }

            AnimationClip fallback = clips.Values.FirstOrDefault(c => c != null);
            if (fallback != null)
            {
                return fallback;
            }

            AnimationClip empty = new AnimationClip();
            string emptyPath = GeneratedFolder + "/Player_Idle_Empty.anim";
            AnimationClip exists = AssetDatabase.LoadAssetAtPath<AnimationClip>(emptyPath);
            if (exists != null)
            {
                return exists;
            }

            AssetDatabase.CreateAsset(empty, emptyPath);
            return empty;
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                sm.RemoveState(states[i].state);
            }

            AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
            for (int i = anyTransitions.Length - 1; i >= 0; i--)
            {
                sm.RemoveAnyStateTransition(anyTransitions[i]);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name)
                {
                    if (parameters[i].type != type)
                    {
                        controller.RemoveParameter(parameters[i]);
                        controller.AddParameter(name, type);
                    }

                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static GameObject CreateOrUpdatePlayerViewPrefab(AnimatorController controller, IReadOnlyDictionary<string, AnimationClip> clips)
        {
            GameObject root = new GameObject("Player_View");
            SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            if (clips.TryGetValue("Idle", out AnimationClip idleClip) && idleClip != null)
            {
                Sprite first = GetFirstSprite(idleClip);
                if (first != null)
                {
                    sr.sprite = first;
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerViewPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Sprite GetFirstSprite(AnimationClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].propertyName != "m_Sprite")
                {
                    continue;
                }

                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[i]);
                if (keys != null && keys.Length > 0)
                {
                    return keys[0].value as Sprite;
                }
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            if (parts.Length <= 1)
            {
                throw new InvalidOperationException("Invalid folder path: " + folderPath);
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private readonly struct ActionRule
        {
            public readonly string ActionName;
            public readonly bool Loop;
            public readonly int Samples;

            public ActionRule(string actionName, bool loop, int samples)
            {
                ActionName = actionName;
                Loop = loop;
                Samples = samples;
            }
        }

        private sealed class SpriteNameComparer : IComparer<Sprite>
        {
            public static readonly SpriteNameComparer Instance = new SpriteNameComparer();
            private static readonly Regex NumberRegex = new Regex(@"\d+", RegexOptions.Compiled);

            public int Compare(Sprite x, Sprite y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x == null)
                {
                    return -1;
                }

                if (y == null)
                {
                    return 1;
                }

                string nx = Normalize(x.name);
                string ny = Normalize(y.name);
                int cmp = StringComparer.OrdinalIgnoreCase.Compare(nx, ny);
                if (cmp != 0)
                {
                    return cmp;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(x.name, y.name);
            }

            private static string Normalize(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                return NumberRegex.Replace(value, m => m.Value.PadLeft(8, '0'));
            }
        }
    }
}
#endif
