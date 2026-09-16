using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Client
{
    [DisallowMultipleComponent]
    public sealed class CharacterTestPreviewDriver : MonoBehaviour
    {
        private static readonly string[] DefaultMotions =
        {
            // 서기
            "Idle",
            "Walk_Forward",
            "Walk_Back",
            "Walk_Left",
            "Walk_Right",
            "Run_Forward",
            "Run_Back",
            "Run_Left",
            "Run_Right",
            // 공중
            "Jump",
            "Fall",
            "Land",
            // 웅크리기
            "Crouch_Idle",
            "Crouch_Start",
            "Crouch_End",
            "Crouch_Walk_Forward",
            "Crouch_Walk_Back",
            "Crouch_Walk_Left",
            "Crouch_Walk_Right",
            // 엎드리기
            "Prone_Idle",
            "Prone_Start",
            "Prone_End",
            "Crawl_Forward",
            "Crawl_Back",
            "Crawl_Left",
            "Crawl_Right",
            "Crouch_To_Prone",
            "Prone_To_Crouch",
            // 들기
            "Carry_Idle",
            "Carry_Walk_Forward",
            "Carry_Walk_Back",
            "Carry_Walk_Left",
            "Carry_Walk_Right",
            "Carry_Run_Forward",
            "Carry_Run_Back",
            "Carry_Run_Left",
            "Carry_Run_Right",
            "Carry_Crouch_Idle",
            "Carry_Crouch_Walk_Forward",
            "Carry_Crouch_Walk_Back",
            "Carry_Crouch_Walk_Left",
            "Carry_Crouch_Walk_Right",
            "Carry_Prone_Idle",
            "Carry_Crawl_Forward",
            "Carry_Crawl_Back",
            "Carry_Crawl_Left",
            "Carry_Crawl_Right",
            "Carry_Jump",
            "Carry_Land",
            // 양손 들기
            "PutUp_TwoHands",
            "Carry_TwoHands",
            "Carry_TwoHands_Walk_Forward",
            "Carry_TwoHands_Walk_Back",
            "Carry_TwoHands_Walk_Left",
            "Carry_TwoHands_Walk_Right",
            "Carry_TwoHands_Run_Forward",
            "Carry_TwoHands_Run_Back",
            "Carry_TwoHands_Run_Left",
            "Carry_TwoHands_Run_Right",
            "Carry_TwoHands_Crouch_Idle",
            "Carry_TwoHands_Crouch_Walk_Forward",
            "Carry_TwoHands_Crouch_Walk_Back",
            "Carry_TwoHands_Crouch_Walk_Left",
            "Carry_TwoHands_Crouch_Walk_Right",
            "Carry_TwoHands_Prone_Idle",
            "Carry_TwoHands_Crawl_Forward",
            "Carry_TwoHands_Crawl_Back",
            "Carry_TwoHands_Crawl_Left",
            "Carry_TwoHands_Crawl_Right",
            "Carry_TwoHands_Jump",
            "Carry_TwoHands_Land",
            "Throw_TwoHands",
            "Throw_TwoHands_Walk",
            "Throw_TwoHands_Run",
            "Throw_TwoHands_Crouch",
            "Throw_TwoHands_Crouch_Walk",
            "Throw_TwoHands_Prone",
            "Throw_TwoHands_Crawl",
            "Carry_TwoHands_Crouch_Start",
            "Carry_TwoHands_Crouch_End",
            "Carry_TwoHands_Prone_Start",
            "Carry_TwoHands_Prone_End",
            "Carry_TwoHands_Crouch_To_Prone",
            "Carry_TwoHands_Prone_To_Crouch",
            "PutUp_TwoHands_Crouch",
            "PutDown_TwoHands_Crouch",
            "PutUp_TwoHands_Prone",
            "PutDown_TwoHands_Prone",
            "PutDown_TwoHands",
            // 집기·놓기
            "Pickup_Low",
            "Pickup_Crouch",
            "Pickup_Prone",
            "PutDown_Low",
            "PutDown_Crouch",
            "PutDown_Prone",
            "Throw",
            // 전투
            "Punch",
            "Punch_Walk",
            "Punch_Run",
            "Punch_Crouch",
            "Punch_Crouch_Walk",
            "Punch_Left",
            "Punch_Left_Walk",
            "Punch_Left_Run",
            "Punch_Left_Crouch",
            "Punch_Left_Crouch_Walk",
            "Punch_Combo",
            "Punch_Combo_Walk",
            "Punch_Combo_Run",
            "Punch_Combo_Crouch",
            "Punch_Combo_Crouch_Walk",
            "Hit",
            "Hit_Walk",
            "Hit_Run",
            "Hit_Crouch",
            "Hit_Crouch_Walk",
            "Hit_Prone",
            "Hit_Crawl",
            "Carry_TwoHands_Hit",
            "Carry_TwoHands_Hit_Walk",
            "Carry_TwoHands_Hit_Run",
            "Carry_TwoHands_Hit_Crouch",
            "Carry_TwoHands_Hit_Crouch_Walk",
            "Carry_TwoHands_Hit_Prone",
            "Carry_TwoHands_Hit_Crawl",
            "Stun_Start",
            "Stun_Idle",
            "Stun_End",
        };

        private static readonly string[] GroupOrder =
        {
            "들고 Hit",
            "서기",
            "공중",
            "웅크리기",
            "엎드리기",
            "들기",
            "양손 들기",
            "집기·놓기",
            "전투",
            "기타",
        };

        [SerializeField]
        private string[] stateNames = DefaultMotions;

        private Vector2 listScroll;
        private string filterText = string.Empty;
        private readonly Dictionary<string, bool> groupExpanded = new();

        private static readonly string[] FootNames =
        {
            "FootToe1.L",
            "FootToe1.R",
            "Foot.L",
            "Foot.R",
        };

        private readonly List<Transform> feet = new();
        private Animator animator;
        private SkinnedMeshRenderer body;
        private int crouchGroinIndex = -1;
        private Transform orbitCamera;
        private int index;
        private Vector3 standPosition;
        private Quaternion standRotation;
        private float plantedFootY = 0.02f;
        private bool planted;
        private float yaw;
        private float pitch;
        private float distance = 5f;
        private float startYaw;
        private float startPitch;
        private float startDistance = 5f;

        public void ConfigureMotions(params string[] names)
        {
            if (names == null || names.Length == 0)
            {
                throw new System.ArgumentException("At least one preview motion is required.", nameof(names));
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
            }

            stateNames = WithControllerMotions(animator, WithDefaultMotions(names));
            index = 0;
            EnsureGroupExpanded(CategoryOf(stateNames[0]));
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            CacheFeet(transform);
            CacheCrouchGroin();
            stateNames = WithControllerMotions(animator, WithDefaultMotions(stateNames));

            foreach (var title in GroupOrder)
            {
                groupExpanded[title] = title is "들고 Hit" or "전투";
            }

            standRotation = transform.rotation;
            standPosition = transform.position;
            CaptureOrbit();
            Play(0);
        }

        private void Start()
        {
            PlantIdleFeet();
            CaptureOrbit();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (WasPressed(keyboard.rightArrowKey) || WasPressed(keyboard.periodKey))
            {
                Play((index + 1) % stateNames.Length);
                return;
            }

            if (WasPressed(keyboard.leftArrowKey) || WasPressed(keyboard.commaKey))
            {
                Play((index + stateNames.Length - 1) % stateNames.Length);
                return;
            }

            if (TryDigit(keyboard, out var digit))
            {
                Play(digit);
            }

            UpdateOrbit(keyboard);
        }

        private void LateUpdate()
        {
            var pos = transform.position;
            pos.x = standPosition.x;
            pos.z = standPosition.z;
            transform.SetPositionAndRotation(pos, standRotation);

            if (TryMinFootY(out var footY))
            {
                if (!planted)
                {
                    PlantIdleFeet();
                }
                else if (IsAirborne(stateNames[index]))
                {
                    if (footY < plantedFootY)
                    {
                        transform.position += Vector3.up * (plantedFootY - footY);
                    }
                }
                else if (!KeepsFloorContact(stateNames[index]))
                {
                    transform.position += Vector3.up * (plantedFootY - footY);
                }
            }

            ApplyOrbit();
            ApplyStandingGroin();
        }

        private void Play(int next)
        {
            index = Mathf.Clamp(next, 0, stateNames.Length - 1);
            EnsureGroupExpanded(CategoryOf(stateNames[index]));
            if (animator == null)
            {
                return;
            }

            animator.Play(stateNames[index], 0, 0f);
            if (KeepsFloorContact(stateNames[index]))
            {
                var position = transform.position;
                position.y = standPosition.y;
                transform.position = position;
            }
            ApplyStandingGroin();
        }

        private void PlantIdleFeet()
        {
            if (!TryMinFootY(out var footY))
            {
                return;
            }

            transform.position += Vector3.up * (0.02f - footY);
            if (!TryMinFootY(out plantedFootY))
            {
                plantedFootY = 0.02f;
            }

            standPosition = transform.position;
            planted = true;
        }

        private void CacheCrouchGroin()
        {
            body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            crouchGroinIndex = -1;
            if (body == null || body.sharedMesh == null)
            {
                return;
            }

            for (var i = 0; i < body.sharedMesh.blendShapeCount; i++)
            {
                if (body.sharedMesh.GetBlendShapeName(i) == "Crouch_Groin_Flat")
                {
                    crouchGroinIndex = i;
                    return;
                }
            }
        }

        private void ApplyStandingGroin()
        {
            if (body == null || crouchGroinIndex < 0)
            {
                return;
            }

            if (stateNames[index].IndexOf("Crouch") < 0 &&
                stateNames[index] != "Stun_Start" &&
                stateNames[index] != "Stun_Idle")
            {
                body.SetBlendShapeWeight(crouchGroinIndex, 0f);
            }
        }

        private void CacheFeet(Transform current)
        {
            for (var i = 0; i < FootNames.Length; i++)
            {
                if (current.name == FootNames[i])
                {
                    feet.Add(current);
                    break;
                }
            }

            for (var i = 0; i < current.childCount; i++)
            {
                CacheFeet(current.GetChild(i));
            }
        }

        private bool TryMinFootY(out float footY)
        {
            footY = float.PositiveInfinity;
            var found = false;
            for (var i = 0; i < feet.Count; i++)
            {
                if (feet[i] == null)
                {
                    continue;
                }

                footY = Mathf.Min(footY, feet[i].position.y);
                found = true;
            }

            return found;
        }

        private void CaptureOrbit()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            orbitCamera = camera.transform;
            var lookAt = LookAtPoint();
            var offset = orbitCamera.position - lookAt;
            distance = Mathf.Clamp(offset.magnitude, 1.5f, 12f);
            if (distance < 0.01f)
            {
                distance = 5f;
            }

            yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(Mathf.Clamp(offset.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -25f, 89f);
            startYaw = yaw;
            startPitch = pitch;
            startDistance = distance;
        }

        private void UpdateOrbit(Keyboard keyboard)
        {
            if (WasPressed(keyboard.rKey))
            {
                yaw = startYaw;
                pitch = startPitch;
                distance = startDistance;
            }

            var step = 90f * Time.deltaTime;
            if (keyboard.qKey.isPressed)
            {
                yaw -= step;
            }

            if (keyboard.eKey.isPressed)
            {
                yaw += step;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.15f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.15f, -25f, 89f);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance = Mathf.Clamp(distance * (1f - scroll * 0.001f), 1.5f, 12f);
            }
        }

        private void ApplyOrbit()
        {
            if (orbitCamera == null)
            {
                return;
            }

            var lookAt = LookAtPoint();
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var position = lookAt + rotation * (Vector3.forward * distance);
            orbitCamera.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        }

        private Vector3 LookAtPoint()
        {
            return standPosition + Vector3.up * 1.15f;
        }

        private static bool IsAirborne(string state)
        {
            return state == "Fall" ||
                   state.EndsWith("Jump", System.StringComparison.Ordinal);
        }

        private static bool KeepsFloorContact(string state)
        {
            return state.IndexOf("Prone", System.StringComparison.Ordinal) >= 0
                || state.IndexOf("Crawl", System.StringComparison.Ordinal) >= 0
                || state == "Knocked_Out"
                || state == "Stun_Start"
                || state == "Stun_Idle"
                || state == "Stun_End"
                || state == "Pickup_Prone"
                || state == "PutDown_Prone";
        }

        private static bool TryDigit(Keyboard keyboard, out int digit)
        {
            if (WasPressed(keyboard.digit1Key) || WasPressed(keyboard.numpad1Key)) { digit = 0; return true; }
            if (WasPressed(keyboard.digit2Key) || WasPressed(keyboard.numpad2Key)) { digit = 1; return true; }
            if (WasPressed(keyboard.digit3Key) || WasPressed(keyboard.numpad3Key)) { digit = 2; return true; }
            if (WasPressed(keyboard.digit4Key) || WasPressed(keyboard.numpad4Key)) { digit = 3; return true; }
            if (WasPressed(keyboard.digit5Key) || WasPressed(keyboard.numpad5Key)) { digit = 4; return true; }
            if (WasPressed(keyboard.digit6Key) || WasPressed(keyboard.numpad6Key)) { digit = 5; return true; }
            if (WasPressed(keyboard.digit7Key) || WasPressed(keyboard.numpad7Key)) { digit = 6; return true; }
            if (WasPressed(keyboard.digit8Key) || WasPressed(keyboard.numpad8Key)) { digit = 7; return true; }
            if (WasPressed(keyboard.digit9Key) || WasPressed(keyboard.numpad9Key)) { digit = 8; return true; }
            if (WasPressed(keyboard.digit0Key) || WasPressed(keyboard.numpad0Key)) { digit = 9; return true; }
            digit = -1;
            return false;
        }

        private static bool WasPressed(KeyControl key)
        {
            return key != null && key.wasPressedThisFrame;
        }

        private void OnGUI()
        {
            const int pad = 12;
            const int width = 380;
            const int header = 86;
            const int row = 24;
            const int groupRow = 22;
            var maxHeight = Mathf.Max(220, Screen.height - pad * 2);
            GUI.Box(new Rect(pad, pad, width, maxHeight), string.Empty);

            var ready = animator != null && animator.runtimeAnimatorController != null;
            var current = stateNames[index];
            GUI.Label(
                new Rect(pad + 8, pad + 4, width - 16, 36),
                $"지금 {current}  ({index + 1}/{stateNames.Length})\n" +
                $"{gameObject.name} {(ready ? "OK" : "없음")}  |  ← → 이동");

            GUI.Label(new Rect(pad + 8, pad + 42, 40, 20), "검색");
            filterText = GUI.TextField(new Rect(pad + 48, pad + 40, width - 120, 22), filterText ?? string.Empty);
            if (GUI.Button(new Rect(pad + width - 64, pad + 40, 52, 22), "지우기"))
            {
                filterText = string.Empty;
            }

            GUI.Label(
                new Rect(pad + 8, pad + 64, width - 16, 18),
                "우클릭 드래그 / Q E  |  휠 줌  |  R 리셋");

            var filter = filterText.Trim();
            var filtering = filter.Length > 0;
            var groups = BuildVisibleGroups(filter);
            var contentHeight = 0f;
            for (var g = 0; g < groups.Count; g++)
            {
                contentHeight += groupRow + 4;
                var title = groups[g].Title;
                var expanded = filtering || IsGroupExpanded(title);
                if (expanded)
                {
                    contentHeight += groups[g].Indices.Count * row;
                }
            }

            var scrollRect = new Rect(pad + 4, pad + header, width - 8, maxHeight - header - 8);
            var content = new Rect(0, 0, width - 28, Mathf.Max(contentHeight, scrollRect.height));
            listScroll = GUI.BeginScrollView(scrollRect, listScroll, content);

            var y = 0f;
            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                var expanded = filtering || IsGroupExpanded(group.Title);
                var label = $"{(expanded ? "▼" : "▶")}  {group.Title}  ({group.Indices.Count})";
                if (GUI.Button(new Rect(2, y, width - 36, groupRow), label))
                {
                    if (!filtering)
                    {
                        groupExpanded[group.Title] = !expanded;
                    }
                }

                y += groupRow + 2;
                if (!expanded)
                {
                    continue;
                }

                for (var i = 0; i < group.Indices.Count; i++)
                {
                    var motionIndex = group.Indices[i];
                    var selected = motionIndex == index;
                    var prev = GUI.backgroundColor;
                    if (selected)
                    {
                        GUI.backgroundColor = new Color(0.45f, 0.75f, 1f, 1f);
                    }

                    if (GUI.Button(
                            new Rect(10, y, width - 48, row - 2),
                            $"{motionIndex + 1}. {stateNames[motionIndex]}"))
                    {
                        Play(motionIndex);
                    }

                    GUI.backgroundColor = prev;
                    y += row;
                }
            }

            GUI.EndScrollView();
        }

        private List<(string Title, List<int> Indices)> BuildVisibleGroups(string filter)
        {
            var buckets = new Dictionary<string, List<int>>();
            for (var i = 0; i < GroupOrder.Length; i++)
            {
                buckets[GroupOrder[i]] = new List<int>();
            }

            for (var i = 0; i < stateNames.Length; i++)
            {
                var name = stateNames[i];
                if (!string.IsNullOrEmpty(filter) &&
                    name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var category = CategoryOf(name);
                if (!buckets.TryGetValue(category, out var list))
                {
                    list = buckets[category] = new List<int>();
                }

                list.Add(i);
            }

            var groups = new List<(string, List<int>)>();
            for (var i = 0; i < GroupOrder.Length; i++)
            {
                var title = GroupOrder[i];
                if (buckets[title].Count > 0)
                {
                    groups.Add((title, buckets[title]));
                }
            }

            return groups;
        }

        private bool IsGroupExpanded(string title)
        {
            if (!groupExpanded.TryGetValue(title, out var expanded))
            {
                groupExpanded[title] = false;
                return false;
            }

            return expanded;
        }

        private void EnsureGroupExpanded(string title)
        {
            groupExpanded[title] = true;
        }

        internal static string CategoryOf(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                return "기타";
            }

            if (state.StartsWith("Carry_TwoHands_Hit", System.StringComparison.Ordinal))
            {
                return "들고 Hit";
            }

            if (state.StartsWith("Punch", System.StringComparison.Ordinal) ||
                state.StartsWith("Hit", System.StringComparison.Ordinal) ||
                state.StartsWith("Stun", System.StringComparison.Ordinal))
            {
                return "전투";
            }

            if (state.StartsWith("Carry_TwoHands", System.StringComparison.Ordinal) ||
                state.StartsWith("PutUp_TwoHands", System.StringComparison.Ordinal) ||
                state.StartsWith("PutDown_TwoHands", System.StringComparison.Ordinal) ||
                state.StartsWith("Throw_TwoHands", System.StringComparison.Ordinal))
            {
                return "양손 들기";
            }

            if (state.StartsWith("Carry", System.StringComparison.Ordinal))
            {
                return "들기";
            }

            if (state.StartsWith("Pickup", System.StringComparison.Ordinal) ||
                state.StartsWith("PutDown", System.StringComparison.Ordinal) ||
                state == "Throw")
            {
                return "집기·놓기";
            }

            if (state.StartsWith("Crouch", System.StringComparison.Ordinal))
            {
                return "웅크리기";
            }

            if (state.StartsWith("Prone", System.StringComparison.Ordinal) ||
                state.StartsWith("Crawl", System.StringComparison.Ordinal))
            {
                return "엎드리기";
            }

            if (state is "Jump" or "Fall" or "Land")
            {
                return "공중";
            }

            if (state == "Idle" ||
                state.StartsWith("Walk", System.StringComparison.Ordinal) ||
                state.StartsWith("Run", System.StringComparison.Ordinal))
            {
                return "서기";
            }

            return "기타";
        }

        private static string[] SortByCategory(string[] names)
        {
            var ranked = new Dictionary<string, int>();
            for (var i = 0; i < GroupOrder.Length; i++)
            {
                ranked[GroupOrder[i]] = i;
            }

            var defaultRank = new Dictionary<string, int>(DefaultMotions.Length);
            for (var i = 0; i < DefaultMotions.Length; i++)
            {
                defaultRank[DefaultMotions[i]] = i;
            }

            var copy = (string[])names.Clone();
            System.Array.Sort(copy, (a, b) =>
            {
                var ca = ranked.TryGetValue(CategoryOf(a), out var ra) ? ra : 99;
                var cb = ranked.TryGetValue(CategoryOf(b), out var rb) ? rb : 99;
                var byCategory = ca.CompareTo(cb);
                if (byCategory != 0)
                {
                    return byCategory;
                }

                var da = defaultRank.TryGetValue(a, out var ia) ? ia : 10_000;
                var db = defaultRank.TryGetValue(b, out var ib) ? ib : 10_000;
                var byDefault = da.CompareTo(db);
                return byDefault != 0
                    ? byDefault
                    : string.CompareOrdinal(a, b);
            });
            return copy;
        }

        private static string[] WithDefaultMotions(string[] names)
        {
            var merged = new List<string>();
            var seen = new HashSet<string>();
            if (names != null)
            {
                for (var i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    {
                        continue;
                    }

                    merged.Add(name);
                }
            }

            for (var i = 0; i < DefaultMotions.Length; i++)
            {
                var name = DefaultMotions[i];
                if (seen.Add(name))
                {
                    merged.Add(name);
                }
            }

            return SortByCategory(merged.ToArray());
        }

        private static string[] WithControllerMotions(Animator target, string[] names)
        {
            if (target == null || target.runtimeAnimatorController == null)
            {
                return names;
            }

            var clips = target.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0)
            {
                return names;
            }

            var merged = new List<string>(names);
            var seen = new HashSet<string>(names);
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || !clip.name.StartsWith("Carry_TwoHands_Hit", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (seen.Add(clip.name))
                {
                    merged.Add(clip.name);
                }
            }

            return SortByCategory(merged.ToArray());
        }
    }
}
