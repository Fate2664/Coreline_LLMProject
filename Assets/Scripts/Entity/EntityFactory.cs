using UnityEngine;

namespace Coreline
{
    //This script implements the factory pattern with the interface
    public class EntityFactory<T> : IEntityFactory<T> where T : Entity
    {
        private EntityData[] data;

        public EntityFactory(EntityData[] data)
        {
            this.data = data;
        }
        
        //Creating an entity
        public T Create(Transform spawnPoint)
        {
            //Choose on of the entity datas
            EntityData entityData = data[Random.Range(0, data.Length)];
            GameObject instance = GameObject.Instantiate(entityData.prefab, spawnPoint.position, spawnPoint.rotation);
            return instance.GetComponent<T>();
        }
    }
}