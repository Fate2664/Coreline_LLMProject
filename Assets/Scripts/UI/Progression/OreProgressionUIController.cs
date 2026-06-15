using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;
using UnityEngine.Events;

namespace Coreline
{
    public class OreProgressionUIController : MonoBehaviour
    {
        private const string OresRootName = "OresRoot";

        [Header("References")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private ListView oresRoot;

        [Header("Ore Progression")]
        [SerializeField] private List<OreItemSO> oreItems = new();
        [SerializeField] private int requiredAmountPerOre = 100;

        [Header("Colours")]
        [SerializeField] private Color incompleteColor = new(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color completeColor = new(0.18f, 0.45f, 0.22f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent onAllOresCompleted;

        private readonly List<OreProgressionEntry> entries = new();
        private readonly Dictionary<OreType, OreProgressionEntry> entriesByOreType = new();
        private bool listInitialized;
        private bool inventorySubscribed;
        private bool initializedFromCurrentInventory;
        private bool allOresCompleted;

        public bool AllOresCompleted => allOresCompleted;

        private void Awake()
        {
            EnsureReferences();
            InitializeList();
            BuildEntries();
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeList();
            SubscribeToInventory();
            BuildEntries();
            InitializeProgressFromCurrentInventory();
            RefreshList();
        }

        private void OnDisable()
        {
            UnsubscribeFromInventory();
        }

        private void Start()
        {
            InitializeProgressFromCurrentInventory();
            RefreshList();
        }

        public int GetCollectedAmount(OreType oreType)
        {
            return entriesByOreType.TryGetValue(oreType, out OreProgressionEntry entry) ? entry.CollectedAmount : 0;
        }

        public void Bind(PlayerInventory inventory)
        {
            if (playerInventory == inventory)
            {
                return;
            }

            UnsubscribeFromInventory();
            playerInventory = inventory;
            initializedFromCurrentInventory = false;
            SubscribeToInventory();
            InitializeProgressFromCurrentInventory();
            RefreshList();
        }

        private void HandleItemAddedToInventory(InventoryItemData item, int amount)
        {
            if (item is not OreItemSO oreItem || amount <= 0)
            {
                return;
            }

            AddOreProgress(oreItem.oreType, amount);
        }

        private void AddOreProgress(OreType oreType, int amount)
        {
            if (!entriesByOreType.TryGetValue(oreType, out OreProgressionEntry entry))
            {
                return;
            }

            entry.CollectedAmount += amount;
            RefreshList();
            CheckCompletion();
        }

        private void InitializeProgressFromCurrentInventory()
        {
            if (initializedFromCurrentInventory || playerInventory == null)
            {
                return;
            }

            initializedFromCurrentInventory = true;
            foreach (OreProgressionEntry entry in entries)
            {
                if (entry?.OreItem == null)
                {
                    continue;
                }

                entry.CollectedAmount = Mathf.Max(entry.CollectedAmount, playerInventory.GetOreCount(entry.OreItem.oreType));
            }

            CheckCompletion();
        }

        private void BuildEntries()
        {
            Dictionary<OreType, int> previousCounts = new();
            foreach (OreProgressionEntry existingEntry in entries)
            {
                if (existingEntry?.OreItem != null)
                {
                    previousCounts[existingEntry.OreType] = existingEntry.CollectedAmount;
                }
            }

            entries.Clear();
            entriesByOreType.Clear();

            foreach (OreItemSO oreItem in oreItems)
            {
                if (oreItem == null || entriesByOreType.ContainsKey(oreItem.oreType))
                {
                    continue;
                }

                OreProgressionEntry entry = new(oreItem);
                if (previousCounts.TryGetValue(oreItem.oreType, out int previousAmount))
                {
                    entry.CollectedAmount = previousAmount;
                }

                entries.Add(entry);
                entriesByOreType.Add(oreItem.oreType, entry);
            }

            if (oresRoot != null && listInitialized)
            {
                oresRoot.SetDataSource(entries);
            }
        }

        private void InitializeList()
        {
            if (listInitialized || oresRoot == null)
            {
                return;
            }

            oresRoot.AddDataBinder<OreProgressionEntry, InventoryItemVisuals>(BindOreProgressionItem);
            oresRoot.SetDataSource(entries);
            listInitialized = true;
        }

        private void BindOreProgressionItem(Data.OnBind<OreProgressionEntry> evt, InventoryItemVisuals target, int index)
        {
            OreProgressionEntry entry = evt.UserData;
            int requiredAmount = Mathf.Max(1, requiredAmountPerOre);
            int displayedAmount = Mathf.Min(entry.CollectedAmount, requiredAmount);
            bool isComplete = entry.CollectedAmount >= requiredAmount;

            if (target.ContentRoot != null)
            {
                target.ContentRoot.gameObject.SetActive(true);
            }

            if (target.ToolTipRoot != null)
            {
                target.ToolTipRoot.gameObject.SetActive(false);
            }

            if (target.Image != null && entry.OreItem != null && entry.OreItem.itemDesc != null)
            {
                target.Image.SetImage(entry.OreItem.itemDesc.Icon);
            }

            if (target.Count != null)
            {
                target.Count.Text = $"{displayedAmount}/{requiredAmount}";
            }

            if (target.ToolTipText != null)
            {
                string oreName = entry.OreItem != null && entry.OreItem.itemDesc != null
                    ? entry.OreItem.itemDesc.Name
                    : entry.OreType.ToString();
                target.ToolTipText.Text = $"{oreName}: {displayedAmount}/{requiredAmount}";
            }

            if (target.ItemRoot != null)
            {
                target.ItemRoot.Color = isComplete ? completeColor : incompleteColor;
            }
        }

        private void RefreshList()
        {
            if (oresRoot != null && oresRoot.gameObject.activeInHierarchy)
            {
                oresRoot.Refresh();
            }
        }

        private void CheckCompletion()
        {
            if (allOresCompleted || entries.Count == 0)
            {
                return;
            }

            int requiredAmount = Mathf.Max(1, requiredAmountPerOre);
            foreach (OreProgressionEntry entry in entries)
            {
                if (entry.CollectedAmount < requiredAmount)
                {
                    return;
                }
            }

            allOresCompleted = true;
            onAllOresCompleted?.Invoke();
        }

        private void SubscribeToInventory()
        {
            if (inventorySubscribed || playerInventory == null)
            {
                return;
            }

            playerInventory.ItemAddedToInventory += HandleItemAddedToInventory;
            inventorySubscribed = true;
        }

        private void UnsubscribeFromInventory()
        {
            if (!inventorySubscribed || playerInventory == null)
            {
                return;
            }

            playerInventory.ItemAddedToInventory -= HandleItemAddedToInventory;
            inventorySubscribed = false;
        }

        private void EnsureReferences()
        {
            playerInventory ??=
                FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            oresRoot ??= FindChildListView(OresRootName);
            oresRoot ??= GetComponentInChildren<ListView>(true);
        }

        private ListView FindChildListView(string objectName)
        {
            ListView[] listViews = GetComponentsInChildren<ListView>(true);
            foreach (ListView listView in listViews)
            {
                if (listView != null && listView.name == objectName)
                {
                    return listView;
                }
            }

            return null;
        }

        [Serializable]
        private class OreProgressionEntry
        {
            public OreProgressionEntry(OreItemSO oreItem)
            {
                OreItem = oreItem;
            }

            public OreItemSO OreItem { get; }
            public OreType OreType => OreItem != null ? OreItem.oreType : default;
            public int CollectedAmount;
        }
    }
}
