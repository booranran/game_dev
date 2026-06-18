using UnityEngine;

public class BoardingController : MonoBehaviour
{
    public static BoardingController Instance;

    [Header("보딩 영역 (이 안에 격자 슬롯을 만들어서 사용)")]
    public Transform boardingAreaCenter;
    public Vector2 boardingAreaSize = new Vector2(3f, 1f);
    public int slotColumns = 3;
    public int slotRows = 2;

    [Header("일반인 서있는 스프라이트 (랜덤)")]
    public Sprite[] boardingSprites;
    public float boardingScale = 0.09f; // 다른 NPC들과 맞춘 기본값

    [Header("정거장당 탑승 인원 범위")]
    public int minBoardingCount = 0;
    public int maxBoardingCount = 5;

    [Header("정렬 순서 (앉은 실루엣은 -30)")]
    public int baseSortingOrder = -20; // 실루엣보다 항상 앞에 보이도록
    public float sortingOrderYScale = 10f; // y가 작을수록(아래=앞) 소팅오더를 얼마나 더 올릴지

    [Header("제외 영역 (플레이어/주요 NPC 근처는 비워둠)")]
    public Transform playerTransform;
    public float exclusionRadius = 1f;

    public int CurrentBoardingCount { get; private set; }

    private Vector3[] slotPositions;
    private GameObject[] slotOccupants;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 턴 시작 시 가장 먼저 호출 - 인원수만 굴림 (EventManager가 가방방어 가중치 계산에 바로 씀)
    // 아직 이번 턴 이벤트/NPC 위치가 안 정해진 시점이라 실제 배치는 PlaceBoardingNPCs()에서 따로 함
    public void RollBoarding()
    {
        CurrentBoardingCount = Random.Range(minBoardingCount, maxBoardingCount + 1);
    }

    // EventManager.GenerateEvent() 이후(이번 턴 NPC 위치가 확정된 후) 호출 - 실제 보딩 NPC 배치
    public void PlaceBoardingNPCs()
    {
        EnsureSlots();
        EvictExcludedOccupants(); // 플레이어/NPC가 가까워진 자리는 비워둠

        if (slotPositions.Length == 0 || boardingSprites == null || boardingSprites.Length == 0)
        {
            Debug.Log($"[BoardingController] 이번 정거장 보딩 인원: {CurrentBoardingCount}명 (영역/스프라이트 미설정, 비주얼 생략)");
            return;
        }

        int targetCount = Mathf.Min(CurrentBoardingCount, slotPositions.Length);
        int currentCount = CountOccupied();

        if (targetCount > currentCount)
        {
            var emptySlots = new System.Collections.Generic.List<int>();
            for (int i = 0; i < slotOccupants.Length; i++)
                if (slotOccupants[i] == null && !IsSlotExcluded(slotPositions[i])) emptySlots.Add(i);

            int toAdd = targetCount - currentCount;
            for (int i = 0; i < toAdd && emptySlots.Count > 0; i++)
            {
                int pick = Random.Range(0, emptySlots.Count);
                SpawnAtSlot(emptySlots[pick]);
                emptySlots.RemoveAt(pick);
            }
        }
        else if (targetCount < currentCount)
        {
            var filledSlots = new System.Collections.Generic.List<int>();
            for (int i = 0; i < slotOccupants.Length; i++)
                if (slotOccupants[i] != null) filledSlots.Add(i);

            int toRemove = currentCount - targetCount;
            for (int i = 0; i < toRemove && filledSlots.Count > 0; i++)
            {
                int pick = Random.Range(0, filledSlots.Count);
                RemoveAtSlot(filledSlots[pick]);
                filledSlots.RemoveAt(pick);
            }
        }

        Debug.Log($"[BoardingController] 이번 정거장 보딩 인원: {targetCount}명 (유지 {Mathf.Min(currentCount, targetCount)} / 변화 {Mathf.Abs(targetCount - currentCount)})");
    }

    void EnsureSlots()
    {
        if (slotPositions != null) return;
        slotPositions = ComputeSlotPositions();
        slotOccupants = new GameObject[slotPositions.Length];
    }

    Vector3[] ComputeSlotPositions()
    {
        if (boardingAreaCenter == null) return new Vector3[0];

        int total = Mathf.Max(1, slotRows * slotColumns);
        var result = new Vector3[total];
        int idx = 0;
        for (int row = 0; row < slotRows; row++)
        {
            for (int col = 0; col < slotColumns; col++)
            {
                float xt = slotColumns > 1 ? (float)col / (slotColumns - 1) : 0.5f;
                float yt = slotRows > 1 ? (float)row / (slotRows - 1) : 0.5f;
                float x = Mathf.Lerp(-boardingAreaSize.x * 0.5f, boardingAreaSize.x * 0.5f, xt);
                float y = Mathf.Lerp(-boardingAreaSize.y * 0.5f, boardingAreaSize.y * 0.5f, yt);
                result[idx] = boardingAreaCenter.position + new Vector3(x, y, 0f);
                idx++;
            }
        }
        return result;
    }

    bool IsSlotExcluded(Vector3 slotPos)
    {
        if (playerTransform != null &&
            Vector2.Distance(slotPos, playerTransform.position) < exclusionRadius)
            return true;

        var npcManager = NPCManager.Instance;
        if (npcManager != null && npcManager.npcSpriteRenderer != null &&
            Vector2.Distance(slotPos, npcManager.npcSpriteRenderer.transform.position) < exclusionRadius)
            return true;

        return false;
    }

    void EvictExcludedOccupants()
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] != null && IsSlotExcluded(slotPositions[i]))
                RemoveAtSlot(i);
        }
    }

    int CountOccupied()
    {
        int count = 0;
        foreach (var go in slotOccupants)
            if (go != null) count++;
        return count;
    }

    void SpawnAtSlot(int slotIndex)
    {
        Vector3 pos = slotPositions[slotIndex];
        var go = new GameObject("BoardingNPC");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * boardingScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boardingSprites[Random.Range(0, boardingSprites.Length)];
        sr.sortingOrder = baseSortingOrder - Mathf.RoundToInt(pos.y * sortingOrderYScale);
        slotOccupants[slotIndex] = go;
    }

    void RemoveAtSlot(int slotIndex)
    {
        if (slotOccupants[slotIndex] == null) return;
        Destroy(slotOccupants[slotIndex]);
        slotOccupants[slotIndex] = null;
    }

    void OnDrawGizmosSelected()
    {
        if (boardingAreaCenter == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boardingAreaCenter.position, new Vector3(boardingAreaSize.x, boardingAreaSize.y, 0f));

        Gizmos.color = Color.yellow;
        foreach (var pos in ComputeSlotPositions())
            Gizmos.DrawSphere(pos, 0.05f);

        Gizmos.color = Color.red;
        if (playerTransform != null)
            Gizmos.DrawWireSphere(playerTransform.position, exclusionRadius);
        var npcManager = NPCManager.Instance;
        if (npcManager != null && npcManager.npcSpriteRenderer != null)
            Gizmos.DrawWireSphere(npcManager.npcSpriteRenderer.transform.position, exclusionRadius);
    }
}
