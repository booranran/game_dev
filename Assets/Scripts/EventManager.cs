using UnityEngine;
using System;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    public enum EventType { None, YieldNPC, EmptySeat, TriggerNPC, ElbowGame }
    public enum NPCType
    {
        Elderly,
        Crush,
        // Pregnant,
        // Passenger,
        // Dog,
        // BabyHappy,
        // BabyCrying,
        TiredWorker,
        YoungPassenger,
        SmartphonePassenger,
    }

    public EventType currentEvent { get; private set; }
    public NPCType currentNPC { get; private set; }

    [Header("이벤트 한도")]
    public int maxEmptySeats = 10;
    public int maxYieldNPCs = 9;

    [Header("None 이벤트 가중치")]
    [Tooltip("Standing 상태에서 None 이벤트 비율 (낮을수록 EmptySeat 자주 발생)")]
    public int noneWeightStanding = 1;
    [Tooltip("Sitting 상태에서 None 이벤트 비율 (낮을수록 YieldNPC 자주 발생)")]
    public int noneWeightSitting = 1;

    [Header("TriggerNPC 가중치")]
    [Tooltip("Crush 이벤트 등장 가중치 (0이면 비활성화)")]
    public int triggerNPCWeight = 1;

    [Header("ElbowGame 가중치")]
    [Tooltip("팔꿈치 게임 등장 가중치 (0이면 비활성화)")]
    public int elbowGameWeight = 1;

    private int emptySeatCount = 0;
    private int yieldNPCCount = 0;

    public static event Action<EventType, NPCType> OnEventGenerated;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateEvent()
    {
        GameManager gm = GameManager.Instance;
        currentEvent = PickEventType(gm.playerState);
        currentNPC = NPCType.Elderly;

        Debug.Log($"[EventManager] 턴 {gm.currentTurn} | 상태: {gm.playerState} | 이벤트: {currentEvent}");

        if (currentEvent == EventType.YieldNPC)
        {
            yieldNPCCount++;
            currentNPC = NPCType.Elderly;
            Debug.Log($"[EventManager] YieldNPC 발생 ({yieldNPCCount}/{maxYieldNPCs})");
        }
        else if (currentEvent == EventType.EmptySeat)
        {
            emptySeatCount++;
            Debug.Log($"[EventManager] EmptySeat 발생 ({emptySeatCount}/{maxEmptySeats})");
            // OpenRandomSeat 여기서 호출 안 함 - HandleEmptySeat에서 빈자리 수 확인 후 분기
        }
        else if (currentEvent == EventType.TriggerNPC)
        {
            currentNPC = NPCType.Crush;
            Debug.Log("[EventManager] TriggerNPC 발생 → Crush 탑승");
        }
        else if (currentEvent == EventType.ElbowGame)
        {
            Debug.Log("[EventManager] ElbowGame 발생");
        }

        OnEventGenerated?.Invoke(currentEvent, currentNPC);
    }

    EventType PickEventType(GameManager.PlayerState state)
    {
        List<EventType> pool = new List<EventType>();

        if (state == GameManager.PlayerState.Sitting)
        {
            if (yieldNPCCount < maxYieldNPCs)
            {
                pool.Add(EventType.YieldNPC);
                pool.Add(EventType.YieldNPC);
                pool.Add(EventType.YieldNPC);
            }
            for (int i = 0; i < elbowGameWeight; i++) pool.Add(EventType.ElbowGame);
            for (int i = 0; i < noneWeightSitting; i++) pool.Add(EventType.None);
        }
        else
        {
            if (emptySeatCount < maxEmptySeats)
            {
                pool.Add(EventType.EmptySeat);
                pool.Add(EventType.EmptySeat);
            }
            for (int i = 0; i < noneWeightStanding; i++) pool.Add(EventType.None);
        }

        for (int i = 0; i < triggerNPCWeight; i++) pool.Add(EventType.TriggerNPC);

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    NPCType PickCompetingNPC()
    {
        int r = UnityEngine.Random.Range(0, 3);
        if (r == 0) return NPCType.TiredWorker;
        if (r == 1) return NPCType.YoungPassenger;
        return NPCType.SmartphonePassenger;
    }

    NPCType PickYieldNPC()
    {
        return NPCType.Elderly;
        // 추가 NPC 준비되면 랜덤 선택으로 변경
    }

    public void ResolveYield()
    {
        GameManager.Instance.Yield();
    }

    public void ResolveIgnore() { }

    public void ResolveSit()
    {
        GameManager.Instance.Sit();
    }

    public void ResolveStandIntentionally()
    {
        GameManager.Instance.StandIntentionally();
    }

    public void ResolveTriggerNPC()
    {
        GameManager.Instance.ChangeCondition(1);
        Debug.Log("[EventManager] TriggerNPC 처리 → 컨디션 +1 (설렘)");
    }
}
