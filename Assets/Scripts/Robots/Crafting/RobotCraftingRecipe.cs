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

    [CreateAssetMenu(menuName = "Robots/Crafting/Robot Crafting Recipe")]
    public sealed class RobotCraftingRecipe : CraftingRecipe
    {
        [SerializeField] private CraftableRobotType robotType;
        [SerializeField] private GameObject robotPrefab;

        public CraftableRobotType RobotType => robotType;
        public GameObject RobotPrefab => robotPrefab;
    }
}
