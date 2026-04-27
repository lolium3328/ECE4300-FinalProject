using UnityEngine;
using System.Collections;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [Header("BGM Clips")]
    [Tooltip("低分播放失败音乐：辉夜大小姐想让我告白もうあるやつ")]
    public AudioClip lowScoreFailBGM;

    [Tooltip("收尾bgm33._ヤチヨ絵巻")]
    public AudioClip endingBGM;

    [Tooltip("游戏菜单bgm BanG Dream！ - デイタイム♪_H")]
    public AudioClip menuBGM;

    [Tooltip("结算的小曲30._IROHA'S_Dancing_All_Night")]
    public AudioClip settlementBGM;

    [Tooltip("轻快的bgm06._ガールズ☆パーティー")]
    public AudioClip upbeatBGM1;

    [Tooltip("轻快的bgm其二22._ヤチヨカップ優勝!")]
    public AudioClip upbeatBGM2;

    [Tooltip("轻柔的bgm14._たいせつなひと")]
    public AudioClip softBGM1;

    [Tooltip("轻柔的bgm其二25._かぐやと彩葉")]
    public AudioClip softBGM2;

    [Tooltip("高分结算bgm12._天才彩葉の即興ジングル")]
    public AudioClip highScoreBGM1;

    [Tooltip("高分结算bgm其二13._私の好きだったもの")]
    public AudioClip highScoreBGM2;

    private AudioSource audioSource1;
    private AudioSource audioSource2;
    private bool isPlayingSource1 = true;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            audioSource1 = gameObject.AddComponent<AudioSource>();
            audioSource2 = gameObject.AddComponent<AudioSource>();
            audioSource1.loop = true;
            audioSource2.loop = true;
            audioSource1.volume = 1f;
            audioSource2.volume = 0f;
            audioSource1.playOnAwake = false;
            audioSource2.playOnAwake = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 播放: 低分播放失败音乐：辉夜大小姐想让我告白もうあるやつ
    /// </summary>
    public void PlayLowScoreFailBGM() => PlayBGM(lowScoreFailBGM);

    /// <summary>
    /// 播放: 收尾bgm33._ヤチヨ絵巻
    /// </summary>
    public void PlayEndingBGM() => PlayBGM(endingBGM);

    /// <summary>
    /// 播放: 游戏菜单bgm BanG Dream！ - デイタイム♪_H
    /// </summary>
    public void PlayMenuBGM() => PlayBGM(menuBGM);

    /// <summary>
    /// 播放: 结算的小曲30._IROHA'S_Dancing_All_Night
    /// </summary>
    public void PlaySettlementBGM() => PlayBGM(settlementBGM);

    /// <summary>
    /// 播放: 轻快的bgm06._ガールズ☆パーティー
    /// </summary>
    public void PlayUpbeatBGM1() => PlayBGM(upbeatBGM1);

    /// <summary>
    /// 播放: 轻快的bgm其二22._ヤチヨカップ優勝!
    /// </summary>
    public void PlayUpbeatBGM2() => PlayBGM(upbeatBGM2);

    /// <summary>
    /// 播放: 轻柔的bgm14._たいせつなひと
    /// </summary>
    public void PlaySoftBGM1() => PlayBGM(softBGM1);

    /// <summary>
    /// 播放: 轻柔的bgm其二25._かぐやと彩葉
    /// </summary>
    public void PlaySoftBGM2() => PlayBGM(softBGM2);

    /// <summary>
    /// 播放: 高分结算bgm12._天才彩葉の即興ジングル
    /// </summary>
    public void PlayHighScoreBGM1() => PlayBGM(highScoreBGM1);

    /// <summary>
    /// 播放: 高分结算bgm其二13._私の好きだったもの
    /// </summary>
    public void PlayHighScoreBGM2() => PlayBGM(highScoreBGM2);

    private void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        AudioSource activeSource = isPlayingSource1 ? audioSource1 : audioSource2;
        if (activeSource.clip == clip && activeSource.isPlaying) return;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(Crossfade(clip));
    }

    private IEnumerator Crossfade(AudioClip nextClip)
    {
        AudioSource activeSource = isPlayingSource1 ? audioSource1 : audioSource2;
        AudioSource nextSource = isPlayingSource1 ? audioSource2 : audioSource1;

        nextSource.clip = nextClip;
        nextSource.volume = 0f;
        nextSource.Play();

        float timer = 0f;
        float fadeDuration = 1f;

        float startVolume = activeSource.volume;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;
            activeSource.volume = Mathf.Lerp(startVolume, 0f, t);
            nextSource.volume = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        activeSource.volume = 0f;
        activeSource.Stop();
        nextSource.volume = 1f;

        isPlayingSource1 = !isPlayingSource1;
        fadeCoroutine = null;
    }
}
