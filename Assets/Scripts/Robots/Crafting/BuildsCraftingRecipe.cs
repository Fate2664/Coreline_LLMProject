using UnityEngine;

namespace Coreline.Robots
{
    public enum CraftableBuildsType
    {
        Chest,
        Furnace
    }

    [CreateAssetMenu(menuName = "Builds/Crafting/Build Crafting Recipe")]
    public sealed class BuildsCraftingRecipe : CraftingRecipe
    {
        [SerializeField] private CraftableBuildsType buildsType;
        [UnityEngine.Serialization.FormerlySerializedAs("robotPrefab")]
        [SerializeField] private GameObject buildPrefab;

        public CraftableBuildsType BuildsType => buildsType;
        public GameObject BuildPrefab => buildPrefab;
    }
}
