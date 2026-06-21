using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class BagDefenseController : MonoBehaviour
{
    public static BagDefenseController Instance;
    public static event Action<int, int> OnBagGameEnd; // (conditionDamage, healthDamage)

    [Header("UI 참조")]
    public GameObject bagDefensePanel;
    public RectTransform noteSpawnParent;
    public RectTransform hitZoneLine;
    public RectTransform hitZoneRect;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI resultText;

    [Header("레인 설정")]
    public RectTransform[] laneButtonRects; // 레인 버튼 4개, 순서대로 0~3
    [HideInInspector] public float[] laneXPositions = { -270f, -90f, 90f, 270f };
    public float spawnY = 500f;
    [HideInInspector] public float hitWindow = 80f;

    [Header("노트 비주얼")]
    public Sprite[] noteSprites; // 지정하면 매번 랜덤으로 하나 골라 사용, 비어있으면 단색
    public Color noteColor = Color.cyan;
    public Vector2 noteSize = new Vector2(100f, 120f); // noteSprites가 비어있을 때 단색 노트 크기
    public float noteScale = 1f; // 이미지 사용 시 원본 크기에 곱하는 배율

    [Header("게임 설정")]
    public float gameDuration = 15f;
    public float spawnCutoffBuffer = 0.2f; // 마지막 노트가 끝까지 내려와 판정날 시간을 보장하는 여유분

    [Header("노트 속도/스폰 간격 - 학생")]
    public float studentNoteSpeed = 400f;
    public float studentMinSpawnInterval = 0.4f;
    public float studentMaxSpawnInterval = 1.2f;

    [Header("노트 속도/스폰 간격 - 직장인")]
    public float workerNoteSpeed = 480f;
    public float workerMinSpawnInterval = 0.3f;
    public float workerMaxSpawnInterval = 1.0f;

    bool IsWorker => GameManager.Instance != null && GameManager.Instance.characterType == GameManager.CharacterType.Worker;
    float NoteSpeed => IsWorker ? workerNoteSpeed : studentNoteSpeed;
    float MinSpawnInterval => IsWorker ? workerMinSpawnInterval : studentMinSpawnInterval;
    float MaxSpawnInterval => IsWorker ? workerMaxSpawnInterval : studentMaxSpawnInterval;

    [Header("등급 기준 (히트율 % - 5단계, S는 100이라 풀콤보여야만 S)")]
    public float gradeS = 100f;
    public float gradeA = 90f;
    public float gradeB = 70f;
    public float gradeC = 50f;
    public float gradeD = 30f;

    [Header("등급별 체력 데미지 (항상 마이너스, 잘할수록 적게 깎임)")]
    public int healthDamageS = -2;
    public int healthDamageA = -4;
    public int healthDamageB = -6;
    public int healthDamageC = -8;
    public int healthDamageD = -10;

    [Header("등급별 컨디션 데미지 (서서 방어한 피로 - 항상 마이너스, 잘할수록 적게 깎임)")]
    public int conditionDamageS = -1;
    public int conditionDamageA = -1;
    public int conditionDamageB = -2;
    public int conditionDamageC = -2;
    public int conditionDamageD = -3;

    [Header("독백 (등급별 5종 - TurnController가 메인 화면에 표시, 턴 전환 분리용)")]
    [TextArea] public string[] startMonologues;
    [TextArea] public string[] sMonologues;
    [TextArea] public string[] aMonologues;
    [TextArea] public string[] bMonologues;
    [TextArea] public string[] cMonologues;
    [TextArea] public string[] dMonologues;

    [Header("노트 히트/미스 효과음")]
    public AudioClip noteHitSFX;
    public AudioClip noteMissSFX;

    [Header("등급별 결과 효과음 (ResultText가 떠있는 동안 재생 - 효과음 끝나야 패널 닫힘)")]
    public AudioClip resultSSFX;
    public AudioClip resultASFX;
    public AudioClip resultBSFX;
    public AudioClip resultCSFX;
    public AudioClip resultDSFX;

    private float gameTimer;
    private bool isPlaying;
    private int totalNotes;
    private int hitNotes;
    private int _pendingConditionDamage;
    private int _pendingHealthDamage;
    private string _pendingGrade;
    private float hitZoneY;
    private float hitZoneTop;
    private float hitZoneBottom;
    private List<BagNote> activeNotes = new List<BagNote>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public string GetStartLine() => PickLine(startMonologues);
    public string GetEndLine() => PickLine(GetMonologuesForGrade(_pendingGrade));

    string[] GetMonologuesForGrade(string grade)
    {
        switch (grade)
        {
            case "S": return sMonologues;
            case "A": return aMonologues;
            case "B": return bMonologues;
            case "C": return cMonologues;
            default: return dMonologues;
        }
    }

    string PickLine(string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    public void StartBagGame()
    {
        BoardingController.Instance?.ClearSurgeNPCs(); // 연출용 임시 NPC 정리 (진짜 보딩 NPC는 그대로 유지됨)

        // 버튼 위치에서 레인 X 자동 계산
        if (laneButtonRects != null && laneButtonRects.Length == 4)
        {
            for (int i = 0; i < 4; i++)
            {
                if (laneButtonRects[i] == null) continue;
                // 버튼의 월드 X → noteSpawnParent 로컬 X로 변환
                Vector2 local = noteSpawnParent.InverseTransformPoint(laneButtonRects[i].position);
                laneXPositions[i] = local.x;
            }
            Debug.Log($"[가방] 레인 X 자동설정: {laneXPositions[0]:F0}, {laneXPositions[1]:F0}, {laneXPositions[2]:F0}, {laneXPositions[3]:F0}");
        }

        if (hitZoneRect != null)
        {
            float cy = hitZoneRect.anchoredPosition.y;
            float half = hitZoneRect.rect.height * 0.5f; // sizeDelta는 stretch anchor시 실제 크기 아님
            hitZoneY = cy;
            hitZoneTop = cy + half;
            hitZoneBottom = cy - half;
        }
        else
        {
            hitZoneY = hitZoneLine != null ? hitZoneLine.anchoredPosition.y : -350f;
            hitZoneTop = hitZoneY + hitWindow;
            hitZoneBottom = hitZoneY - hitWindow;
        }
        totalNotes = 0;
        hitNotes = 0;
        gameTimer = gameDuration;
        isPlaying = true;
        activeNotes.Clear();
        bagDefensePanel.SetActive(true);
        if (resultText) resultText.text = "";
        StartCoroutine(SpawnNotes());
        Debug.Log($"[가방] 게임 시작 | 히트존 Y: {hitZoneY} | 범위: {hitZoneBottom:F0}~{hitZoneTop:F0}");
    }

    IEnumerator SpawnNotes()
    {
        // 마지막에 스폰된 노트도 끝까지 내려와 판정날 시간을 보장하기 위해, 그만큼 일찍 스폰을 멈춤
        float fallTime = (spawnY - hitZoneBottom) / NoteSpeed + spawnCutoffBuffer;

        while (isPlaying && gameTimer > fallTime)
        {
            float interval = UnityEngine.Random.Range(MinSpawnInterval, MaxSpawnInterval);
            yield return new WaitForSeconds(interval);
            if (!isPlaying || gameTimer <= fallTime) break;
            SpawnNote(UnityEngine.Random.Range(0, 4));
        }
    }

    void SpawnNote(int lane)
    {
        var go = new GameObject($"Note_L{lane}", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(noteSpawnParent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = new Vector2(laneXPositions[lane], spawnY);
        var img = go.GetComponent<Image>();
        if (noteSprites != null && noteSprites.Length > 0)
        {
            img.sprite = noteSprites[UnityEngine.Random.Range(0, noteSprites.Length)];
            img.color = Color.white;
            img.preserveAspect = true;
            img.SetNativeSize(); // 스프라이트 원본 비율로 크기 설정 (찌그러짐 방지)
            rt.sizeDelta *= noteScale;
        }
        else
        {
            img.color = noteColor;
            rt.sizeDelta = noteSize;
        }
        var note = go.AddComponent<BagNote>();
        note.Init(lane, NoteSpeed, hitZoneY, hitZoneBottom, this);
        activeNotes.Add(note);
        totalNotes++;
    }

    public void OnLaneButton(int lane)
    {
        if (!isPlaying) return;

        BagNote closest = null;
        float closestDist = float.MaxValue;

        foreach (var note in activeNotes)
        {
            if (note == null || note.lane != lane || note.isHit || note.isMissed) continue;
            float dist = Mathf.Abs(note.GetY() - hitZoneY);
            if (dist < closestDist) { closestDist = dist; closest = note; }
        }

        // 히트존 범위 안에 노트 중심이 있으면 판정
        if (closest != null && closest.GetY() >= hitZoneBottom && closest.GetY() <= hitZoneTop)
        {
            closest.isHit = true;
            hitNotes++;
            activeNotes.Remove(closest);
            Destroy(closest.gameObject);
            AudioManager.Instance?.PlaySFX(noteHitSFX);
            Debug.Log($"[가방] 히트 레인{lane} | {hitNotes}/{totalNotes}");
        }
    }

    public void OnNoteMissed(BagNote note)
    {
        activeNotes.Remove(note);
        Destroy(note.gameObject);
        AudioManager.Instance?.PlaySFX(noteMissSFX);
        Debug.Log($"[가방] 미스 레인{note.lane} | {hitNotes}/{totalNotes}");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.Digit3].wasPressedThisFrame)
        {
            // 팔꿈치 게임 중이면 강제 종료 후 시작
            if (ElbowGameController.Instance != null && ElbowGameController.Instance.IsPlaying)
            {
                ElbowGameController.Instance.ForceEnd();
            }
            StartBagGame();
        }

        if (!isPlaying) return;

        // 컴퓨터 테스트용 키보드 (A S D F)
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) OnLaneButton(0);
            if (Keyboard.current.sKey.wasPressedThisFrame) OnLaneButton(1);
            if (Keyboard.current.dKey.wasPressedThisFrame) OnLaneButton(2);
            if (Keyboard.current.fKey.wasPressedThisFrame) OnLaneButton(3);
        }

        gameTimer -= Time.deltaTime;
        if (timerText) timerText.text = $"{Mathf.CeilToInt(gameTimer)}";
        if (gameTimer <= 0f) EndGame();
    }

    void EndGame()
    {
        isPlaying = false;
        StopAllCoroutines();

        foreach (var note in activeNotes)
            if (note) Destroy(note.gameObject);
        activeNotes.Clear();

        float hitRate = totalNotes > 0 ? (float)hitNotes / totalNotes * 100f : 0f;
        _pendingGrade = GetGradeString(hitRate);
        _pendingConditionDamage = GetConditionDamage(_pendingGrade);
        _pendingHealthDamage = GetHealthDamage(_pendingGrade);

        if (resultText) resultText.text = $"등급 {_pendingGrade}\n{hitNotes} / {totalNotes} 방어";
        Debug.Log($"[가방] 종료 | 히트율: {hitRate:F1}% | 등급: {_pendingGrade} | 컨디션: {_pendingConditionDamage} | 체력: {_pendingHealthDamage}");

        StartCoroutine(ShowResultThenEnd());
    }

    // 결과 효과음이 재생되는 동안 패널 BGM은 잠깐 음소거 - 효과음이 끝나야 패널이 닫힘 (ElbowGameController와 동일한 연출)
    IEnumerator ShowResultThenEnd()
    {
        AudioClip sfx = GetResultSFX(_pendingGrade);
        AudioManager.Instance?.DuckBGM(true);

        if (sfx != null)
        {
            AudioManager.Instance?.PlaySFX(sfx);
            yield return new WaitForSeconds(sfx.length);
        }
        else
        {
            yield return new WaitForSeconds(1.5f); // 효과음 없으면 기존처럼 1.5초
        }

        AudioManager.Instance?.DuckBGM(false);
        EndGameDelayed();
    }

    void EndGameDelayed()
    {
        bagDefensePanel.SetActive(false);
        OnBagGameEnd?.Invoke(_pendingConditionDamage, _pendingHealthDamage);
    }

    string GetGradeString(float hitRate)
    {
        if (hitRate >= gradeS) return "S";
        if (hitRate >= gradeA) return "A";
        if (hitRate >= gradeB) return "B";
        if (hitRate >= gradeC) return "C";
        return "D";
    }

    int GetConditionDamage(string grade)
    {
        switch (grade)
        {
            case "S": return conditionDamageS;
            case "A": return conditionDamageA;
            case "B": return conditionDamageB;
            case "C": return conditionDamageC;
            default: return conditionDamageD;
        }
    }

    int GetHealthDamage(string grade)
    {
        switch (grade)
        {
            case "S": return healthDamageS;
            case "A": return healthDamageA;
            case "B": return healthDamageB;
            case "C": return healthDamageC;
            default: return healthDamageD;
        }
    }

    AudioClip GetResultSFX(string grade)
    {
        switch (grade)
        {
            case "S": return resultSSFX;
            case "A": return resultASFX;
            case "B": return resultBSFX;
            case "C": return resultCSFX;
            default: return resultDSFX;
        }
    }
}
