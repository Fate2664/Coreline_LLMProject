using System;
using System.Collections.Generic;
using Coreline;
using UnityEngine;

namespace Coreline.Robots
{
    public enum CraftableRobotType
    {
        Mining,
        Collecting,
        Scanning
    }

    [Serializable]
    public class RobotCraftingRequirement
    {
        public OreType oreType;
        [Min(1)] public int amount = 1;
    }

    [CreateAssetMenu(menuName = "Robots/Crafting/Robot Crafting Recipe")]
    public class RobotCraftingRecipe : ScriptableObject
    {
        [SerializeField] private CraftableRobotType robotType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject robotPrefab;
        [SerializeField] private List<RobotCraftingRequirement> requirements = new();

        public CraftableRobotType RobotType => robotType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject RobotPrefab => robotPrefab;
        public IReadOnlyList<RobotCraftingRequirement> Requirements => requirements;
    }
}
