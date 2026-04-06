#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 튜토리얼 Phase 1 (39단계) 자동 생성 에디터 도구
/// 메뉴: Tools > Tutorial > Phase 1 빌드
///
/// ★ 기존 에셋 파일을 삭제하지 않음
/// ★ P1_XX_ 접두어 파일만 생성/덮어쓰기
/// ★ TutorialManager의 phase1Steps 리스트 자동 배치
/// </summary>
public class TutorialPhase1Builder
{
    private const string FOLDER = "Assets/Data/Tutorial/Phase1";

    [MenuItem("Tools/Tutorial/Phase 1 빌드 (44단계 자동생성)")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Data/Tutorial", "Phase1");
        }

        var steps = new List<TutorialStepData>();

        // ═══════════════════════════════════════════════════════════
        //  Step 정의 (0-based index, 표시는 1-based)
        // ═══════════════════════════════════════════════════════════

        // --- Step 1: 환영 ---
        steps.Add(Create(1, new StepDef {
            tip = "무한레벨업을 플레이해주셔서 감사합니다!\n저희 게임은 자동으로 플레이 됩니다.\n화면을 터치해 주세요.",
            advance = TutorialAdvanceType.ClickAnywhere,
            rewards = new[] { R(TutorialRewardType.Item, 1) } // 기존 아이템 보상 유지 (Inspector에서 설정)
        }));

        // --- Step 2: 장비 슬롯 클릭 (인벤토리 영역만 허용, 레벨업 패널 숨김) ---
        steps.Add(Create(2, new StepDef {
            tip = "장비를 획득했어요!\n장비 아이콘을 클릭하세요.",
            focus = "InvenSlot:0",
            advance = TutorialAdvanceType.ClickFocusTarget,
            useArea = true,
            areaTarget = "EquipSlotParent",
            padding = new Vector2(100, 100),
            hideTargets = new[] { "EquipLevelUpPanel" }
        }));

        // --- Step 3: 장착 버튼만 포커스 (레벨업 패널+버튼 숨김) ---
        steps.Add(Create(3, new StepDef {
            tip = "장비 슬롯에는 레벨업과 장착 버튼이 있어요!\n장착 버튼을 눌러보세요.",
            focus = "Contains:EquipButton",
            advance = TutorialAdvanceType.ClickFocusTarget,
            padding = new Vector2(20, 20),
            tipPos = TipPosition.Above,
            hideTargets = new[] { "EquipLevelUpPanel", "AllContains:LevelUpButton" }
        }));

        // --- Step 4: AUTO 버튼 → 10회뽑기 보상 (강화/레벨업 패널 숨김) ---
        steps.Add(Create(4, new StepDef {
            tip = "장비를 장착하면 스킬이 자동으로 등록돼요!\nAUTO 버튼을 눌러보세요!",
            focus = "오토",
            advance = TutorialAdvanceType.ClickFocusTarget,
            rewards = new[] { R(TutorialRewardType.EquipmentTicket, 10) },
            hideTargets = new[] { "EquipLevelUpPanel", "EnhancePanel" }
        }));

        // --- Step 5: 10회 뽑기 (강화/레벨업 패널 숨김) ---
        steps.Add(Create(5, new StepDef {
            tip = "장비 뽑기를 해볼까요?\n10회 뽑기 버튼을 터치하세요!",
            focus = "Ten",
            advance = TutorialAdvanceType.ClickFocusTarget,
            hideTargets = new[] { "EquipLevelUpPanel", "EnhancePanel" }
        }));

        // --- Step 6: 가챠 결과 닫기 (닫기 버튼만 클릭 가능) ---
        steps.Add(Create(6, new StepDef {
            tip = "새로운 장비를 획득했어요!\n닫기 버튼을 눌러주세요.",
            focus = "GachaResultCloseBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            padding = new Vector2(40, 40),
            delay = 0f,
            tipPos = TipPosition.Below
        }));

