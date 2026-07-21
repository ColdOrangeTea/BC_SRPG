using System.Collections.Generic;
using BlackChess.SRPG.Items;

namespace BlackChess.SRPG.Units
{
    /// <summary>
    /// 單位 / 玩家持有的道具清單。撿起場地道具後會加進來，使用道具時從這裡取出。
    /// (可依需求改成整個玩家勢力共用一個 Inventory，或每個單位各自一份。)
    /// </summary>
    public class Inventory
    {
        private readonly List<ItemData> _items = new List<ItemData>();
        public IReadOnlyList<ItemData> Items => _items;

        public int Count => _items.Count;

        public void Add(ItemData item)
        {
            if (item != null) _items.Add(item);
        }

        public bool Remove(ItemData item) => _items.Remove(item);

        public bool Has(ItemData item) => _items.Contains(item);
    }
}
