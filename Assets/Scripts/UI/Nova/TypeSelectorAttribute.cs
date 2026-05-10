// Assets/Scripts/SettingsMenu/TypeSelectorAttribute.cs
using System;
using UnityEngine;

namespace NovaSamples.SettingsMenu
{
    [AttributeUsage(AttributeTargets.Field)]
    public class TypeSelectorAttribute : PropertyAttribute { }
}
