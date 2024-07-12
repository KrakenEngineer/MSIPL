using MSIPL;
using System;
using System.Reflection;

namespace MSIPL
{
	//Stores a part/component method to use it easier in MSIPL
	//Use attribute "BringToMSIPL" to method of part/component and PartMethod instance will appeat in GameData.TypeToPartMethods automatically
    public class PartMethod
    {
        private readonly MethodInfo _method;
        private readonly DataType[] _argTypes;
        private readonly DataType _type;

        private PartMethod(MethodInfo method, DataType type, DataType[] argTypes)
        {
            _method = method;
            _type = type;
            _argTypes = argTypes;
        }

        public static PartMethod Create(MethodInfo m)
        {
            if (m == null) throw new Exception($"Method is null");
            if (!Attribute.IsDefined(m, typeof(BringToMSIPL))) throw new Exception($"Method {m.Name} is not a MSIPL method");
            ParameterInfo[] p = m.GetParameters();
            var argt = new DataType[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                argt[i] = TypeSystem.TypeOf(p[i].ParameterType);
                if (argt[i] == DataType.Void)
                    throw new Exception($"Invalid type of argument {i} of {m.Name}");
            }
            return new PartMethod(m, TypeSystem.TypeOf(m.ReturnType), argt);
        }

        public object Invoke(Variable part, Argument[] args, Processor i)
        {
            if (IsInvalid(part))
            {
                i.ThrowError($"Invalid part for method {_method.Name} at MSIPL.PartMethod.Invoke");
                return null;
            }
			bool getComp = ((BringToMSIPL)Attribute.GetCustomAttribute(_method, typeof(BringToMSIPL))).GetComp;
			var argtemp = new object[args.Length];
			for (int j = 0; j < args.Length; j++)
			{
				argtemp[j] = args[j].GetValue(i);
				if (argtemp[j] is Variable v) argtemp[j] = v.Get();
				if (argtemp[j] is LogicPart p) argtemp[j] = p.PartParent;
				if (argtemp[j] is Component c && getComp) argtemp[j] = c.Get();
				DataType argt = TypeSystem.TypeOf(argtemp[j]);
				if (argt != _argTypes[j] && _argTypes[j] != DataType.Any)
				{
					i.ThrowError($"Invalid type of argument {j} of method {_method.Name} at MSIPL.PartMethod.Invoke");
					return null;
				}
			}
			object target = part.Get();
			if (target is LogicPart p1) target = p1.PartParent;
			if (target is Component c1) target = c1.Get();
            return _method.Invoke(target, argtemp);
        }

        public bool IsInvalid(Variable part) => part == null || part.IsNull ||
            (part.Type != DataType.Part && part.Type != DataType.Component) ||
            (part.Type == DataType.Part && typeof(Part) != _method.DeclaringType) ||
            (part.Type == DataType.Component && !(part.Get() as Component).Type.IsSubclassOf(_method.DeclaringType) && (part.Get() as Component).Type != _method.DeclaringType);
        public DataType Type => _type;
    }

	//Makes part/component method avaliable in MSIPL
	//GetComp = false means "don't extract PartComponentBase from MSIPL.Component" (Utilities.cs)
    public class BringToMSIPL : Attribute { public bool GetComp = true; }
}

//Represents part/component property for MSIPL and part itself, stores int/float/bool/configurable versions
//Use methods like "control flag" of "ro int" to create properties or write your own template as soon as you need
public class PartProperty
{
    public readonly bool IsReadOnly;
    public readonly bool IsConfigurable;
    public readonly DataType Type;
    private object _value;

    private PartProperty(bool isReadOnly, bool isConfigurable, DataType type, object value = null)
        {
            IsReadOnly = isReadOnly;
            IsConfigurable = isConfigurable;
            Type = type;

            if (value != null)
                _value = value;
            else
            {
                _value = type switch
                {
                    DataType.Int => 0,
                    DataType.Float => 0f,
                    DataType.Bool => false,
                    _ => null
                };
            }
        }

    public static PartProperty ControlFlag(bool v = false) =>
        new PartProperty(false, false, DataType.Bool, v);

    public static PartProperty ROFlag(bool v = false) =>
        new PartProperty(true, false, DataType.Bool, v);

    public static PartProperty ROInt(int v = 0) =>
        new PartProperty(true, false, DataType.Int, v);

    public static PartProperty ROFloat(float v = 0f) =>
        new PartProperty(true, false, DataType.Float, v);

    public static PartProperty ConfInt(ConfigurableInt v) => v == null ?
        throw new Exception("Configurable int is null") : new PartProperty(false, true, DataType.Int, v);

    public static PartProperty ConfFloat(ConfigurableFloat v) => v == null ?
        throw new Exception("Configurable float is null") : new PartProperty(false, true, DataType.Float, v);

    public static PartProperty ROUnsafe(object v, DataType t, bool sure = true) => sure ?
        throw new Exception("You are not sure. Read how PartProperty works") : new PartProperty(true, false, t, v);

    private DataType TypeOf(object val)
    {
        if (val is int || val is ConfigurableInt) return DataType.Int;
        if (val is float || val is ConfigurableFloat) return DataType.Float;
        if (val is bool || val is ConfigurableBool) return DataType.Bool;
        if (val is Part) return DataType.Part;
        return DataType.Void;
    }

    private bool IsTypeConfigurable(Type t) =>
        t == typeof(ConfigurableInt) || t == typeof(ConfigurableFloat) || t == typeof(ConfigurableBool);

    public object Value
    {
        get => _value;
        set
        {
            if (TypeOf(value) != Type || IsTypeConfigurable(value.GetType()) != IsConfigurable)
                throw new Exception("Invalid value");
            else if (IsTypeConfigurable(value.GetType()))
                throw new Exception("Cannot replace configurable with another configurable");
            else if (!IsConfigurable)
                _value = value;
            else
            {
                switch (Type)
                {
                    case DataType.Int:
                        ((ConfigurableInt)_value).Value = (int)value;
                        break;
                    case DataType.Float:
                        ((ConfigurableFloat)_value).Value = (float)value;
                        break;
                    case DataType.Bool:
                        ((ConfigurableBool)_value).Value = (bool)value;
                        break;
                    default:
                        throw new Exception("Invalid type of configurable value");
                }
            }
        }
    }
}
