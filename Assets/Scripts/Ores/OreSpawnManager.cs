using UnityEngine;

namespace Coreline
{
    public class OreSpawnManager : EntitySpawnManager
    {
        [SerializeField] private MineableRock mineableRock;
        [SerializeField] private OreData[] oreData;
        [SerializeField] private int spawnAmount = 3;
        [SerializeField] private float throwForce = 4f;
        [SerializeField] private float upwardForce = 2f;
        [SerializeField] private float randomSpread = 0.35f;
        
        private EntitySpawner<Ore> spawner;

        protected override void Awake()
        {
            base.Awake();
            mineableRock ??= GetComponent<MineableRock>();

            if (oreData == null || oreData.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
                return;

            spawner = new EntitySpawner<Ore>(new EntityFactory<Ore>(oreData), spawnPointStrategy);
        }

        private void OnEnable()
        {
            mineableRock.OnDepleted += Spawn;
        }

        private void OnDisable()
        {
            mineableRock.OnDepleted -= Spawn;
        }

        public override void Spawn()
        {
            for (int i = 0; i < spawnAmount; i++)
            {
                Ore ore = spawner.Spawn(out Transform spawnPoint);
                ThrowOre(ore, spawnPoint);
            }
        }

        private void ThrowOre(Ore ore, Transform spawnPoint)
        {
            Vector3 directionFromRock = spawnPoint.position - transform.position;
            if (directionFromRock.sqrMagnitude <= Mathf.Epsilon)
                directionFromRock = Random.insideUnitSphere;

            directionFromRock.Normalize();
            directionFromRock += Random.insideUnitSphere * randomSpread;
            directionFromRock.y = Mathf.Abs(directionFromRock.y) + upwardForce;
            directionFromRock.Normalize();

            ore.Throw(directionFromRock, throwForce);
        }
    }
}
