using UnityEngine;

namespace com.unity.testtrack.terrainsystem.attributes
{
    //Adapted from https://answers.unity.com/questions/1573537/how-to-change-the-names-of-a-vector-3-that-is-set.html
    public class CustomVectorLabelsAttribute : PropertyAttribute
    {
        public readonly string[] labels;
        public readonly string rootLabel;

        public CustomVectorLabelsAttribute(string rootLabel, params string[] labels)
        {
            this.labels = labels;
            this.rootLabel = rootLabel;
        }
    }
}