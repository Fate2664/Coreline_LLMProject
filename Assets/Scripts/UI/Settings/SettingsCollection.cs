using System.Collections.Generic;
using NovaSamples.SettingsMenu;
using UnityEngine;

[CreateAssetMenu(menuName = "Settings/Collection")]
public class SettingsCollection : ScriptableObject
{
    public string Category = null;
    
    [SerializeReference]
    [TypeSelector]
    public List<Setting> Settings = new List<Setting>();
}
