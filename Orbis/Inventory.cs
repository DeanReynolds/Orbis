using System.Linq;

namespace Orbis
{
    public class Inventory
    {
        /// <summary>
        /// The size of the player's inventory
        /// </summary>
        public const int PlayerInvSize = 7 * 5;
        /// <summary>
        /// The items in the player's inventory.
        /// </summary>
        private readonly Item[] _items;

        public Inventory(int slots) { _items = new Item[slots]; }

        public int Capacity => _items.Length;

        public int Add(Item item)
        {
            foreach (var _item in _items.Where(_item => (_item != null) && (_item.Key == item.Key) && (_item.Stack < _item.MaxStack)))
            {
                if (_item.Stack > _item.MaxStack)
                {
                    _item.Stack += item.Stack;
                    return 0;
                }
                var canAdd = (_item.MaxStack - _item.Stack);
                if (canAdd >= item.Stack)
                {
                    _item.Stack += item.Stack;
                    return 0;
                }
                if (_item.Stack < _item.MaxStack) _item.Stack = _item.MaxStack;
                item.Stack -= canAdd;
            }
            if (item.Stack <= 0) return 0;
            for (var i = 0; i < _items.Length; i++)
            {
                var _item = _items[i];
                if (_item != null) continue;
                _items[i] = item.Clone(item.Stack);
                _item = _items[i];
                if (_item.Stack <= _item.MaxStack) return 0;
                _item.Stack = _item.MaxStack;
                item.Stack -= _item.MaxStack;
            }
            return item.Stack;
        }
        public void Set(int slot, Item item) { _items[slot] = item; }

        public void Remove(int slot) { _items[slot] = null; }
        public bool Remove(Item item)
        {
            if (!Has(item)) return false;
            for (var i = 0; i < _items.Length; i++)
            {
                var _item = _items[i];
                if (_item.Key != item.Key) continue;
                _item.Stack = (_item.Stack - item.Stack);
                if (_item.Stack == 0)
                {
                    _items[i] = null;
                    return true;
                }
                if (_item.Stack < 0)
                {
                    item.Stack += _item.Stack;
                    _items[i] = null;
                }
                else return true;
            }
            return false;
        }

        public bool Has(Item item) { return (_items.Where(_item => _item.Key == item.Key).Aggregate(item.Stack, (current, _item) => current - _item.Stack) <= 0); }
        public Item Get(int slot) { return _items[slot]; }
    }
}