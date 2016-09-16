namespace Orbis
{
    public class Item
    {
        public string Key;

        /// <summary>
        /// The current amount of item in this stack.
        /// </summary>
        public int Stack = 1;
        /// <summary>
        /// The max stack size of this item.
        /// </summary>
        public int MaxStack = 1;

        public Tile.Types? Tile;
        public byte? Style;

        public Entity Entity;

        public Item(string key) { Key = key; }
        public Item(string key, int stack) { Key = key; Stack = stack; }

        public Item Clone() { return Clone(1); }
        public Item Clone(int stack) { return new Item(Key, stack) { MaxStack = MaxStack, Tile = Tile, Style = Style }; }
    }
}