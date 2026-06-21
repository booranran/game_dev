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

    // 패널/전환마다 원하는 페이드 방식을 직접 골라서 호출하면 됨
    public void PlayBGM(AudioClip clip, FadeStyle style)
    {
        if (clip == null || activeSource.clip == clip) return;

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
