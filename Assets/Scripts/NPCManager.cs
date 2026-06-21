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
    public RectTransform npcDialogueBox; // 말풍선 박스 (npcDialogueText의 부모) - NPC 월드 위치를 따라다님
    public Vector2 dialogueBoxScreenOffset = new Vector2(0f, 100f); // NPC 머리 위로 띄울 화면 픽셀 오프셋

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
        UpdateDialogueBoxPosition();
    }

    Vector3 GetNpcStandingPosition()
    {
        return playerTransform != null ? playerTransform.position + npcStandingOffset : npcStandingOffset;
    }

    // 말풍선 박스를 NPC의 현재 월드 위치(머리 위)로 이동 - Screen Space 캔버스라 World→Screen→로컬 변환 필요
    void UpdateDialogueBoxPosition()
    {
        if (npcDialogueBox == null || npcSpriteRenderer == null || Camera.main == null) return;

        var parentRect = npcDialogueBox.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, npcSpriteRenderer.transform.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out Vector2 localPoint))
            npcDialogueBox.anchoredPosition = localPoint + dialogueBoxScreenOffset;
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
        UpdateDialogueBoxPosition();

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

        if (seat.seatedNPCObject != null) // 그 자리에 이미 다른 오브젝트(경쟁에서 진 NPC 등)가 남아있었다면 정리
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }

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
