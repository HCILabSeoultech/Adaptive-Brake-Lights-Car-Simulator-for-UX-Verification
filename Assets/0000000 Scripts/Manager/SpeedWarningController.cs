using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class SpeedWarningController : MonoBehaviour
{
    [Header("Speed Warning Settings")]
    [Tooltip("Speed threshold in km/h below which the warning plays.")]
    public float speedThreshold = 80f;
    [Tooltip("Delay after Start() before warnings can begin (seconds).")]
    public float startDelay = 10f;
    [Tooltip("초과 시 연속으로 아래에 머물러야 하는 시간 (seconds).")]
    public float belowDuration = 10f;
    [Tooltip("Audio clip to play when speed is too low.")]
    public AudioClip warningClip;

    [Header("Speed Source")]
    [Tooltip("Reference to your DistanceTracker or other provider of current speed.")]
    public DistanceTracker distanceTracker;

    [Header("UI Elements")]
    public Image velocityWarningImage;

    private AudioSource audioSource;
    private bool isPlayingWarning = false;
    private float timer = 0f;
    private float belowTimer = 0f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false; // 한 번만 재생
    }

    void Update()
    {
        // 전체 경과 시간
        timer += Time.deltaTime;

        // startDelay 지난 후에만 체크
        if (timer < startDelay || distanceTracker == null)
            return;

        float currentSpeed = distanceTracker.speedKmh;

        // UI 경고 표시 (즉시)
        velocityWarningImage.gameObject.SetActive(currentSpeed < speedThreshold);

        // threshold 아래로 머문 시간 누적 / 리셋
        if (currentSpeed < speedThreshold)
            belowTimer += Time.deltaTime;
        else
            belowTimer = 0f;

        // 1) 재생 중이 아니고, 연속 belowDuration 동안 속도가 threshold 아래라면 재생 시작
        if (!isPlayingWarning && belowTimer >= belowDuration)
        {
            audioSource.PlayOneShot(warningClip);
            isPlayingWarning = true;
        }

        // 2) 재생 중이었고, 클립이 끝나서 isPlaying이 false라면
        if (isPlayingWarning && !audioSource.isPlaying)
        {
            isPlayingWarning = false;

            // 재생 종료 시점에 여전히 threshold 아래에 머물러 있으면
            // belowTimer 는 이미 연속 시간 계산 중이므로 바로 다시 재생
            if (belowTimer >= belowDuration)
            {
                audioSource.PlayOneShot(warningClip);
                isPlayingWarning = true;
            }
        }
    }
}
