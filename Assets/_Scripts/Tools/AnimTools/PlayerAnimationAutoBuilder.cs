// Path: Assets/_Scripts/Tools/AnimTools/PlayerAnimationAutoBuilder.cs
// Function: One-click build for player Sprite animations and Animator Controller.
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
    public static class PlayerAnimationAutoBuilder
    {
        private const string MenuPath = "Tools/ARPG Tools/AnimTools/Build Player Clips + Controller";
        private const string PlayerRoot = "Assets/_Anim/Player";
        private const string OutputFolder = PlayerRoot + "/Generated";
        private const string ControllerAssetPath = OutputFolder + "/Player_Auto.controller";

        private static readonly ActionClipConfig[] ClipConfigs =
        {
            new ActionClipConfig("Idle", true, 10f, new Vector3(100f, 120f, 0f)),
            new ActionClipConfig("Attack1", false, 12f, new Vector3(560f, 20f, 0f)),
            new ActionClipConfig("Attack2", false, 12f, new Vector3(760f, 20f, 0f)),
            new ActionClipConfig("Attack3", false, 12f, new Vector3(960f, 20f, 0f)),
            new ActionClipConfig("Hurt", false, 10f, new Vector3(760f, 220f, 0f)),
            new ActionClipConfig("Death", false, 10f, new Vector3(980f, 240f, 0f))
        };

        [MenuItem(MenuPath, false, 1201)]
        public static void Build()
        {
            try
            {
                EnsureFolders();

                Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(ClipConfigs.Length);
                List<string> warnings = new List<string>(8);
                for (int i = 0; i < ClipConfigs.Length; i++)
                {
                    ActionClipConfig config = ClipConfigs[i];
                    AnimationClip clip = CreateOrUpdateClip(config, warnings);
                    clips[config.ActionName] = clip;
                }

                AnimatorController controller = CreateController(clips);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string summary = BuildSummary(clips, controller, warnings);
                Debug.Log(summary);
                EditorUtility.DisplayDialog("Player Auto Builder", "Build finished. See Console for details and usage.", "OK");
                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlayerAnimationAutoBuilder] Build failed: " + ex);
                EditorUtility.DisplayDialog("Player Auto Builder", "Build failed. Check Console.", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuild()
        {
            return AssetDatabase.IsValidFolder(PlayerRoot);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PlayerRoot))
            {
                throw new InvalidOperationException("Missing folder: " + PlayerRoot);
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder(PlayerRoot, "Generated");
            }
        }

        private static AnimationClip CreateOrUpdateClip(ActionClipConfig config, List<string> warnings)
        {
            string actionFolder = PlayerRoot + "/" + config.ActionName;
            string clipPath = OutputFolder + "/Player_" + config.ActionName + ".anim";

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = Mathf.Max(1f, config.Fps);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };

            if (!AssetDatabase.IsValidFolder(actionFolder))
            {
                warnings.Add("- Missing action folder: " + actionFolder + " (empty clip generated).");
                AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, Array.Empty<ObjectReferenceKeyframe>());
                SetLoop(clip, config.Loop);
                EditorUtility.SetDirty(clip);
                return clip;
            }

            List<Sprite> sprites = LoadSpritesSorted(actionFolder);
            if (sprites.Count == 0)
            {
                warnings.Add("- No sprites found in: " + actionFolder + " (empty clip generated).");
                AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, Array.Empty<ObjectReferenceKeyframe>());
                SetLoop(clip, config.Loop);
                EditorUtility.SetDirty(clip);
                return clip;
            }

            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Count];
            float frameRate = Mathf.Max(1f, config.Fps);
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / frameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keys);
            SetLoop(clip, config.Loop);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static List<Sprite> LoadSpritesSorted(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            List<Sprite> result = new List<Sprite>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    result.Add(sprite);
                }
            }

            result.Sort(SpriteNameComparer.Instance);
            return result;
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            SerializedObject so = new SerializedObject(clip);
            SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");
            if (settings != null)
            {
                SerializedProperty loopTime = settings.FindPropertyRelative("m_LoopTime");
                if (loopTime != null)
                {
                    loopTime.boolValue = loop;
                }
            }

            so.ApplyModifiedProperties();
        }

        private static AnimatorController CreateController(Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerAssetPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerAssetPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);

            EnsureBool(controller, "IsMoving");
            EnsureTrigger(controller, "Attack1");
            EnsureTrigger(controller, "Attack2");
            EnsureTrigger(controller, "Attack3");
            EnsureTrigger(controller, "Hurt");
            EnsureTrigger(controller, "Die");

            AnimatorState idle = sm.AddState("Idle", ClipConfigs[0].Position);
            idle.motion = clips["Idle"];
            sm.defaultState = idle;

            AnimatorState move = sm.AddState("Move", new Vector3(300f, 120f, 0f));
            move.motion = clips["Idle"];

            AnimatorState attack1 = sm.AddState("Attack1", GetConfig("Attack1").Position);
            attack1.motion = clips["Attack1"];
            AnimatorState attack2 = sm.AddState("Attack2", GetConfig("Attack2").Position);
            attack2.motion = clips["Attack2"];
            AnimatorState attack3 = sm.AddState("Attack3", GetConfig("Attack3").Position);
            attack3.motion = clips["Attack3"];
            AnimatorState hurt = sm.AddState("Hurt", GetConfig("Hurt").Position);
            hurt.motion = clips["Hurt"];
            AnimatorState death = sm.AddState("Death", GetConfig("Death").Position);
            death.motion = clips["Death"];

            AddMoveTransitions(idle, move);

            AddAnyTriggerTransition(sm, attack1, "Attack1");
            AddAnyTriggerTransition(sm, attack2, "Attack2");
            AddAnyTriggerTransition(sm, attack3, "Attack3");
            AddAnyTriggerTransition(sm, hurt, "Hurt");
            AddAnyTriggerTransition(sm, death, "Die");

            AddExitToIdle(attack1, idle);
            AddExitToIdle(attack2, idle);
            AddExitToIdle(attack3, idle);
            AddExitToIdle(hurt, idle);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMoveTransitions(AnimatorState idle, AnimatorState move)
        {
            AnimatorStateTransition idleToMove = idle.AddTransition(move);
            idleToMove.hasExitTime = false;
            idleToMove.duration = 0.05f;
            idleToMove.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

            AnimatorStateTransition moveToIdle = move.AddTransition(idle);
            moveToIdle.hasExitTime = false;
            moveToIdle.duration = 0.05f;
            moveToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
        }

        private static void AddAnyTriggerTransition(AnimatorStateMachine sm, AnimatorState toState, string trigger)
        {
            AnimatorStateTransition transition = sm.AddAnyStateTransition(toState);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitToIdle(AnimatorState from, AnimatorState idle)
        {
            AnimatorStateTransition transition = from.AddTransition(idle);
            transition.hasExitTime = true;
            transition.exitTime = 0.98f;
            transition.duration = 0.05f;
        }

        private static void EnsureBool(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        }

        private static void EnsureTrigger(AnimatorController controller, string name)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                return;
            }

            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        private static ActionClipConfig GetConfig(string actionName)
        {
            for (int i = 0; i < ClipConfigs.Length; i++)
            {
                if (ClipConfigs[i].ActionName == actionName)
                {
                    return ClipConfigs[i];
                }
            }

            throw new InvalidOperationException("Missing clip config for action: " + actionName);
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

        private static string BuildSummary(Dictionary<string, AnimationClip> clips, AnimatorController controller, List<string> warnings)
        {
            StringBuilder sb = new StringBuilder(512);
            sb.AppendLine("[PlayerAnimationAutoBuilder] Build Done");
            sb.AppendLine("Generated Controller:");
            sb.AppendLine("- " + AssetDatabase.GetAssetPath(controller));
            sb.AppendLine("Generated Clips:");

            foreach (KeyValuePair<string, AnimationClip> pair in clips)
            {
                sb.AppendLine("- " + AssetDatabase.GetAssetPath(pair.Value));
            }

            sb.AppendLine("Usage:");
            sb.AppendLine("1) Assign this controller to Player Animator.");
            sb.AppendLine("2) Keep PlayerController trigger names: Attack1/Attack2/Attack3/Hurt/Die.");
            sb.AppendLine("3) Idle uses loop; Attack/Hurt/Death are non-loop clips.");

            if (warnings.Count > 0)
            {
                sb.AppendLine("Warnings:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    sb.AppendLine(warnings[i]);
                }
            }

            return sb.ToString();
        }

        private readonly struct ActionClipConfig
        {
            public readonly string ActionName;
            public readonly bool Loop;
            public readonly float Fps;
            public readonly Vector3 Position;

            public ActionClipConfig(string actionName, bool loop, float fps, Vector3 position)
            {
                ActionName = actionName;
                Loop = loop;
                Fps = fps;
                Position = position;
            }
        }

        private sealed class SpriteNameComparer : IComparer<Sprite>
        {
            public static readonly SpriteNameComparer Instance = new SpriteNameComparer();
            private static readonly Regex NumberRegex = new Regex(@"\d+", RegexOptions.Compiled);

            public int Compare(Sprite a, Sprite b)
            {
                if (ReferenceEquals(a, b))
                {
                    return 0;
                }

                if (a == null)
                {
                    return -1;
                }

                if (b == null)
                {
                    return 1;
                }

                string na = Normalize(a.name);
                string nb = Normalize(b.name);
                int c = StringComparer.OrdinalIgnoreCase.Compare(na, nb);
                if (c != 0)
                {
                    return c;
                }

                return StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name);
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
