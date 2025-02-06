using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CrossWalkManager_2 : MonoBehaviour
{
    // Start is called before the first frame update
    public Animator[] phase1Animators;
    public Animator[] phase2Animators;

    public void TriggerPhase1Animation()
    {
        for (int i = 0; i < phase1Animators.Length; i++)
        {
            phase1Animators[i].SetTrigger("Cross");
        }
    }

    public void TriggerPhase2Animation()
    {
        for (int i = 0; i < phase2Animators.Length; i++)
        {
            phase2Animators[i].SetTrigger("Cross");
        }
    }
}
