using System;
using System.Collections.Generic;

//Mindustry-like memory cell. Works as a huge array, stores values of any type
//Allows to get some additional info (like first free index) about itself
namespace MSIPL
{
    [Serializable]
    public sealed class MemoryCell : PartComponentBase
    {
        private object[] _data;
        private ModuleMemoryCell _config;
        public override string Name => "memory";
        public int Size => (int)Properties["size"].Value;

        public override void OnPartCreated()
        {
            _config = (ModuleMemoryCell)ParentConfig;
            base.OnPartCreated();
            if (PartParent.Mode != Part.PartMode.gameplay)
                return;

            if (Size <= 0)
                throw new Exception($"Invalid memory cell size {Size}");
            _data = new object[Size];
        }

        protected override void GetProperties(out Dictionary<string, PartProperty> prop)
        {
            base.GetProperties(out prop);
            prop.Add("size", PartProperty.ROInt(_config.Size));
            prop.Add("free_count", PartProperty.ROInt(_config.Size));
            prop.Add("first_free", PartProperty.ROInt(0));
            prop.Add("last_free", PartProperty.ROInt(_config.Size - 1));
        }

		[BringToMSIPL]
        public bool is_null(long index) => _data[index] == null ||
			(_data[index] is LogicPart p && p.PartParent == null) ||
			(_data[index] is Component c && (c.Part == null || c.Get() == null || c.Part == null || c.Part.PartParent == null));
		[BringToMSIPL]
		public long get_type(long index) => (long)TypeSystem.TypeOf(_data[index]);
		[BringToMSIPL(GetComp = false)]
		public object get_value(long index) => _data[index];
		[BringToMSIPL(GetComp = false)]
		public void set_value(long index, object v)
        {
            if (_data[index] is LogicPart p) p.RemoveUsing();
            if (_data[index] is Component c) c.RemoveUsing();
            if (is_null(index))
            {
                if (v is LogicPart p1) p1.AddUsing();
                if (v is Component c1) c1.AddUsing();
                Properties["free_count"].Value = (int)Properties["free_count"].Value - 1;
                if ((int)Properties["first_free"].Value == index)
                {
                    int i = (int)Properties["first_free"].Value;
                    while (i < Size && !is_null(i)) i++;
                    Properties["first_free"].Value = i;
                }
                if ((int)Properties["last_free"].Value == index)
                {
                    int i = (int)Properties["last_free"].Value;
                    while (i > 0 && !is_null(i)) i--;
                    Properties["first_free"].Value = i;
                }
            }
            _data[index] = v;
        }
		[BringToMSIPL]
        public void clear(long index)
        {
            if (_data[index] is LogicPart p) p.RemoveUsing();
            if (_data[index] is Component c) c.RemoveUsing();
            _data[index] = null;
            Properties["free_count"].Value = (int)Properties["free_count"].Value - 1;
            if (is_null(index))
            {
                if (index < (int)Properties["first_free"].Value)
                    Properties["first_free"].Value = index;
                if (index > (int)Properties["last_free"].Value)
                    Properties["last_free"].Value = index;
            }
        }
    }

    [Serializable]
    public sealed class ModuleMemoryCell : ModuleBase
    {
        public int Size;
    }
}