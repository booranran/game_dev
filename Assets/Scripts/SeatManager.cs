using UnityEngine;
using System.Collections.Generic;
using System;

public class SeatManager : MonoBehaviour
{
    public static SeatManager Instance;

    public enum SeatType { Normal, PregnantSeat, ElderlySeat }

    // 실루엣 풀의 한 항목 - 평소 스프라이트와 Phase1 후보로 뜰 때 보여줄 강조 스프라이트가 짝으로 묶임
    [Serializable]
    public struct SilhouetteSprite
    {
        public Sprite normal;
        public Sprite highlighted;
    }

    [Serializable]
    public class SeatSlot
    {
        public Transform seatPosition;
        public Transform standingInFrontPosition;
        public GameObject silhouette;
        public SeatType seatType = SeatType.Normal;
        [HideInInspector] public bool isOccupied = true;
        [HideInInspector] public GameObject seatedNPCObject;
        [HideInInspector] public GameObject spawnedIcon; // 동적으로 생성된 선택 아이콘 인스턴스
        [HideInInspector] public int index;
        [HideInInspector] public Sprite currentSilhouetteSprite; // 지금 이 자리에 보이는 평소 스프라이트
        [HideInInspector] public Sprite currentHighlightSprite;  // 위와 짝이 되는 Phase1 후보 강조 스프라이트
    }

    // 특수석 타입이 전담하는 NPC 타입 (배려점수 페널티 매칭, 좌석 채움 시 사용)
    public static EventManager.NPCType? GetMatchingNPCType(SeatType type)
    {
        switch (type)
        {
            case SeatType.PregnantSeat: return EventManager.NPCType.Pregnant;
            case SeatType.ElderlySeat: return EventManager.NPCType.Elderly;
            default: return null;
        }
    }

    // 해당 타입의 특수석이 (하나 이상 있다면) 전부 채워져있는지 - 양보NPC 타입 편향에 사용
    public bool IsSpecialSeatTypeFull(SeatType type)
    {
        bool foundAny = false;
        foreach (var seat in seats)
        {
            if (seat.seatType != type) continue;
            foundAny = true;
            if (!seat.isOccupied) return false;
        }
        return foundAny;
    }

    // 해당 타입의 특수석이 씬에 하나라도 존재하는지 (없으면 "채워짐" 제약 자체가 의미 없음)
    public bool HasSeatType(SeatType type)
    {
        foreach (var seat in seats)
            if (seat.seatType == type) return true;
        return false;
    }

    public SeatSlot[] seats;
    public bool randomizePlayerSeat = false;
    public int playerStartSeatIndex = 0;

    [Header("좌석 실루엣 랜덤 스프라이트 풀 (좌석 수보다 많아도 됨, 시작 시 중복 없이 배정 - 평소/강조 스프라이트 짝으로 등록)")]
    public SilhouetteSprite[] silhouetteSpritePool;

    [Header("빈자리 선택 아이콘")]
    public GameObject selectableIconPrefab; // 좌석 위에 동적으로 생성될 프리팹 하나로 통일
    public float selectableIconOffsetX = 0f; // 좌석 x좌표에 더해지는 오프셋 (좌석마다 다름)
    public float selectableIconFixedY = 0.5f; // y좌표는 좌석과 무관하게 항상 이 값 사용

    [Header("빈자리 선택 콜리더 (의도적으로 좌석까지 덮도록 넓게 잡음 - 아이콘뿐 아니라 좌석 클릭도 선택되게)")]
    public Vector2 selectableIconColliderSize = new Vector2(2.8f, 5f); // 기존 프리팹에 박혀있던 값 그대로 이식
    public Vector2 selectableIconColliderOffset = new Vector2(-0.1f, -1.8f);

    [Header("특수석 배려점수 페널티 (앉는 순간 1회 적용 / 해당 NPC 양보 무시 시 추가 적용)")]
    public float pregnantSeatBasePenalty = -15f;
    public float pregnantSeatIgnoreExtraPenalty = -10f; // 무시 시 기본+추가 = -25
    public float elderlySeatBasePenalty = -8f;
    public float elderlySeatIgnoreExtraPenalty = -7f; // 무시 시 기본+추가 = -15

    [Header("특수석 채움 설정 (해당 NPC 타입만 채워지고, 일반 좌석보다 채움 확률이 낮음)")]
    [Range(0f, 1f)] public float specialSeatFillChance = 0.4f;

