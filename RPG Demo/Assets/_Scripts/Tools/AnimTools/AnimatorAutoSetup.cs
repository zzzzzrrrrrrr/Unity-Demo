// Path: Assets/_Scripts/Tools/AnimTools/AnimatorAutoSetup.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ARPGDemo.Tools.AnimTools
{
    /// <summary>
    public static class AnimatorAutoSetup
    {
        private const string MenuPath = "Tools/ARPG Tools/AnimTools/Auto Setup Full Locomotion";
        private const float MoveThreshold = 0.1f;

        private enum ClipKey
        {
            Idle,
            Run,
            Jump,
            Attack,
            Hurt,
            Death
        }

        [MenuItem(MenuPath, false, 1100)]
        public static void Generate()
        {
            try
            {
                string folder = ResolveSelectedFolder();
                if (string.IsNullOrEmpty(folder))
                {
                    EditorUtility.DisplayDialog("Animator Auto Setup", "Please select an animation-clip folder in Project view.", "OK");
                    return;
                }

                Dictionary<ClipKey, AnimationClip> clips = CollectClips(folder);
                AnimatorController controller = CreateController(folder);
                if (controller == null)
                {
                    EditorUtility.DisplayDialog("Animator Auto Setup", "Failed to create controller. Check folder permission.", "OK");
                    return;
                }

                BuildStateMachine(controller, folder, clips);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = controller;
                EditorGUIUtility.PingObject(controller);
                EditorUtility.DisplayDialog("Animator Auto Setup", $"Controller generated:\n{AssetDatabase.GetAssetPath(controller)}", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorAutoSetup] Execution failed: {ex}");
                EditorUtility.DisplayDialog("Animator Auto Setup", "Execution failed. Check Console.", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateGenerate() => !string.IsNullOrEmpty(ResolveSelectedFolder());

        private static string ResolveSelectedFolder()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            string dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? string.Empty : dir.Replace("\\", "/");
        }

        private static Dictionary<ClipKey, AnimationClip> CollectClips(string folder)
        {
            Dictionary<ClipKey, AnimationClip> result = new Dictionary<ClipKey, AnimationClip>(6);
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            List<AnimationClip> all = new List<AnimationClip>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    all.Add(clip);
                }
            }

            TryMapByKeyword(all, result, ClipKey.Idle, new[] { "idle", "stand", "wait" });
            TryMapByKeyword(all, result, ClipKey.Run, new[] { "run", "move", "walk" });
            TryMapByKeyword(all, result, ClipKey.Jump, new[] { "jump", "air" });
            TryMapByKeyword(all, result, ClipKey.Attack, new[] { "attack", "atk", "slash" });
            TryMapByKeyword(all, result, ClipKey.Hurt, new[] { "hurt", "hit", "damage" });
            TryMapByKeyword(all, result, ClipKey.Death, new[] { "death", "die", "dead" });

            AnimationClip fallback = all.Count > 0 ? all[0] : null;
            foreach (ClipKey key in Enum.GetValues(typeof(ClipKey)))
            {
                if (!result.ContainsKey(key) || result[key] == null)
                {
                    result[key] = fallback;
                }
            }

            return result;
        }

        private static void TryMapByKeyword(List<AnimationClip> clips, Dictionary<ClipKey, AnimationClip> map, ClipKey key, string[] keywords)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (n.Contains(keywords[k]))
                    {
                        map[key] = clips[i];
                        return;
                    }
                }
            }
        }

        private static AnimatorController CreateController(string folder)
        {
            string folderName = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = "AutoAnimator";
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{folderName}_FullLocomotion.controller");
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        private static void BuildStateMachine(AnimatorController controller, string folder, Dictionary<ClipKey, AnimationClip> clips)
        {
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);
            AddParameters(controller);

            AnimatorState idle = sm.AddState("Idle", new Vector3(120f, 100f, 0f));
            AnimatorState run = sm.AddState("Run", new Vector3(320f, 100f, 0f));
            AnimatorState jump = sm.AddState("Jump", new Vector3(220f, 260f, 0f));
            AnimatorState attack = sm.AddState("Attack", new Vector3(560f, 80f, 0f));
            AnimatorState hurt = sm.AddState("Hurt", new Vector3(560f, 220f, 0f));
            AnimatorState death = sm.AddState("Death", new Vector3(760f, 150f, 0f));

            idle.motion = EnsureClip(folder, "Idle_Placeholder", clips[ClipKey.Idle]);
            run.motion = EnsureClip(folder, "Run_Placeholder", clips[ClipKey.Run]);
            jump.motion = EnsureClip(folder, "Jump_Placeholder", clips[ClipKey.Jump]);
            attack.motion = EnsureClip(folder, "Attack_Placeholder", clips[ClipKey.Attack]);
            hurt.motion = EnsureClip(folder, "Hurt_Placeholder", clips[ClipKey.Hurt]);
            death.motion = EnsureClip(folder, "Death_Placeholder", clips[ClipKey.Death]);
            sm.defaultState = idle;

            CreateMoveTransition(idle, run, true);
            CreateMoveTransition(run, idle, false);

            CreateGroundTransition(idle, jump, false);
            CreateGroundTransition(run, jump, false);

            CreateJumpLandTransition(jump, idle, false);
            CreateJumpLandTransition(jump, run, true);

            AnimatorStateTransition anyToAttack = sm.AddAnyStateTransition(attack);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0.02f;
            anyToAttack.canTransitionToSelf = false;
            AddCondition(anyToAttack, AnimatorConditionMode.If, 0f, "Attack");
            AddCondition(anyToAttack, AnimatorConditionMode.IfNot, 0f, "Dead");

            AnimatorStateTransition anyToHurt = sm.AddAnyStateTransition(hurt);
            anyToHurt.hasExitTime = false;
            anyToHurt.duration = 0.02f;
            anyToHurt.canTransitionToSelf = false;
            AddCondition(anyToHurt, AnimatorConditionMode.If, 0f, "Hurt");
            AddCondition(anyToHurt, AnimatorConditionMode.IfNot, 0f, "Dead");

            AnimatorStateTransition anyToDeathByTrigger = sm.AddAnyStateTransition(death);
            anyToDeathByTrigger.hasExitTime = false;
            anyToDeathByTrigger.duration = 0.02f;
            anyToDeathByTrigger.canTransitionToSelf = false;
            AddCondition(anyToDeathByTrigger, AnimatorConditionMode.If, 0f, "Die");

            AnimatorStateTransition anyToDeathByBool = sm.AddAnyStateTransition(death);
            anyToDeathByBool.hasExitTime = false;
            anyToDeathByBool.duration = 0.02f;
            anyToDeathByBool.canTransitionToSelf = false;
            AddCondition(anyToDeathByBool, AnimatorConditionMode.If, 0f, "Dead");

            CreateExitTransition(attack, idle, run);
            CreateExitTransition(hurt, idle, run);
        }

        private static void CreateMoveTransition(AnimatorState from, AnimatorState to, bool moving)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.08f;
            AddCondition(t, moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, MoveThreshold, "Speed");
            AddCondition(t, AnimatorConditionMode.If, 0f, "Grounded");
        }

        private static void CreateGroundTransition(AnimatorState from, AnimatorState to, bool grounded)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.05f;
            AddCondition(t, grounded ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "Grounded");
        }

        private static void CreateJumpLandTransition(AnimatorState from, AnimatorState to, bool moving)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.06f;
            AddCondition(t, AnimatorConditionMode.If, 0f, "Grounded");
            AddCondition(t, moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, MoveThreshold, "Speed");
        }

        private static void CreateExitTransition(AnimatorState from, AnimatorState idle, AnimatorState run)
        {
            AnimatorStateTransition toIdle = from.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.95f;
            toIdle.duration = 0.04f;
            AddCondition(toIdle, AnimatorConditionMode.Less, MoveThreshold, "Speed");
            AddCondition(toIdle, AnimatorConditionMode.If, 0f, "Grounded");
            AddCondition(toIdle, AnimatorConditionMode.IfNot, 0f, "Dead");

            AnimatorStateTransition toRun = from.AddTransition(run);
            toRun.hasExitTime = true;
            toRun.exitTime = 0.95f;
            toRun.duration = 0.04f;
            AddCondition(toRun, AnimatorConditionMode.Greater, MoveThreshold, "Speed");
            AddCondition(toRun, AnimatorConditionMode.If, 0f, "Grounded");
            AddCondition(toRun, AnimatorConditionMode.IfNot, 0f, "Dead");
        }

        private static void AddParameters(AnimatorController controller)
        {
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] ps = controller.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == name)
                {
                    return;
                }
            }

            controller.AddParameter(name, type);
        }

        private static void AddCondition(AnimatorStateTransition transition, AnimatorConditionMode mode, float threshold, string parameter)
        {
            if (transition != null && !string.IsNullOrEmpty(parameter))
            {
                transition.AddCondition(mode, threshold, parameter);
            }
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                sm.RemoveState(states[i].state);
            }

            AnimatorStateTransition[] any = sm.anyStateTransitions;
            for (int i = any.Length - 1; i >= 0; i--)
            {
                sm.RemoveAnyStateTransition(any[i]);
            }
        }

        private static AnimationClip EnsureClip(string folder, string placeholderName, AnimationClip clip)
        {
            if (clip != null)
            {
                return clip;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{placeholderName}.anim");
            AnimationClip created = new AnimationClip();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
#endif
