using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    private void Awake()
    {
        if(Instance == null) Instance = this;
    }

    [SerializeField] public AudioSource audioSource;
    [SerializeField] public AudioClip startDrivingAudioClip;
    [SerializeField] public AudioClip endDrivingAudioClip;
    [SerializeField] public AudioClip rearrangementAudioClip;

    public void PlayStartDrivingAudio()
    {
        audioSource.clip = startDrivingAudioClip;
        audioSource.Play();
    }
    public void PlayEndDrivingAudio()
    {
        audioSource.clip = endDrivingAudioClip;
        audioSource.Play();
    }
    public void PlayRearrangementAudio()
    {
        audioSource.clip = rearrangementAudioClip;
        audioSource.Play();
    }
}