    public float GetSeatBasePenalty(SeatType type)
    {
        switch (type)
        {
            case SeatType.PregnantSeat: return pregnantSeatBasePenalty;
            case SeatType.ElderlySeat: return elderlySeatBasePenalty;
            default: return 0f;
        }
    }

    public float GetSeatIgnoreExtraPenalty(SeatType type)
    {
        switch (type)
        {
            case SeatType.PregnantSeat: return pregnantSeatIgnoreExtraPenalty;
            case SeatType.ElderlySeat: return elderlySeatIgnoreExtraPenalty;
            default: return 0f;
        }
    }

    public SeatSlot currentEmptySeat { get; private set; }
    public SeatSlot playerCurrentSeat { get; private set; }
    public List<SeatSlot> openSeats { get; } = new List<SeatSlot>(); // 선택 대기 중인 빈자리들 (1개 이상)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        currentEmptySeat = null;
        AssignRandomSilhouetteSprites();
        for (int i = 0; i < seats.Length; i++)
        {
            seats[i].index = i;
            seats[i].isOccupied = true;
        }
    }

    // NPCManager.Instance 보장을 위해 모든 Awake() 이후로 미룸 (특수석 초기 상태 채우기)
    void Start()
    {
        foreach (var seat in seats)
        {
            if (seat.seatType != SeatType.Normal)
                FillSpecialSeat(seat);
        }
    }

    // 게임 시작 시 풀에서 중복 없이 랜덤하게 뽑아서 좌석마다 다른 스프라이트로 배정 (특수석은 제외 - Start()에서 따로 채워짐)
    void AssignRandomSilhouetteSprites()
    {
        if (silhouetteSpritePool == null || silhouetteSpritePool.Length == 0) return;

        var pool = new List<SilhouetteSprite>(silhouetteSpritePool);
        foreach (var seat in seats)
        {
            if (seat.seatType != SeatType.Normal) continue;
            if (seat.silhouette == null || pool.Count == 0) continue;
            var sr = seat.silhouette.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) continue;

            int r = UnityEngine.Random.Range(0, pool.Count);
            sr.sprite = pool[r].normal;
            seat.currentSilhouetteSprite = pool[r].normal;
            seat.currentHighlightSprite = pool[r].highlighted;
            pool.RemoveAt(r);
        }
    }

    public void HidePlayerSeat()
    {
        if (seats.Length == 0) return;
        if (randomizePlayerSeat)
            playerStartSeatIndex = UnityEngine.Random.Range(0, seats.Length);

        var slot = seats[playerStartSeatIndex];
        slot.isOccupied = false;
        if (slot.silhouette) slot.silhouette.SetActive(false);
        if (slot.seatedNPCObject) // 특수석 Start() 초기화와 순서가 겹쳐도 안전하게 - 그 자리에 NPC가 먼저 채워져있었다면 치움
        {
            Destroy(slot.seatedNPCObject);
            slot.seatedNPCObject = null;
        }
        playerCurrentSeat = slot;
        Debug.Log($"[SeatManager] 플레이어 시작 자리 숨김 → index {playerStartSeatIndex}");
    }

    // 한 번에 여러 자리를 동시에 열어서 openSeats에 등록 (플레이어가 그중 하나를 클릭해서 선택)
    public void OpenSeats(int count)
    {
        List<SeatSlot> occupied = new List<SeatSlot>();
        foreach (var seat in seats)
            if (seat.isOccupied) occupied.Add(seat);

        int n = Mathf.Min(count, occupied.Count);
        for (int i = 0; i < n; i++)
        {
            int r = UnityEngine.Random.Range(0, occupied.Count);
            var seat = occupied[r];
            occupied.RemoveAt(r);

            seat.isOccupied = false;
            if (seat.silhouette) seat.silhouette.SetActive(false);
            if (seat.seatedNPCObject)
            {
                Destroy(seat.seatedNPCObject);
                seat.seatedNPCObject = null;
            }

            openSeats.Add(seat);
            ActivateSelectableIcon(seat);
            Debug.Log($"[SeatManager] OpenSeats → index {seat.index} 열림, 아이콘 생성 {(seat.spawnedIcon != null ? "성공" : "실패(prefab/seatPosition 확인)")}");
        }

        // 특수석은 채움 확률이 낮아서 이미 비어있는 채로 방치돼있을 수 있음 - 빈자리 이벤트가 뜰 때마다 같이 선택지로 노출
        foreach (var seat in seats)
        {
            if (seat.seatType == SeatType.Normal) continue;
            if (seat.isOccupied || openSeats.Contains(seat)) continue;
            openSeats.Add(seat);
            ActivateSelectableIcon(seat);
            Debug.Log($"[SeatManager] OpenSeats → 특수석 index {seat.index}도 이미 빈자리라 같이 노출");
        }

        Debug.Log($"[SeatManager] 빈자리 {n}개 열림 (요청 {count}개)");
    }

    // 열린 자리 중 하나를 플레이어가 선택 - 그 자리만 currentEmptySeat로 넘기고 나머지는 openSeats에 남음
    public void SelectOpenSeat(SeatSlot seat)
    {
        if (!openSeats.Remove(seat)) return;
        DeactivateSelectableIcon(seat);
        currentEmptySeat = seat;
    }

    // 좌석 하나를 선택한 직후, 남은 빈자리 아이콘들도 더 못 누르게 전부 비활성화 (중복 선택 방지)
    public void DeactivateAllOpenSeatIcons()
    {
        foreach (var seat in openSeats)
            DeactivateSelectableIcon(seat);
    }

    // openSeats에 남아있는 자리들을 전부 다른 랜덤 NPC로 채움 (서있기 선택 시 즉시, 다음 턴 시작 시 자동)
    public void FillAllOpenSeats()
    {
        foreach (var seat in openSeats)
        {
            DeactivateSelectableIcon(seat);
            FillSeatWithRandomNPC(seat);
        }
        openSeats.Clear();
    }

    // 특정 좌석에 다른 점유 좌석에서 빌려온 스프라이트로 랜덤 NPC를 채워 앉힘
    public void FillSeatWithRandomNPC(SeatSlot seat)
    {
        Debug.Log($"[SeatManager] FillSeatWithRandomNPC 호출 → index {seat.index} (호출 스택은 Console에서 이 로그 클릭하면 확인 가능)");
        if (seat.seatPosition == null) return;

        if (seat.seatType != SeatType.Normal)
        {
            FillSpecialSeat(seat);
            return;
        }

        var pool = new List<SeatSlot>();
        foreach (var s in seats)
        {
            if (s.index == seat.index) continue;
            if (s.isOccupied && s.silhouette != null)
            {
                var sr = s.silhouette.GetComponentInChildren<SpriteRenderer>(true);
                if (sr && sr.sprite != null) pool.Add(s);
            }
        }
        if (pool.Count == 0) return;

        if (seat.silhouette) seat.silhouette.SetActive(false);
        if (seat.seatedNPCObject)
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }

        var sourceSeat = pool[UnityEngine.Random.Range(0, pool.Count)];
        var sourceSr = sourceSeat.silhouette.GetComponentInChildren<SpriteRenderer>(true);

        var go = new GameObject("FilledNPC");
        go.transform.position = seat.seatPosition.position;
        // 스프라이트만 빌려오고 스케일은 이 좌석(target) 자신의 실루엣 스케일을 따름 - donor 좌석 스케일을 그대로 쓰면
        // 좌석마다 원근감에 맞게 다르게 잡아둔 크기가 안 맞아서 엉뚱하게 커지거나 작아짐
        go.transform.localScale = seat.silhouette != null ? seat.silhouette.transform.localScale : sourceSeat.silhouette.transform.localScale;
        var sr2 = go.AddComponent<SpriteRenderer>();
        sr2.sprite = sourceSr.sprite;
        sr2.sortingOrder = -29;

        seat.seatedNPCObject = go;
        seat.isOccupied = true;
        seat.currentSilhouetteSprite = sourceSeat.currentSilhouetteSprite; // 빌려온 실루엣과 짝이 맞는 평소/강조 스프라이트도 같이 가져옴
        seat.currentHighlightSprite = sourceSeat.currentHighlightSprite;
    }

    // 특수석은 일반 풀에서 빌려오지 않고 그 좌석이 전담하는 NPC 타입만, 그것도 낮은 확률로만 채움
    // (나머지는 빈자리로 남아 일반 좌석보다 더 자주 비어있는 것처럼 보이게 됨)
    void FillSpecialSeat(SeatSlot seat)
    {
        if (seat.silhouette) seat.silhouette.SetActive(false);
        if (seat.seatedNPCObject)
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }
        seat.isOccupied = false;

        if (UnityEngine.Random.value > specialSeatFillChance) return; // 확률 미달 - 빈자리로 유지

        var npcType = GetMatchingNPCType(seat.seatType);
        var data = npcType.HasValue ? NPCManager.Instance?.GetData(npcType.Value) : null;
        if (data == null || data.sittingSprite == null) return;

        var go = new GameObject("SpecialSeatNPC");
        go.transform.position = seat.seatPosition.position;
        go.transform.localScale = Vector3.one * data.scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = data.sittingSprite;
        sr.sortingOrder = NPCManager.Instance.seatedNPCSortingOrder;

        seat.seatedNPCObject = go;
        seat.isOccupied = true;
    }

    // 좌석 위치에 선택 아이콘을 생성 (Phase1 후보 표시, 빈자리 선택 등 공용으로 사용)
    // extraY: 좌석에 사람이 앉아있는 경우처럼 기본 높이보다 더 올려야 할 때 추가로 더하는 값
    public GameObject SpawnSeatIcon(SeatSlot seat, float extraY = 0f)
    {
        if (selectableIconPrefab == null || seat.seatPosition == null) return null;
        if (seat.spawnedIcon != null) return seat.spawnedIcon;

        Vector3 iconPos = seat.seatPosition.position;
        iconPos.x += selectableIconOffsetX;
        iconPos.y = selectableIconFixedY + extraY;

        var icon = Instantiate(selectableIconPrefab, iconPos, Quaternion.identity);
        var col = icon.GetComponent<BoxCollider2D>();
        if (col == null) col = icon.AddComponent<BoxCollider2D>();

        // 아이콘뿐 아니라 그 아래 좌석까지 클릭 영역으로 잡기 위해 의도적으로 넓게 설정 (인스펙터에서 조절)
        col.size = selectableIconColliderSize;
        col.offset = selectableIconColliderOffset;

        seat.spawnedIcon = icon;
        return icon;
    }

    public void RemoveSeatIcon(SeatSlot seat)
    {
        if (seat.spawnedIcon == null) return;
        Destroy(seat.spawnedIcon);
        seat.spawnedIcon = null;
    }

    void ActivateSelectableIcon(SeatSlot seat)
    {
        var icon = SpawnSeatIcon(seat);
        if (icon == null) return;

        var detector = icon.GetComponent<OpenSeatClickDetector>();
        if (detector == null)
            detector = icon.AddComponent<OpenSeatClickDetector>();
        detector.seat = seat;
    }

    void DeactivateSelectableIcon(SeatSlot seat) => RemoveSeatIcon(seat);

    public void OccupySeat()
    {
        if (currentEmptySeat == null) return;
        if (currentEmptySeat.seatedNPCObject != null)
        {
            Destroy(currentEmptySeat.seatedNPCObject);
            currentEmptySeat.seatedNPCObject = null;
        }
        currentEmptySeat.isOccupied = true;
        playerCurrentSeat = currentEmptySeat;
        currentEmptySeat = null;
    }

    public void ClearPlayerSeat()
    {
        if (playerCurrentSeat != null)
            playerCurrentSeat.isOccupied = true;
        playerCurrentSeat = null;
    }

    public void NPCOccupySeat()
    {
        if (currentEmptySeat != null)
        {
            currentEmptySeat.isOccupied = true;
            currentEmptySeat = null;
        }
    }

    public SeatSlot[] GetCandidateSeats(int count)
    {
        List<SeatSlot> occupied = new List<SeatSlot>();
        foreach (var seat in seats)
            if (seat.isOccupied) occupied.Add(seat);

        List<SeatSlot> result = new List<SeatSlot>();
        while (result.Count < count && occupied.Count > 0)
        {
            int r = UnityEngine.Random.Range(0, occupied.Count);
            result.Add(occupied[r]);
            occupied.RemoveAt(r);
        }
        return result.ToArray();
    }

    // Phase 1에서 정답 자리를 빈자리로 등록할 때 사용
    public void SetCurrentEmptySeat(SeatSlot seat)
    {
        currentEmptySeat = seat;
        seat.isOccupied = false;
        if (seat.silhouette) seat.silhouette.SetActive(false);
        if (seat.seatedNPCObject) // 그 자리가 FillSeatWithRandomNPC 등으로 이미 채워져있던 경우 - 실루엣만 꺼서는 안 사라짐
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }
    }

    public void ResetAllSeats()
    {
        foreach (var seat in seats)
        {
            seat.isOccupied = true;
            if (seat.silhouette) seat.silhouette.SetActive(true);
            DeactivateSelectableIcon(seat);
        }
        openSeats.Clear();
        currentEmptySeat = null;
    }
}
