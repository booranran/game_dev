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

        if (eventType == EventManager.EventType.EmptySeat ||
            eventType == EventManager.EventType.None)
        {
            HideNPC();
            return;
        }
        ShowNPC(npcType, false);
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

    public void ShowNPC(EventManager.NPCType npcType, bool afterYield)
    {
        NPCData data = GetData(npcType);
        if (data == null) return;

        if (afterYield)
        {
            if (npcSpriteRenderer) npcSpriteRenderer.sprite = null;

            Vector3 seatPos = GetNPCSittingPosition();
            GameObject obj = new GameObject("SeatedNPC");
            obj.transform.position = seatPos;
            obj.transform.localScale = Vector3.one * data.scale;
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = data.sittingSprite;
            sr.sortingOrder = seatedNPCSortingOrder;
            seatedNPCObjects.Add(obj);
            var slot = GetCurrentSeatSlot();
            if (slot != null)
            {
                slot.seatedNPCObject = obj;
                SeatManager.Instance?.SetupSeatClickDetection(slot);
            }
            SeatManager.Instance?.NPCOccupySeat();
        }
        else
        {
            if (npcSpriteRenderer)
            {
                npcSpriteRenderer.sprite = data.standingSprite;
                npcSpriteRenderer.transform.position = GetNpcStandingPosition();
                npcSpriteRenderer.transform.localScale = Vector3.one * data.scale;
            }
        }

        switch (npcType)
        {
            case EventManager.NPCType.Elderly:
                npcDialogueText.text = afterYield ? "고맙우이~" : "아이고~ 삭신이 쑤시네";
                break;
            case EventManager.NPCType.Pregnant:
                npcDialogueText.text = afterYield ? "감사합니다!" : "아이고 배가 무거워서 힘드네요...";
                break;
            // 추가 NPC는 스프라이트 준비 후 주석 해제
            // case EventManager.NPCType.Passenger: ...
            // case EventManager.NPCType.Dog: ...
            // case EventManager.NPCType.BabyHappy: ...
            // case EventManager.NPCType.BabyCrying: ...
        }
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

    SeatManager.SeatSlot GetCurrentSeatSlot()
    {
        var sm = SeatManager.Instance;
        if (sm == null) return null;
        if (sm.currentEmptySeat != null) return sm.currentEmptySeat;
        if (sm.playerCurrentSeat != null) return sm.playerCurrentSeat;
        return null;
    }

    Vector3 GetNPCSittingPosition()
    {
        var slot = GetCurrentSeatSlot();
        if (slot != null && slot.seatPosition != null)
            return slot.seatPosition.position;
        return Vector3.zero;
    }

    NPCData GetData(EventManager.NPCType npcType)
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
