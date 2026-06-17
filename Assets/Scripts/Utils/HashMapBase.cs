using System;
using UnityEngine;

namespace Platformer
{
    public class HashMapBase <TKey, TValue>
    {
        private class Node
        {
            public TKey Key { get; }
            public TValue Value { get; set; }
            public Node Next { get; set; }
            
            public Node(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
        }

        private Node[] buckets; //array that contains the key and value
        private int capacity;
        public int Count { get;  private set; }

        public HashMapBase(int capacity = 16)
        {
            this.capacity = capacity;
            buckets = new Node[capacity];
            Count = 0;
        }
        
        //Hash function that maps a key to a bucket index
        private int GetBucketIndex(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            
            //Ensure the index is positive by masking out the sign
            int hashCode = key.GetHashCode() & 0x7fffffff;
            return hashCode % capacity; 
        }
        
        //Insert or update a key-value pair
        public void Insert_Update(TKey key, TValue value)
        {
            int index = GetBucketIndex(key);
            Node current = buckets[index];
            
            //Check if key already exists in the chain to update its value 
            while (current != null)
            {
                if (current.Key.Equals(key))
                {
                    current.Value = value;
                    return;
                }
                current = current.Next;
            }
            
            //Key does not exist so insert a new node at the fron of the chain
            Node newNode = new Node(key, value);
            newNode.Next = buckets[index];
            buckets[index] = newNode;
            Count++;
        }

        //Get a value from its key
        public bool TryGetValue(TKey key, out TValue value)
        {
            int index = GetBucketIndex(key);
            Node current = buckets[index];

            while (current != null)
            {
                if (current.Key.Equals(key))
                {
                    value = current.Value;
                    return true;
                }
                current = current.Next;
            }

            value = default;
            return false;
        }
        
        //Remove a key-value pair from the map
        public bool Remove(TKey key)
        {
            int index = GetBucketIndex(key);
            Node current = buckets[index];
            Node previous = null;

            while (current != null)
            {
                if (current.Key.Equals(key))
                {
                    if (previous == null)
                    {
                        //Node to remove is the head of the bucket list
                        buckets[index] = current.Next;
                    }
                    else
                    {
                        //Node to remove is in the middle or end
                        previous.Next = current.Next;
                    }
                    Count--;
                    return true;
                }
                previous = current;
                current = current.Next;
            }
            return false;
        }
    }
}
