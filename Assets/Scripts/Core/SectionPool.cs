using System.Collections.Generic;
using UnityEngine;

namespace TerrainErosion
{
    public class SectionPool
    {
        private LayerSection[] pool;
        private Queue<LayerSection> free;
        private int size;

        public SectionPool(int capacity)
        {
            size = capacity;
            pool = new LayerSection[capacity];
            free = new Queue<LayerSection>(capacity);

            for (int i = 0; i < capacity; i++)
            {
                pool[i] = new LayerSection(0f, SoilType.Bedrock);
                free.Enqueue(pool[i]);
            }
        }

        public LayerSection Get(float height, SoilType type)
        {
            if (free.Count == 0)
            {
                Debug.LogError("SectionPool: Out of memory!");
                return null;
            }

            LayerSection section = free.Dequeue();
            section.height = height;
            section.soilType = type;
            section.saturation = 0f;
            return section;
        }

        public void Release(LayerSection section)
        {
            if (section == null) return;
            section.Reset();
            free.Enqueue(section);
        }

        public void Reset()
        {
            free.Clear();
            for (int i = 0; i < size; i++)
            {
                pool[i].Reset();
                free.Enqueue(pool[i]);
            }
        }
    }
}