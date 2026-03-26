using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class BuildPlayerAnimatorController
{
    [MenuItem("Tools/플레이어 Animator Controller 재생성")]
    public static void Build()
    {
        string controllerPath = "Assets/Sprite/캐릭터/플레이어 컨트롤러.controller";

        // 기존 컨트롤러 로드 또는 새로 생성
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }
        else
        {
            // 기존 파라미터/레이어 전부 제거
            while (controller.parameters.Length > 0)
                controller.RemoveParameter(0);
            while (controller.layers.Length > 0)
                controller.RemoveLayer(0);
        }

        // ═══ 파라미터 추가 ═══
        controller.AddParameter("Direction", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        // Direction 기본값 = 1 (Side)
        var parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == "Direction")
            {
                parameters[i].defaultFloat = 1f;
                break;
            }
        }
        controller.parameters = parameters;

        // ═══ 레이어 + 스테이트 머신 ═══
        controller.AddLayer("Base Layer");
        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine sm = layer.stateMachine;

        // ═══ 애니메이션 클립 로드 ═══
        string basePath = "Assets/Sprite/캐릭터/";

        AnimationClip idleDown  = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Idle_Down.anim");
        AnimationClip idleSide  = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Idle_Side.anim");
        AnimationClip idleUp    = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Idle_Up.anim");

        AnimationClip walkDown  = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Walk_Down.anim");
        AnimationClip walkSide  = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Walk_Side.anim");
        AnimationClip walkUp    = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Walk_Up.anim");

        AnimationClip atkDown   = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Attack_Down.anim");
        AnimationClip atkSide   = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Attack_Side.anim");
        AnimationClip atkUp     = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Attack_Up.anim");

        AnimationClip deathDown = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Death_Down.anim");
        AnimationClip deathSide = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Death_Side.anim");
        AnimationClip deathUp   = AssetDatabase.LoadAssetAtPath<AnimationClip>(basePath + "Death_Up.anim");

        // ═══ Blend Tree 생성 ═══
        BlendTree idleTree, walkTree, attackTree, deathTree;

        // IdleTree
        var idleState = controller.CreateBlendTreeInController("IdleTree", out idleTree, 0);
        idleState.motion = idleTree;
        idleTree.blendParameter = "Direction";
        idleTree.blendType = BlendTreeType.Simple1D;
        idleTree.useAutomaticThresholds = false;
        idleTree.AddChild(idleDown,  0f);
        idleTree.AddChild(idleSide,  1f);
        idleTree.AddChild(idleUp,    2f);

        // WalkTree
        var walkState = controller.CreateBlendTreeInController("WalkTree", out walkTree, 0);
        walkState.motion = walkTree;
        walkTree.blendParameter = "Direction";
        walkTree.blendType = BlendTreeType.Simple1D;
        walkTree.useAutomaticThresholds = false;
        walkTree.AddChild(walkDown,  0f);
        walkTree.AddChild(walkSide,  1f);
        walkTree.AddChild(walkUp,    2f);

        // AttackTree
        var attackState = controller.CreateBlendTreeInController("AttackTree", out attackTree, 0);
        attackState.motion = attackTree;
        attackTree.blendParameter = "Direction";
        attackTree.blendType = BlendTreeType.Simple1D;
        attackTree.useAutomaticThresholds = false;
        attackTree.AddChild(atkDown,  0f);
        attackTree.AddChild(atkSide,  1f);
        attackTree.AddChild(atkUp,    2f);

        // DeathTree
        var deathState = controller.CreateBlendTreeInController("DeathTree", out deathTree, 0);
        deathState.motion = deathTree;
        deathTree.blendParameter = "Direction";
        deathTree.blendType = BlendTreeType.Simple1D;
        deathTree.useAutomaticThresholds = false;
        deathTree.AddChild(deathDown, 0f);
        deathTree.AddChild(deathSide, 1f);
        deathTree.AddChild(deathUp,   2f);

        // ═══ 기본 스테이트 ═══
        sm.defaultState = idleState;

        // ═══ 트랜지션 ═══
        // Idle → Walk (IsMoving = true)
        var t1 = idleState.AddTransition(walkState);
        t1.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        t1.hasExitTime = false;
        t1.duration = 0.1f;

        // Walk → Idle (IsMoving = false)
        var t2 = walkState.AddTransition(idleState);
        t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        t2.hasExitTime = false;
        t2.duration = 0.1f;

        // Attack → Idle (ExitTime)
        var t3 = attackState.AddTransition(idleState);
        t3.hasExitTime = true;
        t3.exitTime = 0.9f;
        t3.duration = 0.1f;

        // AnyState → Attack (Attack 트리거)
        var t4 = sm.AddAnyStateTransition(attackState);
        t4.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        t4.hasExitTime = false;
        t4.duration = 0.05f;
        t4.canTransitionToSelf = false;

        // AnyState → Death (Death 트리거)
        var t5 = sm.AddAnyStateTransition(deathState);
        t5.AddCondition(AnimatorConditionMode.If, 0, "Death");
        t5.hasExitTime = false;
        t5.duration = 0.05f;
        t5.canTransitionToSelf = false;

        // ═══ 스테이트 위치 (Animator 창 레이아웃) ═══
        var childStates = sm.states;
        for (int i = 0; i < childStates.Length; i++)
        {
            var cs = childStates[i];
            switch (cs.state.name)
            {
                case "IdleTree":   cs.position = new Vector3(300,  50, 0); break;
                case "WalkTree":   cs.position = new Vector3(600,  50, 0); break;
                case "AttackTree": cs.position = new Vector3(450, 250, 0); break;
                case "DeathTree":  cs.position = new Vector3(450,-150, 0); break;
            }
            childStates[i] = cs;
        }
        sm.states = childStates;

        // ═══ 저장 ═══
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("✅ 플레이어 Animator Controller 재생성 완료!");
        Debug.Log($"   스테이트: IdleTree, WalkTree, AttackTree, DeathTree");
        Debug.Log($"   파라미터: Direction(Float), IsMoving(Bool), Attack(Trigger), Death(Trigger)");
        Debug.Log($"   트랜지션: Idle↔Walk, AnyState→Attack, AnyState→Death, Attack→Idle");
    }
}
