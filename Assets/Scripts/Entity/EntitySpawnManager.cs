using UnityEngine;

namespace Coreline
{
    //This is a base class for spawning the entities. All entity spawn managers will inherit from this
    public abstract class EntitySpawnManager : MonoBehaviour
    {
        //Spawn strategy enum
        protected enum SpawnPointStrategyType
        {
            Linear,
            Random
        }
        
        [SerializeField] protected SpawnPointStrategyType spawnPointStrategyType = SpawnPointStrategyType.Linear;   //Set default strategy to linear
        [SerializeField] protected Transform[] spawnPoints;

        protected ISpawnPointStrategy spawnPointStrategy;


        protected virtual void Awake()
        {
            //Set spawn point strategy based on selection in the editor
            spawnPointStrategy = spawnPointStrategyType switch
            {
                SpawnPointStrategyType.Linear => new LinearSpawnPointStrategy(spawnPoints),
                SpawnPointStrategyType.Random => new RandomSpawnPointStrategy(spawnPoints),
                _ => spawnPointStrategy
            };
        }
        
        //Abstract spawn method
        public abstract void Spawn();
    }
}