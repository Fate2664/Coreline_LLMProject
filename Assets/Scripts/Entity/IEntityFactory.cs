using UnityEngine;

namespace Coreline
{
    //This generic interface will define a contract for all scripts that want to spawn an entity
    public interface IEntityFactory<T> where T : Entity
    {
        T Create(Transform spawnPoint);
    }
}
