using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Coreline
{
    public class OreRockSpawnManager : MonoBehaviour
    {
        [Serializable]
        private class OreRockSpawnGroup
        {
            public OreType oreType;
            public GameObject rockPrefab;
            public Transform[] spawnPoints;
            [Min(-1f)] public float respawnDelayOverride = -1f;

            public float GetRespawnDelay(float defaultRespawnDelay)
            {
                return respawnDelayOverride >= 0f ? respawnDelayOverride : defaultRespawnDelay;
            }
        }

        [SerializeField] private OreRockSpawnGroup[] oreRockSpawns;
        [SerializeField, Min(0f)] private float defaultRespawnDelay = 200f;
        [SerializeField] private Transform spawnedRockParent;

        private readonly Dictionary<Transform, GameObject> activeRocksBySpawnPoint = new();
        private readonly Dictionary<Transform, Coroutine> respawnRoutinesBySpawnPoint = new();
        private readonly Dictionary<GameObject, MineableRock> mineablesByRock = new();
        private readonly Dictionary<GameObject, UnityAction> depletionHandlersByRock = new();

        private void Start()
        {
            SpawnStartGroups();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<GameObject, UnityAction> pair in depletionHandlersByRock)
            {
                if (pair.Key == null || !mineablesByRock.TryGetValue(pair.Key, out MineableRock mineable) || mineable == null)
                {
                    continue;
                }

                mineable.OnDepleted -= pair.Value;
            }

            depletionHandlersByRock.Clear();
            mineablesByRock.Clear();
        }

        public void Spawn()
        {
            foreach (OreRockSpawnGroup spawnGroup in oreRockSpawns)
            {
                SpawnGroup(spawnGroup);
            }
        }

        private void SpawnStartGroups()
        {
            foreach (OreRockSpawnGroup spawnGroup in oreRockSpawns)
            {
                SpawnGroup(spawnGroup);
            }
        }

        public void Spawn(OreType oreType)
        {
            foreach (OreRockSpawnGroup spawnGroup in oreRockSpawns)
            {
                if (spawnGroup != null && spawnGroup.oreType == oreType)
                {
                    SpawnGroup(spawnGroup);
                }
            }
        }

        private void SpawnGroup(OreRockSpawnGroup spawnGroup)
        {
            if (spawnGroup.rockPrefab == null || spawnGroup.spawnPoints == null)
            {
                return;
            }

            foreach (Transform spawnPoint in spawnGroup.spawnPoints)
            {
                SpawnAtPoint(spawnGroup, spawnPoint);
            }
        }

        private bool SpawnAtPoint(OreRockSpawnGroup spawnGroup, Transform spawnPoint)
        {
            if (spawnPoint == null || HasActiveRock(spawnPoint))
            {
                return false;
            }

            CancelRespawn(spawnPoint);

            Transform parent = spawnedRockParent != null ? spawnedRockParent : transform;
            GameObject rock = Instantiate(spawnGroup.rockPrefab, spawnPoint.position, spawnPoint.rotation, parent);
            rock.name = $"{spawnGroup.oreType}Rock";

            activeRocksBySpawnPoint[spawnPoint] = rock;
            RegisterDepletionHandler(spawnGroup, spawnPoint, rock);
            return true;
        }

        private bool HasActiveRock(Transform spawnPoint)
        {
            if (!activeRocksBySpawnPoint.TryGetValue(spawnPoint, out GameObject activeRock))
            {
                return false;
            }

            if (activeRock != null)
            {
                return true;
            }

            activeRocksBySpawnPoint.Remove(spawnPoint);
            return false;
        }

        private void RegisterDepletionHandler(OreRockSpawnGroup spawnGroup, Transform spawnPoint, GameObject rock)
        {
            MineableRock mineable = rock.GetComponentInChildren<MineableRock>();
            if (mineable == null)
            {
                return;
            }

            UnityAction depletionHandler = () => HandleRockDepleted(spawnGroup, spawnPoint, rock);
            mineable.OnDepleted += depletionHandler;

            mineablesByRock[rock] = mineable;
            depletionHandlersByRock[rock] = depletionHandler;
        }

        private void HandleRockDepleted(OreRockSpawnGroup spawnGroup, Transform spawnPoint, GameObject rock)
        {
            UnregisterDepletionHandler(rock);

            if (activeRocksBySpawnPoint.TryGetValue(spawnPoint, out GameObject activeRock) && activeRock == rock)
            {
                activeRocksBySpawnPoint.Remove(spawnPoint);
            }

            CancelRespawn(spawnPoint);
            respawnRoutinesBySpawnPoint[spawnPoint] = StartCoroutine(RespawnAfterDelay(spawnGroup, spawnPoint));
        }

        private IEnumerator RespawnAfterDelay(OreRockSpawnGroup spawnGroup, Transform spawnPoint)
        {
            float delay = Mathf.Max(0f, spawnGroup.GetRespawnDelay(defaultRespawnDelay));
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            respawnRoutinesBySpawnPoint.Remove(spawnPoint);
            SpawnAtPoint(spawnGroup, spawnPoint);
        }

        private void CancelRespawn(Transform spawnPoint)
        {
            if (!respawnRoutinesBySpawnPoint.TryGetValue(spawnPoint, out Coroutine routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            respawnRoutinesBySpawnPoint.Remove(spawnPoint);
        }

        private void UnregisterDepletionHandler(GameObject rock)
        {
            if (rock == null ||
                !depletionHandlersByRock.TryGetValue(rock, out UnityAction handler) ||
                !mineablesByRock.TryGetValue(rock, out MineableRock mineable) ||
                mineable == null)
            {
                return;
            }

            mineable.OnDepleted -= handler;
            depletionHandlersByRock.Remove(rock);
            mineablesByRock.Remove(rock);
        }
    }
}
