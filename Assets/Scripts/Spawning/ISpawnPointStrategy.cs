using UnityEngine;

namespace Coreline
{
    //This interface is a contract for all spawn point strategies to inherit from
    public interface ISpawnPointStrategy
    {
        Transform NextSpawnPoint();
    }
}