using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Client.Interactions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// 매치 맵의 "들 수 있는 소품" 전환. 마트: 진열 상품·봉지·꽃다발·상자류는 Carryable, 선반·냉장고·가구·카트는 고정.
    /// 저택(<c>Mansion</c> 씬)에서도 같은 메뉴를 쓰며, 변형 프리팹 폴더·보고서 경로·키워드 일부가 씬 이름으로 갈린다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><b>대상</b>: 경계(<c>MartEnvironment/Boundary</c>) 안에 있고, (a) 분해 결과 <c>Products</c>·<c>Refill</c> 그룹의 자식이거나
    /// (b) 낱개 프리팹 인스턴스 중 원본 프리팹 이름이 포함 키워드에 걸리거나, 분류 불가라도 최대 변이 <see cref="LooseSizeCap"/> 이하인 것.
    /// 제외 키워드(선반·냉장고·카운터·카트·구조물 등)는 항상 뺀다.</item>
    /// <item><b>우리 프리팹</b>(<c>Gen_*</c> 생성 프리팹, <c>Prefabs/Mart/*</c>)은 프리팹 자체에 Rigidbody(kinematic)·CarryableItem을 넣는다.
    /// 인스턴스는 자동으로 따라온다.</item>
    /// <item><b>Synty 팩 프리팹</b>은 건드리지 않고 <c>Prefabs/Carryable/Mart/&lt;이름&gt; Carryable.prefab</c> 변형을 만들어
    /// 인스턴스를 교체한다(위치·회전 오버라이드 유지).</item>
    /// <item>물건 id는 씬 계층 해시(<c>CarryableItem.ResolveSceneInstanceObjectId</c>)라 별도 지정이 없다. 모든 피어가 같은 씬을 로드하므로 일치한다.</item>
    /// <item>정적 배칭: Carryable·Rigidbody가 없는 렌더러에 <c>BatchingStatic</c>만 켠다(로비 방식).</item>
    /// </list>
    /// </remarks>
    public static class MartCarryableSetupMenu
    {
        private const string MenuRoot = "Game/Match Map/Carryable/";
        private const string MartVariantFolder = "Assets/_Game/Content/Prefabs/Carryable/Mart";
        private const string MansionVariantFolder = "Assets/_Game/Content/Prefabs/Carryable/Mansion";
        private const string MartReportPath = "docs/design/match-map/carryable-props.md";
        private const string MansionReportPath = "docs/design/match-map/mansion/carryable-props.md";
        private const string MansionSceneName = "Mansion";

        /// <summary>열린 씬이 저택이면 저택용 폴더·보고서·키워드를 쓴다. 그 외(마트·놀이터)는 마트 설정.</summary>
        private static bool IsMansionScene => SceneManager.GetActiveScene().name == MansionSceneName;

        private static string VariantFolder => IsMansionScene ? MansionVariantFolder : MartVariantFolder;
        private static string ReportPath => IsMansionScene ? MansionReportPath : MartReportPath;
        private const string BoundaryName = "Boundary";
        private const float LooseSizeCap = 0.5f;
        private const int ItemsPerTick = 40;

        /// <summary>원본 프리팹 이름에 이 낱말이 있으면 고정(들 수 없음).</summary>
        private static readonly string[] ExcludeKeywords =
        {
            "Shelf", "Aisle", "Fridge", "Freezer", "Counter", "Checkout", "Trolley", "Cart", "Table", "Cabinet", "Kiosk",
            "Stand", "Rack", "Display", "Pallet", "Sign", "Wall", "Floor", "Door", "Light", "Ceiling", "Pillar", "Bld_", "Env_",
            "Roof", "Window", "Stairs", "Fence", "Column", "Beam", "Pipe", "Duct", "Vent", "Camera", "Speaker", "Register",
            "Scanner", "Screen", "Monitor", "Terminal", "Machine", "Gate", "Barrier", "Bollard", "Bench", "Chair", "Stool",
            "Bin_", "Dumptser", "Dumpster", "Vehicle", "Shredder", "Planter", "Papers", "Trash_Bags", "Alarm", "Dispenser",
            "Mirror", "Sink", "Toilet", "Map_", "Mat_", "Trim", "Rocks", "Stall", "Entrance", "FX_", "Hanger", "Sweater_Set",
            "Airconditioner", "Wheel_Stop", "Mall_", "Stove", "Oven", "Rotisserie", "Mower", "Mart_basket",
        };

        /// <summary>진열 그룹 안에 있어도 항상 고정인 것(선반에 붙은 가격표·라벨).</summary>
        private static readonly string[] AlwaysFixedKeywords = { "PriceTag", "Price_Tag", "Label" };

        /// <summary>저택(Horror Mansion 팩)에서 추가로 고정하는 것: 벽 장식·천·데칼·가구·설비. 마트 제외 목록에 더해진다.</summary>
        private static readonly string[] MansionExcludeKeywords =
        {
            "Painting", "Picture", "Curtain", "Rug", "Sheet_", "Decal", "Page", "Silhouette", "Keyhole", "Door_Mat",
            "Power_Box", "Shackle", "Chandelier", "Torch", "Ladder", "Dresser", "Wardrobe", "Bed", "Ottoman", "Shower",
            "Bath", "Fireplace", "Furnace", "Piano", "Coffin", "Gravestone", "Statue", "Wood_Stack", "Bookend",
        };

        /// <summary>
        /// 저택에서 진열(분해 결과) 그룹 안에 있어도 항상 고정: 낙엽 조각, 종잇장(두께 1 mm라 받침 판정이 불안정), 데칼.
        /// </summary>
        private static readonly string[] MansionAlwaysFixedKeywords = { "Page", "Env_", "Leaves", "Decal" };

        private static string[] ActiveAlwaysFixedKeywords => IsMansionScene
            ? AlwaysFixedKeywords.Concat(MansionAlwaysFixedKeywords).ToArray()
            : AlwaysFixedKeywords;

        /// <summary>저택에서는 마트 제외 목록 중 이 낱말을 제외하지 않는다(책상 위 서류는 들 수 있게).</summary>
        private static readonly string[] MansionUnexcludeKeywords = { "Papers" };

        /// <summary>저택에서 크기와 무관하게 들 수 있는 것. 마트 포함 목록에 더해진다.</summary>
        private static readonly string[] MansionIncludeKeywords =
        {
            "Book", "Candle_0", "Goblet", "Plate_0", "Can_", "Cardboard", "Bottle", "Jar", "Suitcase", "Mask", "Skull",
            "Key_", "Spoon", "Kitchen_Pan", "Plant_Pot", "Doily", "Papers", "Paper_", "Bowl", "Cup", "Mug", "Lockbox",
            "Music_Box", "Jack_In_The_Box", "Bone", "Teddy", "Doll", "Toilet_Roll",
            // 보고서 '분류 불가'에서 손에 들 만한 것(2026-09-17): 수건·고양이 인형·장식 두상·십자가·소형 램프·탁상 시계·칼
            "Towel", "Cat_Stuffed", "Ornamental_Head", "Exorcist_Cross", "Lamp_02", "Lamp_03", "Clock_02", "Wep_Knife",
        };

        private static string[] ActiveExcludeKeywords => IsMansionScene
            ? ExcludeKeywords.Except(MansionUnexcludeKeywords).Concat(MansionExcludeKeywords).ToArray()
            : ExcludeKeywords;

        private static string[] ActiveIncludeKeywords => IsMansionScene
            ? IncludeKeywords.Concat(MansionIncludeKeywords).ToArray()
            : IncludeKeywords;

        /// <summary>낱개 프리팹 중 이 낱말이 있으면 크기와 무관하게 들 수 있음.</summary>
        private static readonly string[] IncludeKeywords =
        {
            "Product", "Food_", "Bag", "Flower", "Bouquet", "Cardboard", "Box", "Bottle", "Can_", "Jar", "Carton", "Crate",
            "Sack", "Balloon", "Toy", "Book", "Magazine", "Newspaper", "Cup", "Bowl", "Plate", "Mug", "Fruit", "Vegetable",
            "Lettuce", "Shoe", "Napkin", "Sugar", "Sauce", "Bucket", "Keyboard", "Computer_Tower", "Radio", "Briefcase",
            "Safety_Step", // 창고 발판(0.8 m): 사용자 요청(2026-09-11)으로 들 수 있게
        };

        private static readonly (string keyword, string name)[] DisplayNames =
        {
            ("Lettuce", "양배추"), ("Flower", "꽃"), ("Bouquet", "꽃다발"), ("Cardboard", "상자"), ("Box", "상자"), ("Bag", "봉지"),
            ("Food_", "식품"), ("Shoe", "신발"), ("Safety_Step", "발판"), ("Product", "상품"), ("Gen_", "상품"),
            // 저택
            ("Book", "책"), ("Candle", "양초"), ("Goblet", "잔"), ("Plate", "접시"), ("Bottle", "병"), ("Jar", "병"),
            ("Suitcase", "가방"), ("Mask", "가면"), ("Skull", "두개골"), ("Key_", "열쇠"), ("Spoon", "숟가락"),
            ("Kitchen_Pan", "팬"), ("Plant_Pot", "화분"), ("Bowl", "그릇"), ("Paper", "종이"), ("Can_", "캔"),
            ("Lockbox", "금고"), ("Music_Box", "오르골"), ("Toilet_Roll", "휴지"), ("Doily", "장식 천"),
            ("Towel", "수건"), ("Cat_Stuffed", "고양이 인형"), ("Ornamental_Head", "장식 두상"), ("Cross", "십자가"),
            ("Lamp", "램프"), ("Clock", "시계"), ("Knife", "칼"), ("Fork", "포크"), ("Vase", "꽃병"), ("Bible", "성경"),
            ("Crystal_Ball", "수정구"), ("Phone", "전화기"), ("Radio", "라디오"), ("Toaster", "토스터"), ("Bucket", "양동이"),
            ("Stake", "말뚝"), ("Mallet", "망치"), ("Burner", "버너"), ("Ghost_Meter", "탐지기"),
        };

        private sealed class Target
        {
            public GameObject Instance;   // 교체·수정 기준이 되는 프리팹 인스턴스 루트(또는 비프리팹 오브젝트)
            public GameObject Source;     // 원본 프리팹 에셋
            public string Reason;
        }

        private sealed class Classification
        {
            public readonly List<Target> Targets = new();
            public readonly Dictionary<string, int> Included = new(StringComparer.Ordinal);
            public readonly Dictionary<string, (int count, Vector3 size)> Excluded = new(StringComparer.Ordinal);
            public readonly Dictionary<string, (int count, Vector3 size)> Unclassified = new(StringComparer.Ordinal);
            public readonly Dictionary<string, int> AlreadyCarryable = new(StringComparer.Ordinal);
            public int OutsideBoundary;
        }

        // ------------------------------------------------------------------ 1. 보고

        [MenuItem(MenuRoot + "1. Report Targets")]
        public static void ReportTargets()
        {
            var scene = SceneManager.GetActiveScene();
            var classification = Classify(scene);
            var report = BuildReport(scene, classification);
            var absolute = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, report, new UTF8Encoding(false));
            Debug.Log(
                $"[MartCarryable] 대상 {classification.Targets.Count}개, 제외 {classification.Excluded.Values.Sum(v => v.count)}개, " +
                $"분류 불가 {classification.Unclassified.Values.Sum(v => v.count)}개, 경계 밖 {classification.OutsideBoundary}개 → {ReportPath}");
        }

        private static Classification Classify(Scene scene)
        {
            var result = new Classification();
            var hasBoundary = TryGetBoundary(scene, out var boundary);
            if (!hasBoundary)
            {
                Debug.LogWarning("[MartCarryable] Boundary 콜라이더를 찾지 못해 경계 판정 없이 진행합니다.");
            }

            var seenRoots = new HashSet<GameObject>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
                if (root == null)
                {
                    root = renderer.gameObject;
                }

                if (!seenRoots.Add(root))
                {
                    continue;
                }

                if (hasBoundary && !Inside(boundary, renderer.bounds.center))
                {
                    result.OutsideBoundary++;
                    continue;
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
                var sourceName = source != null ? source.name : root.name;
                var parentName = root.transform.parent != null ? root.transform.parent.name : string.Empty;
                var size = RendererBounds(root).size;

                if (parentName == "Structure")
                {
                    continue; // 구조 조각
                }

                if (root.GetComponent<CarryableItem>() != null)
                {
                    result.AlreadyCarryable[sourceName] = result.AlreadyCarryable.GetValueOrDefault(sourceName) + 1;
                    continue; // 이미 변환된 것
                }

                // 가격표·라벨은 진열 그룹에 섞여 있어도 선반에 붙은 것이라 항상 고정.
                if (ActiveAlwaysFixedKeywords.Any(k => sourceName.Contains(k, StringComparison.Ordinal)))
                {
                    Count(result.Excluded, sourceName, size);
                    continue;
                }

                // 분해 결과 상품(Products)·채운 상품(Refill)은 이름에 진열대 이름이 들어 있어도(Gen_..._Aisle_Preset_...) 상품이다.
                var isProductGroup = parentName == "Products" || parentName == "Refill";
                if (!isProductGroup && ActiveExcludeKeywords.Any(k => sourceName.Contains(k, StringComparison.Ordinal)))
                {
                    Count(result.Excluded, sourceName, size);
                    continue;
                }
                var byKeyword = ActiveIncludeKeywords.Any(k => sourceName.Contains(k, StringComparison.Ordinal));
                var bySize = Mathf.Max(size.x, size.y, size.z) <= LooseSizeCap;
                if (isProductGroup || byKeyword || bySize)
                {
                    if (source == null)
                    {
                        Count(result.Unclassified, sourceName + " (프리팹 아님)", size);
                        continue;
                    }

                    result.Targets.Add(new Target
                    {
                        Instance = root,
                        Source = source,
                        Reason = isProductGroup ? "진열" : byKeyword ? "키워드" : "소형",
                    });
                    result.Included[sourceName] = result.Included.GetValueOrDefault(sourceName) + 1;
                    continue;
                }

                Count(result.Unclassified, sourceName, size);
            }

            return result;
        }

        private static void Count(Dictionary<string, (int count, Vector3 size)> bucket, string key, Vector3 size)
        {
            bucket[key] = bucket.TryGetValue(key, out var current) ? (current.count + 1, current.size) : (1, size);
        }

        private static string BuildReport(Scene scene, Classification c)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 마트 맵 들 수 있는 소품 목록 (자동 생성)");
            sb.AppendLine();
            sb.AppendLine($"씬 `{scene.name}`, 생성 {DateTime.Now:yyyy-MM-dd HH:mm}. 메뉴 `Game > Match Map > Carryable > 1. Report Targets`.");
            sb.AppendLine();
            sb.AppendLine("규칙: 경계 안 + (진열 상품 그룹 `Products`/`Refill` 자식 | 원본 프리팹 이름 포함 키워드 | 최대 변 0.5 m 이하) − 제외 키워드(선반·냉장고·카운터·카트·구조물·설비).");
            sb.AppendLine();
            sb.AppendLine($"- 이미 Carryable: **{c.AlreadyCarryable.Values.Sum()}개**, 프리팹 {c.AlreadyCarryable.Count}종");
            sb.AppendLine($"- 아직 변환 안 된 대상: {c.Targets.Count}개, 프리팹 {c.Included.Count}종 (0이면 변환 완료)");
            sb.AppendLine($"- 제외(고정): {c.Excluded.Values.Sum(v => v.count)}개, 프리팹 {c.Excluded.Count}종");
            sb.AppendLine($"- 분류 불가(고정 유지, 검토 필요): {c.Unclassified.Values.Sum(v => v.count)}개, 프리팹 {c.Unclassified.Count}종");
            sb.AppendLine($"- 경계 밖(플레이 구역 아님, 고정): {c.OutsideBoundary}개");
            sb.AppendLine();
            sb.AppendLine("## Carryable 프리팹 (변환 완료)");
            sb.AppendLine();
            sb.AppendLine("| 프리팹 | 개수 |");
            sb.AppendLine("|---|---:|");
            foreach (var pair in c.AlreadyCarryable.OrderByDescending(p => p.Value))
            {
                sb.AppendLine($"| {pair.Key} | {pair.Value} |");
            }

            if (c.Targets.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 아직 변환 안 된 대상");
                sb.AppendLine();
                sb.AppendLine("| 프리팹 | 개수 | 이유 |");
                sb.AppendLine("|---|---:|---|");
                foreach (var group in c.Targets.GroupBy(t => t.Source.name).OrderByDescending(g => g.Count()))
                {
                    sb.AppendLine($"| {group.Key} | {group.Count()} | {string.Join("/", group.Select(t => t.Reason).Distinct())} |");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## 분류 불가 (고정으로 남김 — 들 수 있게 하려면 IncludeKeywords에 추가)");
            sb.AppendLine();
            sb.AppendLine("| 프리팹 | 개수 | 크기(m) |");
            sb.AppendLine("|---|---:|---|");
            foreach (var pair in c.Unclassified.OrderByDescending(p => p.Value.count))
            {
                sb.AppendLine($"| {pair.Key} | {pair.Value.count} | {pair.Value.size.x:0.00}×{pair.Value.size.y:0.00}×{pair.Value.size.z:0.00} |");
            }

            sb.AppendLine();
            sb.AppendLine("## 제외 (가구·구조·설비)");
            sb.AppendLine();
            sb.AppendLine("| 프리팹 | 개수 |");
            sb.AppendLine("|---|---:|");
            foreach (var pair in c.Excluded.OrderByDescending(p => p.Value.count))
            {
                sb.AppendLine($"| {pair.Key} | {pair.Value.count} |");
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------ 2. 변환

        private static Queue<Target> pending;
        private static Dictionary<GameObject, GameObject> variantBySource;
        private static int convertedOwn;
        private static int replaced;
        private static int failed;
        private static Scene convertingScene;

        /// <summary>
        /// 한 번에 끝내는 변환. 에디터가 잠시 멈추지만 Play 진입·재컴파일(도메인 리로드)에 배경 큐가 날아가는 일이 없다.
        /// </summary>
        [MenuItem(MenuRoot + "2. Convert Targets (Now, Blocking)")]
        public static void ConvertTargetsNow()
        {
            ConvertTargets();
            if (pending == null)
            {
                return;
            }

            EditorApplication.update -= ProcessPending;
            try
            {
                while (pending != null && pending.Count > 0)
                {
                    ProcessPending();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(MenuRoot + "2b. Convert Targets (Background)")]
        public static void ConvertTargetsInBackground()
        {
            ConvertTargets();
        }

        private static void ConvertTargets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[MartCarryable] Play 모드를 끝낸 뒤 실행하세요.");
                return;
            }

            if (pending != null)
            {
                Debug.LogWarning("[MartCarryable] 이미 변환 중입니다.");
                return;
            }

            convertingScene = SceneManager.GetActiveScene();
            var classification = Classify(convertingScene);
            if (classification.Targets.Count == 0)
            {
                Debug.LogWarning("[MartCarryable] 변환 대상이 없습니다.");
                return;
            }

            EnsureFolder(VariantFolder);
            variantBySource = new Dictionary<GameObject, GameObject>();
            convertedOwn = replaced = failed = 0;

            // 우리 프리팹은 에셋을 직접 고친다(인스턴스 전체가 한 번에 따라옴). 팩 프리팹은 변형을 만든다.
            var sources = classification.Targets.Select(t => t.Source).Distinct().ToArray();
            foreach (var source in sources)
            {
                var path = AssetDatabase.GetAssetPath(source);
                if (IsOwnPrefab(path))
                {
                    if (ConfigureOwnPrefab(path))
                    {
                        convertedOwn++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                else
                {
                    var variant = GetOrCreateVariant(source);
                    if (variant != null)
                    {
                        variantBySource[source] = variant;
                    }
                    else
                    {
                        failed++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            pending = new Queue<Target>(classification.Targets.Where(t => variantBySource.ContainsKey(t.Source)));
            Debug.Log(
                $"[MartCarryable] 우리 프리팹 수정 {convertedOwn}종, 팩 변형 {variantBySource.Count}종, 실패 {failed}종. " +
                $"인스턴스 교체 {pending.Count}개 시작.");
            if (pending.Count == 0)
            {
                FinishConversion();
                return;
            }

            EditorApplication.update += ProcessPending;
        }

        private static void FinishConversion()
        {
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= ProcessPending;
            pending = null;
            EditorSceneManager.MarkSceneDirty(convertingScene);
            EditorSceneManager.SaveScene(convertingScene);
            var carryables = Object.FindObjectsByType<CarryableItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count(c => c.gameObject.scene == convertingScene);
            Debug.Log($"[MartCarryable] 완료: 인스턴스 교체 {replaced}개, 실패 {failed}개. 씬의 Carryable {carryables}개. 씬 저장됨.");
        }

        private static void ProcessPending()
        {
            if (pending == null)
            {
                EditorApplication.update -= ProcessPending;
                return;
            }

            var processed = 0;
            while (pending.Count > 0 && processed < ItemsPerTick)
            {
                var target = pending.Dequeue();
                processed++;
                if (target.Instance == null || !variantBySource.TryGetValue(target.Source, out var variant))
                {
                    failed++;
                    continue;
                }

                try
                {
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                        target.Instance,
                        variant,
                        new PrefabReplacingSettings
                        {
                            changeRootNameToAssetName = false,
                            prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                        },
                        InteractionMode.AutomatedAction);
                    replaced++;
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogWarning($"[MartCarryable] 교체 실패 {target.Instance.name}: {exception.Message}", target.Instance);
                }
            }

            var total = replaced + failed + pending.Count;
            if (EditorUtility.DisplayCancelableProgressBar("마트 Carryable 전환", $"{replaced + failed}/{total}", total == 0 ? 1f : (replaced + failed) / (float)total))
            {
                pending.Clear();
            }

            if (pending.Count > 0)
            {
                return;
            }

            FinishConversion();
        }

        public static bool IsConverting => pending != null;
        public static (int replaced, int failed, int pending) Progress => (replaced, failed, pending?.Count ?? 0);

        private static bool IsOwnPrefab(string assetPath)
        {
            return assetPath.StartsWith("Assets/_Game/", StringComparison.Ordinal);
        }

        /// <summary>우리 프리팹(생성 프리팹·Mart 프리팹)에 Rigidbody(kinematic)·CarryableItem을 넣어 저장한다.</summary>
        private static bool ConfigureOwnPrefab(string assetPath)
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                if (!ConfigureCarryable(root, Path.GetFileNameWithoutExtension(assetPath)))
                {
                    Debug.LogWarning($"[MartCarryable] 콜라이더가 없어 건너뜀: {assetPath}");
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, assetPath, out var success);
                return success;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>팩 프리팹의 Carryable 변형. 이미 있으면 재사용.</summary>
        private static GameObject GetOrCreateVariant(GameObject source)
        {
            var variantPath = $"{VariantFolder}/{source.name} Carryable.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            if (existing != null)
            {
                return existing;
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                if (instance == null || !ConfigureCarryable(instance, source.name))
                {
                    Debug.LogWarning($"[MartCarryable] 변형 만들기 실패(콜라이더 없음): {source.name}", source);
                    return null;
                }

                instance.name = $"{source.name} Carryable";
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, variantPath, out var success);
                return success ? saved : null;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static bool ConfigureCarryable(GameObject target, string sourceName)
        {
            if (target.GetComponentsInChildren<Collider>(true).Length == 0 && !TryAddBoundsCollider(target))
            {
                return false;
            }

            foreach (var meshCollider in target.GetComponentsInChildren<MeshCollider>(true))
            {
                meshCollider.convex = true;
            }

            // 에디터의 GetComponent는 빠진 컴포넌트에 '가짜 null'을 돌려주므로 ?? 대신 Unity의 == null 비교를 쓴다.
            var body = target.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = target.AddComponent<Rigidbody>();
            }

            body.mass = 1f;
            body.useGravity = true;
            body.isKinematic = true;

            var carryable = target.GetComponent<CarryableItem>();
            if (carryable == null)
            {
                carryable = target.AddComponent<CarryableItem>();
            }

            var serialized = new SerializedObject(carryable);
            serialized.FindProperty("displayName").stringValue = DisplayNameFor(sourceName);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// 콜라이더가 하나도 없는 팩 프리팹(저택 서류 `SM_Prop_Papers_01~03` 등)에 렌더러 경계 크기의 BoxCollider를 붙인다.
        /// 종이처럼 얇은 것은 놓기·받침 판정이 흔들리지 않게 두께를 최소 2 cm로 올린다.
        /// </summary>
        private static bool TryAddBoundsCollider(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Bounds? local = null;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = world.center + Vector3.Scale(world.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    var localPoint = target.transform.InverseTransformPoint(point);
                    if (local.HasValue)
                    {
                        var b = local.Value;
                        b.Encapsulate(localPoint);
                        local = b;
                    }
                    else
                    {
                        local = new Bounds(localPoint, Vector3.zero);
                    }
                }
            }

            const float minThickness = 0.02f;
            var bounds = local.Value;
            var size = Vector3.Max(bounds.size, Vector3.one * minThickness);
            var box = target.AddComponent<BoxCollider>();
            box.center = bounds.center + Vector3.up * Mathf.Max(0f, (size.y - bounds.size.y) * 0.5f);
            box.size = size;
            return true;
        }

        private static string DisplayNameFor(string sourceName)
        {
            foreach (var (keyword, name) in DisplayNames)
            {
                if (sourceName.Contains(keyword, StringComparison.Ordinal))
                {
                    return name;
                }
            }

            return "물건";
        }

        // ------------------------------------------------------------------ 3. 정적 배칭

        [MenuItem(MenuRoot + "3. Apply Static Batching To Fixed Props")]
        public static void ApplyStaticBatching()
        {
            var scene = SceneManager.GetActiveScene();
            var marked = 0;
            var cleared = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene || renderer is ParticleSystemRenderer || renderer is SkinnedMeshRenderer)
                {
                    continue;
                }

                // 움직일 수 있는 것(Carryable·Rigidbody·파쇄기 계열)은 배칭에서 뺀다.
                var movable = renderer.GetComponentInParent<Rigidbody>(true) != null ||
                              renderer.GetComponentInParent<CarryableItem>(true) != null ||
                              renderer.GetComponentInParent<Game.Bootstrap.ShredderInteractable>(true) != null;
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                var next = movable ? flags & ~StaticEditorFlags.BatchingStatic : flags | StaticEditorFlags.BatchingStatic;
                if (next == flags)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, next);
                if (movable) cleared++; else marked++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MartCarryable] 정적 배칭: 켬 {marked}개, 끔 {cleared}개. 씬 저장됨.");
        }

        // ------------------------------------------------------------------ 공용

        private static bool TryGetBoundary(Scene scene, out Bounds boundary)
        {
            boundary = default;
            var found = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box.transform.parent == null || box.transform.parent.name != BoundaryName)
                    {
                        continue;
                    }

                    if (!found)
                    {
                        boundary = box.bounds;
                        found = true;
                    }
                    else
                    {
                        boundary.Encapsulate(box.bounds);
                    }
                }
            }

            return found;
        }

        private static bool Inside(Bounds boundary, Vector3 point)
        {
            return point.x > boundary.min.x && point.x < boundary.max.x &&
                   point.z > boundary.min.z && point.z < boundary.max.z &&
                   point.y < boundary.max.y;
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