        // --- Step 7: 장비 장착 (슬롯 영역만 허용, 레벨업 버튼 숨김) ---
        steps.Add(Create(7, new StepDef {
            tip = "뽑은 장비를 장착해 보세요!\n장비를 터치해서 장착!",
            focus = "InvenSlot:0",
            advance = TutorialAdvanceType.WaitForAction,
            action = "EquipItem",
            useArea = true,
            areaTarget = "EquipSlotParent",
            padding = new Vector2(100, 100),
            tipPos = TipPosition.Above,
            hideTargets = new[] { "EquipLevelUpPanel", "EnhancePanel", "AllContains:LevelUpButton" }
        }));

        // --- Step 8: 농장 버튼 (씬 전환) ---
        steps.Add(Create(8, new StepDef {
            tip = "이제 농장에 가볼까요?\n농장 버튼을 터치하세요!",
            focus = "농장Button",
            advance = TutorialAdvanceType.ClickFocusTarget,
            sceneTransition = true
        }));

        // --- Step 9: 농장 설명 ---
        steps.Add(Create(9, new StepDef {
            tip = "이곳은 농장이에요!\n씨앗을 심어서 작물을 얻는 곳입니다.\n화면을 터치해 주세요.",
            advance = TutorialAdvanceType.ClickAnywhere,
            delay = 0f
        }));

