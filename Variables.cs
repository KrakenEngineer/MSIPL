using System;
using System.Collections.Generic;

namespace MSIPL
{
    //Allows to add/get/set variables and get their properties
    public sealed class VariableStorage
    {
        public readonly int MaxCount;
        public readonly Dictionary<string, Variable> Variables =
            new Dictionary<string, Variable>();

        public VariableStorage(int maxCount = int.MaxValue)
        {
            if (maxCount <= 0)
                Logger.AddMessage("Invalid max variables count at constructor of MSIPL.VariableStorage", Logger.MessageType.Debug);
            MaxCount = maxCount;
        }

        public bool Exists(string name) => Variables.ContainsKey(name);
        public int Count => Variables.Count;

        public object GetValue(string name)
        {
            if (Exists(name))
                return Variables[name].Get();
            Logger.AddMessage("Variable doesn't exist at MSIPL.VariableStorage.GetValue", Logger.MessageType.RuntimeError);
            return null;
        }
        public Variable GetPointer(string name)
        {
            if (Exists(name))
                return Variables[name];
            Logger.AddMessage("Variable doesn't exist at MSIPL.VariableStorage.GetPointer", Logger.MessageType.RuntimeError);
            return null;
        }
        public void SetValue(string name, object value)
        {
            if (!Exists(name))
                Logger.AddMessage("Variable doesn't exist at MSIPL.VariableStorage.SetValue", Logger.MessageType.RuntimeError);
            else
                Variables[name].Set(value);
        }

        public bool TryAdd(Variable variable)
        {
            if (variable == null || Exists(variable.Name))
                return false;

            if (Count < MaxCount)
                Variables.Add(variable.Name, variable);
            return Count <= MaxCount;
        }

        public override string ToString()
        {
            string output = "";

            foreach (var item in Variables)
                output += item.Value.ToString() + '\n';

            return output;
        }
    }

    public sealed class Variable
    {
        private readonly bool _isReadonly;
        private readonly DataType _type;
        private readonly string _name;
        private object _value;
        private Variable(Variable var)
        {
            _type = var._type;
            _name = var._name;
            _value = var._value;
            _isReadonly = var._isReadonly;
        }

        private Variable(DataType type, string name, object value, bool isReadonly)
        {
            _type = type;
            _name = name;
            _value = value;
            _isReadonly = isReadonly;
        }

        public static Variable Create<T>(string name, T value, bool isReadonly) =>
            new Variable(TypeSystem.TypeOf(typeof(T)), name, value, isReadonly);

        public static Variable Label(string name, uint value) =>
            new Variable(DataType.Label, name, value, true);

        public object Get() => _value;
        public void Set(object value)
        {
            if (value == null || TypeSystem.TypeOf(value.GetType()) != _type)
            {
                Logger.AddMessage("Invalid value at MSIPL.Variable.Set. This error is a bug, so report that TypeSystem.ConvertValue is missing somewhere or it doesn't work correctly", Logger.MessageType.Debug);
                return;
            }
            _value = value;
        }
        public Variable Clone() => new Variable(this);
        //WARNING: USE THIS METHOD ONLY IF YOU ARE SURE THAT YOU NEED IT
        public void Clear() => _value = null;

        public string Name => _name;
        public DataType Type => _type;
        public bool IsReadonly => _isReadonly;
        public bool IsNull => _value == null || (_value is LogicPart p && p.PartParent == null);
        public override string ToString() => $"{_type} {_name} {_value} {_isReadonly}";
    }

    public enum DataType
    {
        None,
        Int,
        Float,
        Bool,
        Char, //system type of IO
        Part,
        Label
    }
}
