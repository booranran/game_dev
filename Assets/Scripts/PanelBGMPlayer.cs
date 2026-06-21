using UnityEngine;

// 패널 GameObject에 붙이기만 하면 됨 - SetActive(true)로 켜질 때(OnEnable) 자동으로 그 BGM으로 전환
public class PanelBGMPlayer : MonoBehaviour
{
    public AudioClip bgm;
    public AudioManager.FadeStyle fadeStyle = AudioManager.FadeStyle.Crossfade;

    void OnEnable()
    {
        if (bgm != null)
            AudioManager.Instance?.PlayBGM(bgm, fadeStyle);
    }

    // 패널이 꺼지면 본게임 브금으로 복귀 - 곧바로 다른 패널이 켜져서 그 BGM으로 또 바뀌어도
    // 같은 프레임 안에서 코루틴이 교체되는 거라 들리는 끊김 없음
    void OnDisable()
    {
        if (bgm != null)
            AudioManager.Instance?.PlayMainGameBGM();
    }
}
