using UnityEngine;

namespace Coreline
{
    //This script spawns whatever T is according to the spawn strategy
    public class EntitySpawner<T> where T : Entity
    {
        private IEntityFactory<T> entityFactory;
        private ISpawnPointStrategy spawnPointStrategy;

        public EntitySpawner(IEntityFactory<T> entityFactory, ISpawnPointStrategy spawnPointStrategy)
        {
            this.entityFactory = entityFactory;
            this.spawnPointStrategy = spawnPointStrategy;
        }

        public T Spawn(out Transform spawnPoint)    //Get a reference to the next spawn point (this is for spawning enemies at different locations)
        {
            spawnPoint = spawnPointStrategy.NextSpawnPoint();
            return entityFactory.Create(spawnPoint);
        }
    }
}