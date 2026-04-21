// Path: Assets/_Scripts/Tools/AnimatorAutoSetup.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ARPGDemo.Tools
{
    /// <summary>
    public static class AnimatorAutoSetup
    {
        private const string MenuPath = "Tools/ARPG Tools/Animator/Auto Setup 3-Hit Combo";

        private const string ParamIsMoving = "IsMoving";
        private const string ParamIsGrounded = "IsGrounded";
        private const string ParamIsDead = "IsDead";

        private const string ParamAttack1 = "Attack1";
        private const string ParamAttack2 = "Attack2";
        private const string ParamAttack3 = "Attack3";
        private const string ParamHurt = "Hurt";
        private const string ParamDie = "Die";

        private enum StateKey
        {
            Idle,
            Run,
            Jump,
            AttackGeneric,
            Attack1,
            Attack2,
            Attack3,
            Hurt,
            Death
        }

        [MenuItem(MenuPath, false, 1001)]
        public static void GenerateAnimatorController()
        {
            try
            {
                string folderPath = ResolveSelectedFolderPath();
                if (string.IsNullOrEmpty(folderPath))
                {
                    EditorUtility.DisplayDialog("Animator Auto Setup", "Please select an animation-clip folder in Project window.", "OK");
                    return;
                }

                Dictionary<StateKey, AnimationClip> clips = CollectAndMapClips(folderPath);
                AnimatorController controller = CreateController(folderPath);
                if (controller == null)
                {
                    EditorUtility.DisplayDialog("Animator Auto Setup", "Failed to create Animator Controller. Check path and permissions.", "OK");
                    return;
                }

                BuildStateMachine(controller, clips, folderPath);
                AutoBindAttackAnimationEvents(clips);

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
                EditorUtility.DisplayDialog("Animator Auto Setup", "Auto generation failed. See Console for details.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateGenerateAnimatorController()
        {
            return !string.IsNullOrEmpty(ResolveSelectedFolderPath());
        }

        private static string ResolveSelectedFolderPath()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
            {
                return string.Empty;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return string.Empty;
            }

            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath;
            }

            string dir = Path.GetDirectoryName(selectedPath);
            if (string.IsNullOrEmpty(dir))
            {
                return string.Empty;
            }

            return dir.Replace("\\", "/");
        }

        private static Dictionary<StateKey, AnimationClip> CollectAndMapClips(string folderPath)
        {
            Dictionary<StateKey, AnimationClip> result = new Dictionary<StateKey, AnimationClip>(9);
            string[] clipGuids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folderPath });
            List<AnimationClip> allClips = new List<AnimationClip>(clipGuids.Length);

            for (int i = 0; i < clipGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    continue;
                }

                if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                allClips.Add(clip);
            }

            TryMapByKeywords(allClips, result, StateKey.Run, new[] { "run", "move", "walk" });
            TryMapByKeywords(allClips, result, StateKey.Jump, new[] { "jump", "air" });
            TryMapByKeywords(allClips, result, StateKey.Hurt, new[] { "hurt", "hit", "damage" });
            TryMapByKeywords(allClips, result, StateKey.Death, new[] { "death", "die", "dead" });

            TryMapByKeywords(allClips, result, StateKey.AttackGeneric, new[] { "attack", "atk", "slash" });
            TryMapByKeywords(allClips, result, StateKey.Attack1, new[] { "attack1", "atk1", "light1", "slash1", "a1" });
            TryMapByKeywords(allClips, result, StateKey.Attack2, new[] { "attack2", "atk2", "light2", "slash2", "a2" });
            TryMapByKeywords(allClips, result, StateKey.Attack3, new[] { "attack3", "atk3", "heavy", "slash3", "a3" });

            AnimationClip fallback = allClips.Count > 0 ? allClips[0] : null;

            AnimationClip genericAttack = GetOrDefault(result, StateKey.AttackGeneric, fallback);
            if (!result.ContainsKey(StateKey.Attack1) || result[StateKey.Attack1] == null)
            {
                result[StateKey.Attack1] = genericAttack;
            }

            if (!result.ContainsKey(StateKey.Attack2) || result[StateKey.Attack2] == null)
            {
                result[StateKey.Attack2] = result[StateKey.Attack1] != null ? result[StateKey.Attack1] : genericAttack;
            }

            if (!result.ContainsKey(StateKey.Attack3) || result[StateKey.Attack3] == null)
            {
                result[StateKey.Attack3] = genericAttack;
            }

            foreach (StateKey key in Enum.GetValues(typeof(StateKey)))
            {
                if (!result.ContainsKey(key) || result[key] == null)
                {
                    result[key] = fallback;
                }
            }

            return result;
        }

        private static AnimationClip GetOrDefault(Dictionary<StateKey, AnimationClip> map, StateKey key, AnimationClip fallback)
        {
            if (map.TryGetValue(key, out AnimationClip clip) && clip != null)
            {
                return clip;
            }

            return fallback;
        }

        private static void TryMapByKeywords(
            List<AnimationClip> clips,
            Dictionary<StateKey, AnimationClip> map,
            StateKey stateKey,
            string[] keywords)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                string name = clips[i].name.ToLowerInvariant();
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (name.Contains(keywords[k]))
                    {
                        map[stateKey] = clips[i];
                        return;
                    }
                }
            }
        }

        private static AnimatorController CreateController(string folderPath)
        {
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = "AutoAnimator";
            }

            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{folderName}_Auto.controller");
            return AnimatorController.CreateAnimatorControllerAtPath(targetPath);
        }

        private static void BuildStateMachine(
            AnimatorController controller,
            Dictionary<StateKey, AnimationClip> clips,
            string folderPath)
        {
            if (controller == null)
            {
                return;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            ClearStateMachine(sm);
            AddParameters(controller);

            AnimationClip idleClip = EnsureClip(folderPath, "Idle_Placeholder", clips[StateKey.Idle]);
            AnimationClip runClip = EnsureClip(folderPath, "Run_Placeholder", clips[StateKey.Run]);
            AnimationClip jumpClip = EnsureClip(folderPath, "Jump_Placeholder", clips[StateKey.Jump]);
            AnimationClip atk1Clip = EnsureClip(folderPath, "Attack1_Placeholder", clips[StateKey.Attack1]);
            AnimationClip atk2Clip = EnsureClip(folderPath, "Attack2_Placeholder", clips[StateKey.Attack2]);
            AnimationClip atk3Clip = EnsureClip(folderPath, "Attack3_Placeholder", clips[StateKey.Attack3]);
            AnimationClip hurtClip = EnsureClip(folderPath, "Hurt_Placeholder", clips[StateKey.Hurt]);
            AnimationClip deathClip = EnsureClip(folderPath, "Death_Placeholder", clips[StateKey.Death]);

            AnimatorState idle = sm.AddState("Idle", new Vector3(120f, 120f, 0f));
            AnimatorState run = sm.AddState("Run", new Vector3(360f, 120f, 0f));
            AnimatorState jump = sm.AddState("Jump", new Vector3(240f, 300f, 0f));
            AnimatorState attack1 = sm.AddState("Attack1", new Vector3(620f, 30f, 0f));
            AnimatorState attack2 = sm.AddState("Attack2", new Vector3(820f, 30f, 0f));
            AnimatorState attack3 = sm.AddState("Attack3", new Vector3(1020f, 30f, 0f));
            AnimatorState hurt = sm.AddState("Hurt", new Vector3(760f, 240f, 0f));
            AnimatorState death = sm.AddState("Death", new Vector3(1020f, 240f, 0f));

            idle.motion = idleClip;
            run.motion = runClip;
            jump.motion = jumpClip;
            attack1.motion = atk1Clip;
            attack2.motion = atk2Clip;
            attack3.motion = atk3Clip;
            hurt.motion = hurtClip;
            death.motion = deathClip;

            sm.defaultState = idle;

            AnimatorStateTransition idleToRun = idle.AddTransition(run);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.08f;
            AddCondition(idleToRun, AnimatorConditionMode.If, 0f, ParamIsMoving);
            AddCondition(idleToRun, AnimatorConditionMode.If, 0f, ParamIsGrounded);

            AnimatorStateTransition runToIdle = run.AddTransition(idle);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.08f;
            AddCondition(runToIdle, AnimatorConditionMode.IfNot, 0f, ParamIsMoving);
            AddCondition(runToIdle, AnimatorConditionMode.If, 0f, ParamIsGrounded);

            AnimatorStateTransition idleToJump = idle.AddTransition(jump);
            idleToJump.hasExitTime = false;
            idleToJump.duration = 0.05f;
            AddCondition(idleToJump, AnimatorConditionMode.IfNot, 0f, ParamIsGrounded);

            AnimatorStateTransition runToJump = run.AddTransition(jump);
            runToJump.hasExitTime = false;
            runToJump.duration = 0.05f;
            AddCondition(runToJump, AnimatorConditionMode.IfNot, 0f, ParamIsGrounded);

            AnimatorStateTransition jumpToIdle = jump.AddTransition(idle);
            jumpToIdle.hasExitTime = false;
            jumpToIdle.duration = 0.08f;
            AddCondition(jumpToIdle, AnimatorConditionMode.If, 0f, ParamIsGrounded);
            AddCondition(jumpToIdle, AnimatorConditionMode.IfNot, 0f, ParamIsMoving);

            AnimatorStateTransition jumpToRun = jump.AddTransition(run);
            jumpToRun.hasExitTime = false;
            jumpToRun.duration = 0.08f;
            AddCondition(jumpToRun, AnimatorConditionMode.If, 0f, ParamIsGrounded);
            AddCondition(jumpToRun, AnimatorConditionMode.If, 0f, ParamIsMoving);

            AnimatorStateTransition anyToAttack1 = sm.AddAnyStateTransition(attack1);
            anyToAttack1.hasExitTime = false;
            anyToAttack1.duration = 0f;
            AddCondition(anyToAttack1, AnimatorConditionMode.If, 0f, ParamAttack1);
            AddCondition(anyToAttack1, AnimatorConditionMode.IfNot, 0f, ParamIsDead);

            AnimatorStateTransition a1ToA2 = attack1.AddTransition(attack2);
            a1ToA2.hasExitTime = false;
            a1ToA2.duration = 0f;
            AddCondition(a1ToA2, AnimatorConditionMode.If, 0f, ParamAttack2);
            AddCondition(a1ToA2, AnimatorConditionMode.IfNot, 0f, ParamIsDead);

            AnimatorStateTransition a2ToA3 = attack2.AddTransition(attack3);
            a2ToA3.hasExitTime = false;
            a2ToA3.duration = 0f;
            AddCondition(a2ToA3, AnimatorConditionMode.If, 0f, ParamAttack3);
            AddCondition(a2ToA3, AnimatorConditionMode.IfNot, 0f, ParamIsDead);

            CreateExitToLocomotionTransitions(attack2, idle, run);
            CreateExitToLocomotionTransitions(attack3, idle, run);
            CreateExitToLocomotionTransitions(hurt, idle, run);

            AnimatorStateTransition anyToHurt = sm.AddAnyStateTransition(hurt);
            anyToHurt.hasExitTime = false;
            anyToHurt.duration = 0.02f;
            AddCondition(anyToHurt, AnimatorConditionMode.If, 0f, ParamHurt);
            AddCondition(anyToHurt, AnimatorConditionMode.IfNot, 0f, ParamIsDead);

            AnimatorStateTransition anyToDeathByTrigger = sm.AddAnyStateTransition(death);
            anyToDeathByTrigger.hasExitTime = false;
            anyToDeathByTrigger.duration = 0.02f;
            AddCondition(anyToDeathByTrigger, AnimatorConditionMode.If, 0f, ParamDie);

            AnimatorStateTransition anyToDeathByBool = sm.AddAnyStateTransition(death);
            anyToDeathByBool.hasExitTime = false;
            anyToDeathByBool.duration = 0.02f;
            AddCondition(anyToDeathByBool, AnimatorConditionMode.If, 0f, ParamIsDead);
        }

        private static void CreateExitToLocomotionTransitions(AnimatorState from, AnimatorState idle, AnimatorState run)
        {
            if (from == null)
            {
                return;
            }

            AnimatorStateTransition toIdle = from.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.98f;
            toIdle.duration = 0.05f;
            AddCondition(toIdle, AnimatorConditionMode.IfNot, 0f, ParamIsMoving);
            AddCondition(toIdle, AnimatorConditionMode.If, 0f, ParamIsGrounded);
            AddCondition(toIdle, AnimatorConditionMode.IfNot, 0f, ParamIsDead);

            AnimatorStateTransition toRun = from.AddTransition(run);
            toRun.hasExitTime = true;
            toRun.exitTime = 0.98f;
            toRun.duration = 0.05f;
            AddCondition(toRun, AnimatorConditionMode.If, 0f, ParamIsMoving);
            AddCondition(toRun, AnimatorConditionMode.If, 0f, ParamIsGrounded);
            AddCondition(toRun, AnimatorConditionMode.IfNot, 0f, ParamIsDead);
        }

        private static void AddCondition(
            AnimatorStateTransition transition,
            AnimatorConditionMode mode,
            float threshold,
            string parameter)
        {
            if (transition == null || string.IsNullOrEmpty(parameter))
            {
                return;
            }

            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddParameters(AnimatorController controller)
        {
            EnsureParameter(controller, ParamIsMoving, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, ParamIsGrounded, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, ParamIsDead, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, ParamAttack1, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, ParamAttack2, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, ParamAttack3, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, ParamHurt, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, ParamDie, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            if (controller == null || string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == parameterName)
                {
                    return;
                }
            }

            controller.AddParameter(parameterName, parameterType);
        }

        private static void AutoBindAttackAnimationEvents(Dictionary<StateKey, AnimationClip> clips)
        {
            BindAttackEventsSafely(GetClip(clips, StateKey.Attack1), 1);
            BindAttackEventsSafely(GetClip(clips, StateKey.Attack2), 2);
            BindAttackEventsSafely(GetClip(clips, StateKey.Attack3), 3);
        }

        private static AnimationClip GetClip(Dictionary<StateKey, AnimationClip> clips, StateKey key)
        {
            if (clips != null && clips.TryGetValue(key, out AnimationClip clip))
            {
                return clip;
            }

            return null;
        }

        private static void BindAttackEventsSafely(AnimationClip clip, int stageIndex)
        {
            if (clip == null)
            {
                return;
            }

            try
            {
                AnimationEvent[] oldEvents = AnimationUtility.GetAnimationEvents(clip);
                List<AnimationEvent> retained = new List<AnimationEvent>();

                for (int i = 0; i < oldEvents.Length; i++)
                {
                    string fn = oldEvents[i].functionName;
                    bool isManaged =
                        fn == "AnimEvent_BeginAttackWindow" ||
                        fn == "AnimEvent_AttackHit" ||
                        fn == "AnimEvent_EndAttackWindow" ||
                        fn == "AnimEvent_BeginRecovery" ||
                        fn == "AnimEvent_AttackSfx";

                    if (!isManaged)
                    {
                        retained.Add(oldEvents[i]);
                    }
                }

                float length = Mathf.Max(0.01f, clip.length);
                float tBegin = length * 0.24f;
                float tHit = length * 0.35f;
                float tRecovery = length * 0.58f;
                float tEnd = length * 0.8f;

                retained.Add(CreateEvent("AnimEvent_BeginAttackWindow", tBegin, stageIndex));
                retained.Add(CreateEvent("AnimEvent_AttackHit", tHit, stageIndex));
                retained.Add(CreateEvent("AnimEvent_AttackSfx", tHit, stageIndex));
                retained.Add(CreateEvent("AnimEvent_BeginRecovery", tRecovery, stageIndex));
                retained.Add(CreateEvent("AnimEvent_EndAttackWindow", tEnd, stageIndex));

                AnimationUtility.SetAnimationEvents(clip, retained.ToArray());
                EditorUtility.SetDirty(clip);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnimatorAutoSetup] Failed to bind animation event on clip {clip.name}: {ex.Message}");
            }
        }

        private static AnimationEvent CreateEvent(string functionName, float time, int stage)
        {
            AnimationEvent evt = new AnimationEvent();
            evt.functionName = functionName;
            evt.time = Mathf.Max(0f, time);
            evt.intParameter = stage;
            return evt;
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            if (sm == null)
            {
                return;
            }

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

            AnimatorTransition[] entryTransitions = sm.entryTransitions;
            for (int i = entryTransitions.Length - 1; i >= 0; i--)
            {
                sm.RemoveEntryTransition(entryTransitions[i]);
            }
        }

        private static AnimationClip EnsureClip(string folderPath, string clipName, AnimationClip clip)
        {
            if (clip != null)
            {
                return clip;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{clipName}.anim");
            AnimationClip newClip = new AnimationClip();
            AssetDatabase.CreateAsset(newClip, path);
            return newClip;
        }
    }
}
#endif
