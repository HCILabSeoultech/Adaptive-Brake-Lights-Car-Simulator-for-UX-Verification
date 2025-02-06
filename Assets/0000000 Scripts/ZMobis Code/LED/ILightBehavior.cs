using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ILightBehavior
{
    IEnumerator ApplyLighting(List<GameObject> leds, float intensity);
}