        // --- Step 10: 작물상점 ---
        steps.Add(Create(10, new StepDef {
            tip = "작물상점에서 씨앗을 구매해 보세요!",
            focus = "작물상점",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 11: 씨앗 선택 (상점 전체 — 마스크 없이) ---
        steps.Add(Create(11, new StepDef {
            tip = "원하는 씨앗을 선택해 주세요!",
            advance = TutorialAdvanceType.WaitForAction,
            action = "SeedSelected",
            useArea = true,
            areaTarget = "CropShopPanel",
            padding = new Vector2(200, 200)
        }));

        // --- Step 12: 씨앗 구매 (상세 패널 영역만 허용) ---
        steps.Add(Create(12, new StepDef {
            tip = "구매 버튼을 눌러 씨앗을 구매하세요!",
            advance = TutorialAdvanceType.WaitForAction,
            action = "BuySeed",
            useArea = true,
            areaTarget = "CropShopDetail",
            padding = new Vector2(100, 100)
        }));

        // --- Step 13: 작물상점 닫기 (닫기 버튼 직접 포커스) ---
        steps.Add(Create(13, new StepDef {
            tip = "작물상점을 닫아주세요.",
            focus = "CropShopCloseBtn",
            advance = TutorialAdvanceType.WaitForAction,
            action = "CropShopClosed"
        }));

        // --- Step 14: 빈밭 클릭 ---
        steps.Add(Create(14, new StepDef {
            tip = "빈 밭을 터치해서 작물을 심어보세요!",
            focus = "FarmPlot:0",
            advance = TutorialAdvanceType.WaitForAction,
            action = "PlotOpened"
        }));

        // --- Step 15: 씨앗 심기 (버튼+팝업 통합, 패널 전체 영역) ---
        steps.Add(Create(15, new StepDef {
            tip = "씨앗 버튼을 눌러 씨앗을 심으세요!",
            focus = "BtnPlant",
            advance = TutorialAdvanceType.WaitForAction,
            action = "SeedPlanted",
            useArea = true,
            areaTarget = "PlantModePanel",
            padding = new Vector2(200, 20),
            hideTargets = new[] { "BtnWater", "BtnFertilize", "BtnHarvest", "BtnInstantFinish" },
            delay = 0f
        }));

        // --- Step 16: 물주기 (버튼+팝업 통합, 패널 전체 영역) ---
        steps.Add(Create(16, new StepDef {
            tip = "물 버튼을 눌러 물을 주세요!",
            focus = "BtnWater",
            advance = TutorialAdvanceType.WaitForAction,
            action = "WaterApplied",
            useArea = true,
            areaTarget = "PlantModePanel",
            padding = new Vector2(200, 20),
            hideTargets = new[] { "BtnPlant", "BtnFertilize", "BtnHarvest", "BtnInstantFinish" }
        }));

        // --- Step 17: 비료주기 (버튼+팝업 통합, 패널 전체 영역) ---
        steps.Add(Create(17, new StepDef {
            tip = "비료 버튼을 눌러 비료를 주세요!",
            focus = "BtnFertilize",
            advance = TutorialAdvanceType.WaitForAction,
            action = "FertApplied",
            useArea = true,
            areaTarget = "PlantModePanel",
            padding = new Vector2(200, 20),
            hideTargets = new[] { "BtnPlant", "BtnWater", "BtnHarvest", "BtnInstantFinish" }
        }));

        // --- Step 18: 수확 버튼 ---
        steps.Add(Create(18, new StepDef {
            tip = "수확 버튼을 눌러 수확하세요!",
            focus = "BtnHarvest",
            advance = TutorialAdvanceType.WaitForAction,
            action = "HarvestComplete"
        }));

        // --- Step 19: 작물관리 패널 닫기 (닫기 버튼 포커스) ---
        steps.Add(Create(19, new StepDef {
            tip = "작물 관리 패널을 닫아주세요.",
            focus = "PlantModeCloseBtn",
            advance = TutorialAdvanceType.WaitForAction,
            action = "PlantModeClosed"
        }));

        // --- Step 20: 작물인벤토리 열기 ---
        steps.Add(Create(20, new StepDef {
            tip = "작물 인벤토리를 확인해 보세요!\n크롭포인트도 확인할 수 있어요.",
            focus = "Contains:수확인벤",
            advance = TutorialAdvanceType.ClickFocusTarget,
            tipPos = TipPosition.Above
        }));

        // --- Step 21: 작물인벤토리 닫기 ---
        steps.Add(Create(21, new StepDef {
            tip = "작물 인벤토리를 닫아주세요.",
            focus = "인벤닫기",
            advance = TutorialAdvanceType.WaitForAction,
            action = "FarmInventoryClosed"
        }));

        // --- Step 22: 메인게임으로 이동 (씬 전환) ---
        steps.Add(Create(22, new StepDef {
            tip = "메인게임으로 돌아가 볼까요?\n메인게임 이동 버튼을 터치하세요!",
            focus = "메인게임으로",
            advance = TutorialAdvanceType.ClickFocusTarget,
            sceneTransition = true
        }));

        // --- Step 23: 100회 뽑기 ---
        steps.Add(Create(23, new StepDef {
            tip = "100회 뽑기를 해볼까요?\n100회 뽑기 버튼을 터치하세요!",
            focus = "HundredGachaBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            rewards = new[] { R(TutorialRewardType.EquipmentTicket, 100) },
            delay = 0f
        }));

        // --- Step 24: 100회 뽑기 결과 닫기 (닫기 버튼만 클릭 가능) ---
        steps.Add(Create(24, new StepDef {
            tip = "장비를 확인하고 닫기 버튼을 눌러주세요.",
            focus = "GachaResultCloseBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            padding = new Vector2(40, 40),
            delay = 0f,
            tipPos = TipPosition.Below
        }));

        // --- Step 25: 동료뽑기 버튼 ---
        steps.Add(Create(25, new StepDef {
            tip = "동료를 뽑아볼까요?\n동료뽑기 버튼을 눌러주세요!",
            focus = "CompanionGachaBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 28: 동료 1회 뽑기 ---
        steps.Add(Create(26, new StepDef {
            tip = "동료 1회 뽑기를 해보세요!",
            focus = "CompanionSinglePullBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            rewards = new[] { R(TutorialRewardType.CompanionTicket, 1) },
            delay = 0f
        }));

        // --- Step 27: 동료 뽑기 결과 닫기 (닫기 버튼 포커스 + 구멍) ---
        steps.Add(Create(27, new StepDef {
            tip = "동료 뽑기 결과를 닫아주세요.",
            advance = TutorialAdvanceType.ClickFocusTarget,
            focus = "CompanionResultCloseBtn",
            padding = new Vector2(40, 40),
            tipPos = TipPosition.Below,
            delay = 0f
        }));

        // --- Step 30: 동료 아이콘 타겟 ---
        steps.Add(Create(28, new StepDef {
            tip = "동료를 확인해 보세요!\n동료 아이콘을 터치하세요.",
            focus = "CompanionSlot:0",
            advance = TutorialAdvanceType.ClickFocusTarget,
            delay = 0f
        }));

        // --- Step 29: 핫바 등록 버튼 (설명 + 클릭) ---
        steps.Add(Create(29, new StepDef {
            tip = "동료에는 레벨업과 핫바 등록 버튼이 있어요!\n핫바에 등록해 보세요!",
            focus = "Contains:HotbarRegisterButton",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 30: 동료 오토 버튼 ---
        steps.Add(Create(30, new StepDef {
            tip = "동료 오토 버튼을 눌러보세요!\n자동으로 동료가 소환됩니다.",
            focus = "CompanionAutoBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 31: 메뉴 버튼 ---
        steps.Add(Create(31, new StepDef {
            tip = "메뉴 버튼을 눌러보세요!",
            focus = "MenuToggleBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 32: 인벤토리 버튼 ---
        steps.Add(Create(32, new StepDef {
            tip = "인벤토리 버튼을 눌러보세요!",
            focus = "MenuInventoryBtn",
            advance = TutorialAdvanceType.WaitForAction,
            action = "OpenInventory"
        }));

        // --- Step 33: 채팅 펼치기 ---
        steps.Add(Create(33, new StepDef {
            tip = "채팅을 펼쳐볼까요?\n펼치기 버튼을 눌러주세요!",
            focus = "ChatExpandBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            delay = 0f
        }));

        // --- Step 34: 채팅 입력 ---
        steps.Add(Create(34, new StepDef {
            tip = "채팅창에 글을 써보세요!",
            focus = "ChatInputField",
            advance = TutorialAdvanceType.WaitForAction,
            action = "ChatMessageSent",
            delay = 0f
        }));

        // --- Step 35: 채팅 축소 ---
        steps.Add(Create(35, new StepDef {
            tip = "채팅을 축소해 주세요!",
            focus = "ChatCollapseBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 36: 메일 버튼 ---
        steps.Add(Create(36, new StepDef {
            tip = "메일을 확인해 보세요!",
            focus = "MenuMailBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 37: 모두 받기 ---
        steps.Add(Create(37, new StepDef {
            tip = "모두 받기 버튼을 눌러주세요!",
            focus = "MailClaimAllBtn",
            advance = TutorialAdvanceType.WaitForAction,
            action = "MailClaimAll",
            delay = 0f
        }));

        // --- Step 38: 쿠폰 버튼 ---
        steps.Add(Create(38, new StepDef {
            tip = "쿠폰 버튼을 눌러보세요!",
            focus = "MailCouponBtn",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // --- Step 39: 쿠폰 코드 입력 (입력+등록 버튼 영역) ---
        steps.Add(Create(39, new StepDef {
            tip = "쿠폰 코드에 'ㄱ'을 입력해 보세요!",
            focus = "MailCouponInput",
            advance = TutorialAdvanceType.WaitForAction,
            action = "CouponUsed",
            useArea = true,
            areaTarget = "MailCouponPanel",
            padding = new Vector2(40, 80),
            delay = 0f
        }));

        // --- Step 40: 우편 패널 닫기 (강화로 전환 전) ---
        steps.Add(Create(40, new StepDef {
            tip = "우편을 닫아주세요.",
            focus = "MailCloseBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            delay = 0f
        }));

        // --- Step 41: 메뉴 인벤토리 버튼 클릭 (인벤토리 열기) ---
        steps.Add(Create(41, new StepDef {
            tip = "장비를 강화해 볼까요?\n인벤토리 버튼을 눌러주세요!",
            focus = "MenuInventoryBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            delay = 0f
        }));

        // --- Step 42: 장비 패널 슬롯 머리(Helmet) 선택 → 강화창 진입 ---
        steps.Add(Create(42, new StepDef {
            tip = "머리 장비를 터치하세요!\n장비 슬롯을 클릭하면 강화창이 열립니다.",
            focus = "EquipPanelSlot:Helmet",
            advance = TutorialAdvanceType.WaitForAction,
            action = "EnhancePanelOpened",
            padding = new Vector2(20, 20),
            hideTargets = new[] { "EquipLevelUpPanel", "AllContains:EquipButton", "AllContains:LevelUpButton" }
        }));

        // --- Step 43: 강화 버튼 클릭 (1레벨 올리기) ---
        steps.Add(Create(43, new StepDef {
            tip = "강화 버튼을 눌러 장비를 강화하세요!",
            focus = "EnhanceActionBtn",
            advance = TutorialAdvanceType.ClickFocusTarget,
            padding = new Vector2(20, 20)
        }));

        // --- Step 44: 강화 패널 닫기 ---
        steps.Add(Create(44, new StepDef {
            tip = "강화 패널을 닫아주세요.",
            focus = "Contains:BtnClose",
            advance = TutorialAdvanceType.ClickFocusTarget
        }));

        // ═══════════════════════════════════════════════════════════
        //  TutorialManager에 자동 배치
        // ═══════════════════════════════════════════════════════════

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 씬에서 TutorialManager 찾기
        var mgr = Object.FindObjectOfType<TutorialManager>(true);
        if (mgr == null)
        {
            // DontDestroyOnLoad에 있을 수 있으니 모든 오브젝트 검색
            var all = Resources.FindObjectsOfTypeAll<TutorialManager>();
            if (all.Length > 0) mgr = all[0];
        }

        if (mgr != null)
        {
            Undo.RecordObject(mgr, "Tutorial Phase1 Auto Setup");
            mgr.phase1Steps = steps;

            // ★ phase2, phase3는 비우기 (전부 phase1에 통합)
            mgr.phase2Steps.Clear();
            mgr.phase3Steps.Clear();

            EditorUtility.SetDirty(mgr);
            Debug.Log($"[TutorialBuilder] ★ phase1Steps에 {steps.Count}개 스텝 배치 완료!");
            Debug.Log("[TutorialBuilder] ★ phase2Steps, phase3Steps 비움 (phase1에 통합)");
        }
        else
        {
            Debug.LogWarning("[TutorialBuilder] TutorialManager를 씬에서 찾을 수 없습니다.\n" +
                           "MainScene을 열고 다시 실행해 주세요.");
        }

        Debug.Log($"[TutorialBuilder] ★ Phase 1 빌드 완료: {steps.Count}개 스텝 ({FOLDER})");
    }

    // ═══════════════════════════════════════════════════════════════
    //  헬퍼
    // ═══════════════════════════════════════════════════════════════

    private struct StepDef
    {
        public string tip;
        public string focus;
        public TutorialAdvanceType advance;
        public string action;
        public bool sceneTransition;
        public float delay;
        public Vector2 padding;
        public TipPosition tipPos;
        public TutorialReward[] rewards;
        public bool useArea;
        public string areaTarget;
        public string[] hideTargets;
    }

    private static TutorialStepData Create(int num, StepDef d)
    {
        string assetName = $"P1_{num:D2}";
        string path = $"{FOLDER}/{assetName}.asset";

        // 기존 에셋이 있으면 로드, 없으면 생성
        var step = AssetDatabase.LoadAssetAtPath<TutorialStepData>(path);
        if (step == null)
        {
            step = ScriptableObject.CreateInstance<TutorialStepData>();
            AssetDatabase.CreateAsset(step, path);
        }

        step.tipMessage = d.tip ?? "";
        step.focusTargetName = d.focus ?? "";
        step.focusPadding = d.padding == Vector2.zero ? new Vector2(40, 40) : d.padding;
        step.advanceType = d.advance;
        step.requiredAction = d.action ?? "";
        step.autoAdvanceDelay = 2f;
        step.tipPosition = d.tipPos;
        step.isSceneTransitionStep = d.sceneTransition;
        step.delayBeforeShow = d.delay;
        step.useAreaFocus = d.useArea;
        step.areaTargetName = d.areaTarget ?? "";
        step.hideTargets = d.hideTargets;
        step.rewards = d.rewards;

        EditorUtility.SetDirty(step);
        return step;
    }

    private static TutorialReward R(TutorialRewardType type, int amount)
    {
        return new TutorialReward { rewardType = type, amount = amount };
    }
}
#endif
