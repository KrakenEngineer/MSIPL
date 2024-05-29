using System;

namespace MSIPL
{
    [Serializable]
    public sealed class MemoryCell : PartComponentBase
    {
        private object[] _data;
        public int Size => _data == null ? 0 : _data.Length;

        public override void OnPartCreated()
        {
            if (PartParent.Mode != Part.PartMode.gameplay)
                return;

            int size = ((ModuleMemoryCell)ParentConfig).Size;
            if (size <= 0)
                throw new Exception($"Invalid memory cell size {size}");
            _data = new object[size];
        }

        public bool IsNull(long index) => _data[index] == null || (_data[index] is LogicPart p && p.PartParent == null);
        public void GetType(long index, Variable v) => v.Set((long)TypeSystem.TypeOf(_data[index]));
        public void Get(long index, Variable v) => v.Set(TypeSystem.ConvertValue(_data[index], v.Type));
        public void Set(long index, object v) => _data[index] = v;
        public void Clear(long index) => _data[index] = null;
    }

    [Serializable]
    public sealed class ModuleMemoryCell : ModuleBase
    {
        public int Size;
    }
}