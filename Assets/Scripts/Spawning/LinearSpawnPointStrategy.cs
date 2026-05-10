using UnityEngine;

namespace Coreline
{
    //This is the linear spawn point strategy. It spawns each entity one after the other
    public class LinearSpawnPointStrategy : ISpawnPointStrategy
    {
        private int index = 0;
        private Transform[] spawnPoints;

        public LinearSpawnPointStrategy(Transform[] spawnPoints)
        {
            this.spawnPoints = spawnPoints;
        }
        
        public Transform NextSpawnPoint()
        {
            Transform result = spawnPoints[index];
            index = (index + 1) % spawnPoints.Length;
            return result;
        }
    }
}