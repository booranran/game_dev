using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class StartPanelController : MonoBehaviour
{
    [Header("전체 패널 (게임 시작되면 통째로 꺼짐)")]
    public GameObject startPanelRoot;

    [Header("최상위 암전 (라인선택→Intro, Intro2→게임 전환에 재사용)")]
    public Image fadeOverlay;
    public float fadeInDuration = 1f;
    public float fadeOutDuration = 1f;

    [Header("1. 캐릭터 선택")]
    public GameObject selectCharacterGroup;

    [Header("2. 라인 선택")]
    public GameObject selectLineGroup;

    [Header("3. Intro - 플레이어 등장(IntroImage+character는 통째로 켜짐) + 독백 + 흰색 디밍 + 투두")]
    public GameObject introGroup;
    public Image introCharacterImage; // 등장하는 캐릭터 일러스트 - 학생/직장인에 따라 교체
    public Sprite studentIntroCharacter;
    public Sprite workerIntroCharacter;
    [TextArea] public string studentIntroMonologue;
    [TextArea] public string workerIntroMonologue;
    public TextMeshProUGUI introMonologueText;
    public GameObject introMonologueBox; // monolog 오브젝트 자체 (박스 show/hide용, 텍스트는 introMonologueText가 담당)
    public Image introDimOverlay; // Intro 안의 흰색 이미지 - 장면전환용 아님, 배경 흐리게 하는 디밍용
    public float introDimAlpha = 0.6f;
    public float introDimFadeDuration = 0.5f;
    public GameObject introTodo;
    public TextMeshProUGUI introTodoText;
    [TextArea] public string studentTodoText;
    [TextArea] public string workerTodoText;
    public AudioClip todoSFX;
    [Range(0f, 1f)] public float todoBGMVolume = 0.4f; // Todo 떠있는 동안 배경음 볼륨

    [Header("4. Intro2 - 일러스트 슬라이드 (01·02·03 독백+클릭 / 04+Start 클릭)")]
    public GameObject intro2Group;
    public Image intro2Image;
    public TextMeshProUGUI intro2MonologueText;
    public GameObject intro2MonologueBox;
    public GameObject intro2Start;
    public Sprite studentIllustration01;
    public Sprite workerIllustration01;
    [TextArea] public string studentMonologue01;
    [TextArea] public string workerMonologue01;
    public Sprite studentIllustration02;
    public Sprite workerIllustration02;
    [TextArea] public string studentMonologue02;
    [TextArea] public string workerMonologue02;
    public Sprite studentIllustration03;
    public Sprite workerIllustration03;
    [TextArea] public string studentMonologue03;
    [TextArea] public string workerMonologue03;
    public Sprite illustration04; // 로고 슬라이드 - 캐릭터 구분 없이 공용
    public float startLogoDelay = 1f; // 일러스트04 뜨고 나서 Start 로고 뜨기까지 지연
    public float logoBGMFadeOutDuration = 2f; // 일러스트03부터 BGM이 점점 작아지는 시간 (로고 뜰 때쯈 조용해지도록)
    public AudioClip logoJingleSFX; // Start 로고 등장과 동시에 1회 재생되는 징글
    public AudioClip gameStartSFX; // 로고에서 continue 눌러서 본게임으로 넘어가는 순간 1회 재생 (징글과는 별개)

    [Header("클릭 안내 (TriggerEventController의 continuePrompt와 동일한 구조 - 풀스크린 투명 버튼 OnClick에 OnContinueClicked() 연결 필요)")]
    public GameObject continuePrompt;
    public float continuePromptDelay = 2f;

    private bool waitingForContinue;
    private GameManager.CharacterType selectedCharacter = GameManager.CharacterType.Student;
    private GameManager.LineType selectedLine = GameManager.LineType.Line9;

    void Start()
    {
        // GameManager의 디버그 모드로 바로 시작한 경우 - 패널은 띄울 필요 없이 그냥 꺼둠
        if (GameManager.Instance != null && GameManager.Instance.debugMode)
        {
            if (startPanelRoot) startPanelRoot.SetActive(false);
            else gameObject.SetActive(false);
            return;
        }

        if (introMonologueBox) introMonologueBox.SetActive(false);
        if (introTodo) introTodo.SetActive(false);
        if (introDimOverlay) SetImageAlpha(introDimOverlay, 0f);
        if (intro2MonologueBox) intro2MonologueBox.SetActive(false);
        if (intro2Start) intro2Start.SetActive(false);
        if (continuePrompt) continuePrompt.SetActive(false);
        if (fadeOverlay)
        {
            SetImageAlpha(fadeOverlay, 0f);
            fadeOverlay.gameObject.SetActive(false); // 안 보이는 동안엔 꺼둬야 풀스크린 레이캐스트가 버튼들을 안 막음
        }

        ShowOnly(selectCharacterGroup);
    }

    public void SelectStudent() => SelectCharacter(GameManager.CharacterType.Student);
    public void SelectWorker() => SelectCharacter(GameManager.CharacterType.Worker);
    public void SelectLine7() => SelectLine(GameManager.LineType.Line7);
    public void SelectLine9() => SelectLine(GameManager.LineType.Line9);

    void SelectCharacter(GameManager.CharacterType type)
    {
        selectedCharacter = type;
        ShowOnly(selectLineGroup);
    }

    void SelectLine(GameManager.LineType type)
    {
        selectedLine = type;
        StartCoroutine(PlayIntroSequence());
    }

    bool IsWorker => selectedCharacter == GameManager.CharacterType.Worker;

    IEnumerator PlayIntroSequence()
    {
        // 1. 암전
        if (fadeOverlay) fadeOverlay.gameObject.SetActive(true);
        yield return FadeImage(fadeOverlay, 0f, 1f, fadeInDuration);

        // 2. Intro 노출 - 플레이어 등장 + 독백
        ShowOnly(introGroup);
        if (introCharacterImage) introCharacterImage.sprite = IsWorker ? workerIntroCharacter : studentIntroCharacter;
        if (introMonologueText) introMonologueText.text = IsWorker ? workerIntroMonologue : studentIntroMonologue;
        if (introMonologueBox) introMonologueBox.SetActive(true);
        yield return FadeImage(fadeOverlay, 1f, 0f, fadeOutDuration);
        if (fadeOverlay) fadeOverlay.gameObject.SetActive(false); // 다 보이고 나면 꺼서 이후 클릭들이 안 막히게

        yield return WaitForContinue();
        if (introMonologueBox) introMonologueBox.SetActive(false);

        // 3. 흰색 디밍 + 투두 UI
        yield return FadeImage(introDimOverlay, 0f, introDimAlpha, introDimFadeDuration);
        if (introTodo) introTodo.SetActive(true);
        if (introTodoText) introTodoText.text = IsWorker ? workerTodoText : studentTodoText;
        AudioManager.Instance?.SetBGMVolume(todoBGMVolume);
        AudioManager.Instance?.PlaySFX(todoSFX);
        yield return WaitForContinue();
        if (introTodo) introTodo.SetActive(false);
        AudioManager.Instance?.SetBGMVolume(1f);

        // 4. Intro2 - 일러스트 슬라이드
        ShowOnly(intro2Group);

        if (intro2Image) intro2Image.sprite = IsWorker ? workerIllustration01 : studentIllustration01;
        if (intro2MonologueText) intro2MonologueText.text = IsWorker ? workerMonologue01 : studentMonologue01;
        if (intro2MonologueBox) intro2MonologueBox.SetActive(true);
        yield return WaitForContinue();
        if (intro2MonologueBox) intro2MonologueBox.SetActive(false);

        if (intro2Image) intro2Image.sprite = IsWorker ? workerIllustration02 : studentIllustration02;
        if (intro2MonologueText) intro2MonologueText.text = IsWorker ? workerMonologue02 : studentMonologue02;
        if (intro2MonologueBox) intro2MonologueBox.SetActive(true);
        yield return WaitForContinue();
        if (intro2MonologueBox) intro2MonologueBox.SetActive(false);

        if (intro2Image) intro2Image.sprite = IsWorker ? workerIllustration03 : studentIllustration03;
        if (intro2MonologueText) intro2MonologueText.text = IsWorker ? workerMonologue03 : studentMonologue03;
        if (intro2MonologueBox) intro2MonologueBox.SetActive(true);
        yield return WaitForContinue();
        if (intro2MonologueBox) intro2MonologueBox.SetActive(false);

        if (intro2Image) intro2Image.sprite = illustration04;
        AudioManager.Instance?.FadeOutBGM(logoBGMFadeOutDuration); // 04로 넘어온 뒤부터 BGM이 점점 작아짐
        yield return new WaitForSeconds(startLogoDelay);
        if (intro2Start) intro2Start.SetActive(true);
        AudioManager.Instance?.PlaySFX(logoJingleSFX); // 로고 등장과 동시에 징글 1회 재생
        yield return WaitForContinue();
        AudioManager.Instance?.PlaySFX(gameStartSFX); // 이 시점의 continue만 효과음 - 공용 버튼이라 다른 단계 클릭엔 안 울리게 코드로 직접 처리

        // 5. 최종 암전 → 암전 상태에서 게임 초기화 → 게임 노출
        if (fadeOverlay) fadeOverlay.gameObject.SetActive(true); // 다시 켜서 페이드 보이게
        yield return FadeImage(fadeOverlay, 0f, 1f, fadeInDuration);

        GameManager.Instance.BeginGame(selectedCharacter, selectedLine); // 인트로 다 끝난 지금 시점에야 실제로 게임 시작

        ShowOnly(null);
        yield return FadeImage(fadeOverlay, 1f, 0f, fadeOutDuration);

        if (startPanelRoot) startPanelRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    void ShowOnly(GameObject group)
    {
        if (selectCharacterGroup) selectCharacterGroup.SetActive(group == selectCharacterGroup);
        if (selectLineGroup) selectLineGroup.SetActive(group == selectLineGroup);
        if (introGroup) introGroup.SetActive(group == introGroup);
        if (intro2Group) intro2Group.SetActive(group == intro2Group);
    }

    // 풀스크린 버튼의 OnClick에서 호출 - TriggerEventController/ResultPanelController와 동일한 역할
    public void OnContinueClicked()
    {
        if (!waitingForContinue) return;
        waitingForContinue = false;
    }

    IEnumerator WaitForContinue()
    {
        waitingForContinue = true;
        if (continuePrompt) continuePrompt.SetActive(false);
        yield return new WaitForSeconds(continuePromptDelay);
        if (continuePrompt) continuePrompt.SetActive(true);
        yield return new WaitUntil(() => !waitingForContinue);
        if (continuePrompt) continuePrompt.SetActive(false);
    }

    void SetImageAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (!img) yield break;
        SetImageAlpha(img, from);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetImageAlpha(img, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetImageAlpha(img, to);
    }
}
