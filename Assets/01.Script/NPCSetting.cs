using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NPCSetting", menuName = "Scriptable Objects/NPCSetting")]
public class NPCSetting : ScriptableObject
{
    [TextArea(3, 50)] 
    public string Description;
}
