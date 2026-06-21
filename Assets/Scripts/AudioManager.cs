using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public enum FadeStyle { Crossfade, FadeOutThenIn, Cut }

    [Header("BGM 소스 2개 (Crossfade일 때만 둘 다 동시에 사용됨)")]
    public AudioSource bgmSourceA;
    public AudioSource bgmSourceB;

    [Header("페이드 길이")]
    public float crossfadeDuration = 1.5f; // Crossfade - 겹치는 구간 길이
    public float fadeOutDuration = 1f;     // FadeOutThenIn - 줄어드는 구간 길이
    public float fadeInDuration = 1f;      // FadeOutThenIn - 늘어나는 구간 길이

    [Header("시작화면 ↔ 본게임 BGM")]
    public AudioClip startBGM;
    public AudioClip mainGameBGM;
    public FadeStyle mainGameFadeStyle = FadeStyle.Crossfade;

    [Header("SFX (버튼 클릭 등 - 겹쳐 재생되는 1회성 효과음, BGM과 별도)")]
    public AudioSource sfxSource;

    private AudioSource activeSource;
    private AudioSource inactiveSource;
    private Coroutine fadeRoutine;
    private AudioClip currentClip; // 페이드 완료 전에도 "지금 재생 요청된 클립"을 즉시 반영 (가드/claim 체크용)

    // PlayBGM이 호출될 때마다(가드 통과 여부 상관없이) 올라감 - PanelBGMPlayer가 "내가 꺼진 뒤에 누가 BGM을 새로 요청했는지" 확인하는 용도
    public int RequestVersion { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        activeSource = bgmSourceA;
        inactiveSource = bgmSourceB;
    }

    void OnEnable()
    {
        GameManager.OnGameInitialized += PlayMainGameBGM;
    }

    void OnDisable()
    {
        GameManager.OnGameInitialized -= PlayMainGameBGM;
    }

    void Start()
    {
        PlayBGM(startBGM, mainGameFadeStyle);
    }

    public void PlayMainGameBGM() => PlayBGM(mainGameBGM, mainGameFadeStyle);

    // 패널이 꺼질 때 호출 - 한 프레임 기다려서 그 사이 아무도 PlayBGM/FadeOutBGM을 새로 안 불렀으면(=다른 패널이 안 가져갔으면)만 본게임 브금으로 복귀
    // (AudioManager는 항상 켜져있는 오브젝트라 여기서 코루틴을 돌려야 안전 - 꺼지는 패널 쪽에서 직접 코루틴 돌리면 한 프레임도 못 기다리고 같이 멈춰버림)
    public void RequestReturnToMainGameIfUnclaimed()
    {
        StartCoroutine(DoReturnIfUnclaimed());
    }

    IEnumerator DoReturnIfUnclaimed()
    {
        int versionAtCall = RequestVersion;
        yield return null;
        if (RequestVersion == versionAtCall)
            PlayMainGameBGM();
    }

    // 패널/전환마다 원하는 페이드 방식을 직접 골라서 호출하면 됨
    public void PlayBGM(AudioClip clip, FadeStyle style)
    {
        RequestVersion++; // 가드를 통과하든 말든 "요청이 들어왔다"는 사실 자체는 항상 기록
        if (clip == null || currentClip == clip) return;
        currentClip = clip;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (style == FadeStyle.Cut)
        {
            activeSource.Stop();
            activeSource.clip = clip;
            activeSource.loop = true;
            activeSource.volume = 1f;
            activeSource.Play();
            return;
        }

        fadeRoutine = StartCoroutine(style == FadeStyle.Crossfade ? DoCrossfade(clip) : DoFadeOutThenIn(clip));
    }

    // 두 BGM이 겹치는 구간 없이 항상 Crossfade로만 쓰고 싶을 때 쓰는 짧은 버전
    public void CrossfadeTo(AudioClip clip) => PlayBGM(clip, FadeStyle.Crossfade);

    // 버튼 클릭음 등 - PlayOneShot이라 여러 번 빠르게 눌러도 서로 안 끊고 겹쳐서 재생됨
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // 결과 효과음 등이 재생되는 동안 BGM을 잠깐 안 들리게 - 크로스페이드 중이면 둘 다 음소거 (볼륨 값은 안 건드려서 진행 중인 페이드에 영향 없음)
    public void DuckBGM(bool duck)
    {
        if (bgmSourceA != null) bgmSourceA.mute = duck;
        if (bgmSourceB != null) bgmSourceB.mute = duck;
    }

    // DuckBGM은 완전 무음이라 다름 - Todo처럼 "완전히 안 들리는 게 아니라 살짝만 줄이고 싶을 때" 직접 볼륨 지정
    // (진행 중인 페이드가 없을 때 쓰는 용도 - 페이드 코루틴이 매 프레임 볼륨을 덮어쓰는 중이면 같이 쓰지 말 것)
    public void SetBGMVolume(float volume)
    {
        if (bgmSourceA != null) bgmSourceA.volume = volume;
        if (bgmSourceB != null) bgmSourceB.volume = volume;
    }

    // 다른 클립으로 안 바꾸고 그냥 지금 BGM만 조용히 - 로고 등장처럼 "음악이 잦아드는" 연출용
    public void FadeOutBGM(float duration)
    {
        RequestVersion++;
        currentClip = null; // 다음에 어떤 클립이 와도(같은 클립이어도) 새로 재생되게
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(DoFadeOut(duration));
    }

    IEnumerator DoFadeOut(float duration)
    {
        float fromVolume = activeSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            activeSource.volume = Mathf.Lerp(fromVolume, 0f, t / duration);
            yield return null;
        }
        activeSource.Stop();
        // volume을 다시 1로 되돌리지 않음 - 지금 진짜로 무음 상태인 게 맞고, 다음 크로스페이드/페이드인은
        // 어차피 자기 쪽에서 0부터 다시 올리니 여기서 1로 리셋해두면 오히려 다음 페이드 계산이 잘못된 시작값을 읽게 됨
    }

    IEnumerator DoCrossfade(AudioClip clip)
    {
        inactiveSource.clip = clip;
        inactiveSource.loop = true;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        float fromVolume = activeSource.volume;
        float t = 0f;
        while (t < crossfadeDuration)
        {
            t += Time.deltaTime;
            float ratio = t / crossfadeDuration;
            activeSource.volume = Mathf.Lerp(fromVolume, 0f, ratio);
            inactiveSource.volume = Mathf.Lerp(0f, 1f, ratio);
            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 1f; // 다음 차례에 다시 쓸 때를 위해 원복

        var temp = activeSource;
        activeSource = inactiveSource;
        inactiveSource = temp;
    }

    // 겹치는 구간 없이 - 기존 BGM을 0까지 줄이고 끊은 다음, 클립을 바꿔서 다시 0부터 키움
    IEnumerator DoFadeOutThenIn(AudioClip clip)
    {
        float fromVolume = activeSource.volume;
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            activeSource.volume = Mathf.Lerp(fromVolume, 0f, t / fadeOutDuration);
            yield return null;
        }
        activeSource.Stop();

        activeSource.clip = clip;
        activeSource.loop = true;
        activeSource.Play();

        t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.deltaTime;
            activeSource.volume = Mathf.Lerp(0f, 1f, t / fadeInDuration);
            yield return null;
        }
    }
}
