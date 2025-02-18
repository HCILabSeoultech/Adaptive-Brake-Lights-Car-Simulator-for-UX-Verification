using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ILightBehavior
{
    IEnumerator ApplyLighting(MeshRenderer mainBrakeRenderer, List<MeshRenderer> subBrakeRenderers, float intensity);
}