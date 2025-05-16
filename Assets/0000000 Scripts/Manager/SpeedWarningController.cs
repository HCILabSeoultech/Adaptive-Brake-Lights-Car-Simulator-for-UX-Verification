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
    [Tooltip("Audio clip to play when speed is too low.")]
    public AudioClip warningClip;

    [Header("Speed Source")]
    [Tooltip("Reference to your DistanceTracker or other provider of current speed.")]
    public DistanceTracker distanceTracker;

    private AudioSource audioSource;
    private bool isPlayingWarning = false;
    private float timer = 0f;

    public Image velocityWarningImage;
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;        // Clip should play once per trigger
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 초기 지연 시간 내에는 체크하지 않음
        if (timer < startDelay || distanceTracker == null)
            return;

        float currentSpeed = distanceTracker.speedKmh;

        if (currentSpeed < speedThreshold)
        {
            velocityWarningImage.gameObject.SetActive(true);
        }
        else
        {
            velocityWarningImage.gameObject.SetActive(false);
        }
        
        // 1) 재생 중이 아니고, 속도가 임계치 아래로 떨어졌다면 재생 시작
        if (!isPlayingWarning && currentSpeed < speedThreshold)
        {
            audioSource.PlayOneShot(warningClip);
            isPlayingWarning = true;
        }

        // 2) 재생 중이었고, 이제 클립이 끝나서 isPlaying이 false라면
        if (isPlayingWarning && !audioSource.isPlaying)
        {
            isPlayingWarning = false;

            // 종료 시점에 여전히 임계치 아래라면 즉시 재생
            if (currentSpeed < speedThreshold)
            {
                audioSource.PlayOneShot(warningClip);
                isPlayingWarning = true;
            }
        }
    }
}