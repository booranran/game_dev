using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance;

    [Serializable]
    public class NPCData
    {
        public Sprite standingSprite;
        public Sprite sittingSprite;
        public float scale = 1f; // 이 NPC 타입의 스프라이트에 맞춘 배율 (서있기/앉기 공통)
        [TextArea] public string requestDialogue; // 양보 요청 시 대사
        [TextArea] public string thankYouDialogue; // 양보 받은 직후 감사 대사
    }

    [Header("NPC 데이터")]
    public NPCData elderly;
    public NPCData pregnant;
    // public NPCData passenger;
    // public NPCData dog;
    // public NPCData babyHappy;
    // public NPCData babyCrying;

    [Header("NPC 표시")]
    public SpriteRenderer npcSpriteRenderer;
    public TextMeshProUGUI npcDialogueText;

    [Header("앉은 NPC 설정")]
    public int seatedNPCSortingOrder = -29;

    private List<GameObject> seatedNPCObjects = new List<GameObject>();

    [Header("NPC 서있는 위치 (플레이어 기준 오프셋)")]
    public Transform playerTransform;
    public Vector3 npcStandingOffset;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EventManager.OnEventGenerated += OnEventGenerated;
    }

    void OnDisable()
    {
        EventManager.OnEventGenerated -= OnEventGenerated;
    }

    void OnEventGenerated(EventManager.EventType eventType, EventManager.NPCType npcType)
    {
        if (eventType == EventManager.EventType.TriggerNPC) return; // TriggerEventController가 전담

        // YieldNPC일 때만 서있는 NPC를 보여줌 - 그 외(EmptySeat/None/ElbowGame/BagDefense 등)는 항상 숨김
        // (예전엔 EmptySeat/None만 따로 체크하다가 ElbowGame/BagDefense가 빠져서 기본값 NPCType.Elderly가 그대로 노출되는 버그가 있었음)
        if (eventType == EventManager.EventType.YieldNPC)
        {
            ShowNPC(npcType);
            return;
        }
        HideNPC();
    }

    // 트리거 이벤트 등 동적 데이터로 NPC를 보여줄 때 사용
    public void ShowStanding(Sprite sprite, string dialogue, float scale = 1f)
    {
        if (npcSpriteRenderer)
        {
            npcSpriteRenderer.sprite = sprite;
            npcSpriteRenderer.transform.position = GetNpcStandingPosition();
            npcSpriteRenderer.transform.localScale = Vector3.one * scale;
        }
        if (npcDialogueText) npcDialogueText.text = dialogue;
    }

    Vector3 GetNpcStandingPosition()
    {
        return playerTransform != null ? playerTransform.position + npcStandingOffset : npcStandingOffset;
    }

    // 양보NPC 이벤트 등장 시 - 서있는 미리보기 + 평소 대사
    public void ShowNPC(EventManager.NPCType npcType)
    {
        NPCData data = GetData(npcType);
        if (data == null) return;

        if (npcSpriteRenderer)
        {
            npcSpriteRenderer.sprite = data.standingSprite;
            npcSpriteRenderer.transform.position = GetNpcStandingPosition();
            npcSpriteRenderer.transform.localScale = Vector3.one * data.scale;
        }

        if (npcDialogueText) npcDialogueText.text = data.requestDialogue;
    }

    // 양보 누른 직후(이번 턴) - 위치/스프라이트는 서있는 채로 그대로 두고, 대사만 감사 인사로 바꿈
    public void ShowThankYouDialogue(EventManager.NPCType npcType)
    {
        NPCData data = GetData(npcType);
        if (data == null || !npcDialogueText) return;
        npcDialogueText.text = data.thankYouDialogue;
    }

    // 다음 턴 시작 시 - 양보로 비워진 그 좌석에 실제로 앉힘 (seat는 양보 시점에 미리 캡쳐해둔 슬롯 - playerCurrentSeat는 그 사이 null이 돼서 못 씀)
    public void SeatNPCAt(EventManager.NPCType npcType, SeatManager.SeatSlot seat)
    {
        NPCData data = GetData(npcType);
        if (data == null || seat == null || seat.seatPosition == null) return;

        GameObject obj = new GameObject("SeatedNPC");
        obj.transform.position = seat.seatPosition.position;
        obj.transform.localScale = Vector3.one * data.scale;
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = data.sittingSprite;
        sr.sortingOrder = seatedNPCSortingOrder;
        seatedNPCObjects.Add(obj);
        seat.seatedNPCObject = obj;
    }

    public void ShowIgnoreReaction(EventManager.NPCType npcType) { }

    public void ClearSeatedNPCs()
    {
        foreach (var obj in seatedNPCObjects)
            if (obj) Destroy(obj);
        seatedNPCObjects.Clear();
    }

    public void HideNPC()
    {
        if (npcSpriteRenderer) npcSpriteRenderer.sprite = null;
        if (npcDialogueText) npcDialogueText.text = "";
    }

    public NPCData GetData(EventManager.NPCType npcType)
    {
        switch (npcType)
        {
            case EventManager.NPCType.Elderly: return elderly;
            case EventManager.NPCType.Pregnant: return pregnant;
            // 추가 NPC는 스프라이트 준비 후 주석 해제
            default: return null;
        }
    }
}
