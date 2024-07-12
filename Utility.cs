using System.Collections.Generic;
using System;
using NCalc;

namespace MSIPL
{
	//Clears references to destroyed parts/components from variables and memory cells
	//So real garbage collector can handle them
    public static class GarbageCollector
    {
        private static int _timer = 0;
        private static readonly SortedSet<LogicPart> _parts = new SortedSet<LogicPart>();

        public static void Update()
        {
            if (_timer < 1000)
                _timer++;
            else
            {
                _timer = 0;
                foreach (var p in _parts)
                    Collect(p);
            }
        }

        public static bool TryAdd(LogicPart part) => _parts.Add(part);

        private static void Delete(LogicPart part) => _parts.Remove(part);

        private static void Collect(LogicPart part)
        {
            if (part.PartParent == null)
            {
                Delete(part);
                return;
            }
            if (part.PartParent.TryGetComponent(out Processor proc))
                Collect(proc);
            if (part.PartParent.TryGetComponent(out MemoryCell mem))
                Collect(mem);
        }

        private static void Collect(Processor proc)
        {
            foreach (var v in proc.Variables.Variables)
                if (v.Value.IsNull)
                    v.Value.Clear(false);
        }

        private static void Collect(MemoryCell mem)
        {
            for (long i = 0; i < mem.Size; i++)
                if (mem.is_null(i))
                    mem.clear(i);
        }
    }

	//Responsible for type recognision & convertation. If you add custom type - don't forget to handle it here
    public static class TypeSystem
    {
        public static DataType TypeOf(string original)
        {
            return original switch
            {
                "int" => DataType.Int,
                "float" => DataType.Float,
                "bool" => DataType.Bool,
                _ => DataType.Void
            };
        }

        public static DataType TypeOf(object value)
        {
            if (value == null)
                return DataType.Void;
            return TypeOf(value.GetType());
        }

        public static DataType TypeOf(Type type)
        {
            if (type == typeof(int) || type == typeof(long))
                return DataType.Int;
            if (type == typeof(float) || type == typeof(double))
                return DataType.Float;
            if (type == typeof(bool))
                return DataType.Bool;
			if (type == typeof(string))
				return DataType.Str;
            if (type == typeof(LogicPart) || type == typeof(Part) || type.IsSubclassOf(typeof(Part)))
                return DataType.Part;
            if (type == typeof(Component))
                return DataType.Component;
            return DataType.Any;
        }

        public static object ConvertValue(object value, DataType t)
        {
            if (!t.IsValueType())
                Logger.AddMessage($"Invalid type {t} for convertation", Logger.MessageType.RuntimeError);
            return t switch
            {
                DataType.Int => Convert.ToInt64(value),
                DataType.Float => Convert.ToDouble(value),
                DataType.Bool => Convert.ToBoolean(value),
                _ => null
            };
        }

        public static bool IsValueType(this DataType t) => t == DataType.Int || t == DataType.Float || t == DataType.Bool;
    }

	//Generates some boring template errors. If you throw some other error much - add it to this generator and use it
	//Don't forget to add corresponding ErrorType
    public static class ErrorGenerator
    {
        private static string Generate(ErrorType t, string source, uint line, params object[] content)
        {
            string error = t switch
            {
                ErrorType.parameter => $"Invalid parameter {content[0]} which means {content[1]}",
                ErrorType.function_name => $"Unknown {content[0]} function {content[1]}",
                ErrorType.syntax => $"Invalid {content[0]}",
                ErrorType.expression => $"Invalid expression {content[0]}",
                ErrorType.data_type => $"Invalid type {content[0]} of variable {content[1]}",
                ErrorType.readonly_var => $"Variable {content[0]} is readonly",
                ErrorType.value => "Invalid value",
                ErrorType.variable_existance => $"Variable {content[0]} " + ((bool)content[1] ? "already exists" : "doesn't exist"),
                ErrorType.argument_count => $"Function {content[0]}() requires {content[1]} parameters when {content[2]} given",
                ErrorType.argument_type => $"Argument {content[1]} of function {content[0]}() should have type {content[2]} while {content[3]} was given",
                ErrorType.return_variable => $"Function {content[0]}() {((bool)content[1] ? "requires" : "doesn't require")} a variable for return value",
                ErrorType.part => $"Part {content[0]} doesn't exist",
                _ => (string)content[0]
            };

            error += $" at MSIPL.{source}, line number is {line}";
            return error;
        }
        
        public static string GenerateNone(string source,  uint line, string content) =>
            Generate(ErrorType.none, source, line, content);

        public static string GenerateParameter(string source, uint line, string par, string sence) =>
            Generate(ErrorType.parameter, source, line, par, sence);

        public static string GenerateFunctionName(string source, uint line, string ins, string func) =>
            Generate(ErrorType.function_name, source, line, ins, func);

        public static string GenerateSyntax(string source, uint line, string syntax) =>
            Generate(ErrorType.syntax, source, line, syntax);

        public static string GenerateExpression(string source, uint line, string exp) =>
            Generate(ErrorType.expression, source, line, exp);

        public static string GenerateReadonly(string source, uint line, string name) =>
            Generate(ErrorType.readonly_var, source, line, name);

        public static string GenerateDataType(string source, uint line, DataType t, string var) =>
            Generate(ErrorType.data_type, source, line, t, var);

        public static string GenerateValue(string source, uint line) =>
            Generate(ErrorType.value, source, line);

        public static string GenerateVariableExistance(string source, uint line, string name, bool exists) =>
            Generate(ErrorType.variable_existance, source, line, name, exists);

        public static string GenerateArgumentCount(string source, uint line, string func, int req, int argc) =>
            Generate(ErrorType.argument_count, source, line, func, req, argc);

        public static string GenerateArgumentType(string source, uint line, string func, uint num, ArgumentType req, ArgumentType t) =>
            Generate(ErrorType.argument_type, source, line, func, num, req, t);

        public static string GenerateReturnVariable(string source, uint line, string func, bool req) =>
            Generate(ErrorType.return_variable, source, line, func, req);

        public static string GeneratePart(string source, uint line, string name) =>
            Generate(ErrorType.part, source, line, name);
    }

	//Type of template error for ErrorGenerator
    public enum ErrorType
    {
        none,
        parameter,
        function_name,
        syntax,
        expression,
        readonly_var,
        data_type,
        value,
        variable_existance,
        argument_count,
        argument_type,
        return_variable,
        part
    }

	//Allows to store component in MSIPL variable without direct reference
	//So garbage collector (real, not MSIPL one) can collect it if destroyed
    public class Component
    {
        private readonly Type _type;
        private LogicPart _part;
        private int _index;

        private Component(Type t)
        {
            _type = t;
        }

        public static Component Create(Type t)
        {
            if (t == null || !t.IsSubclassOf(typeof(PartComponentBase)))
                throw new Exception($"This type {t.Name} is not a part component");
            return new Component(t);
        }

        public PartComponentBase Get() => _part.PartParent == null ? null : _part.PartParent.GetComponent(_type, _index);

        public void Set(LogicPart p, int index)
        {
            if (p == null) throw new Exception("Part is null");
            if (index < 0) throw new Exception("Negative index is not allowed");
            if (p.PartParent == null || p.PartParent.GetComponent(_type, index) == null) throw new Exception("Component doesn't exist");
			if (_part != null) _part.RemoveUsing();
			p.AddUsing();
            _part = p;
            _index = index;
        }

        public void AddUsing()
        {
            if (_part == null) throw new Exception("This part no longer exists");
            _part.AddUsing();
        }

        public void RemoveUsing()
        {
            _part.RemoveUsing();
            _part = null;
        }

        public static implicit operator LogicPart(Component component) => component._part;
        public Type Type => _type;
        public LogicPart Part => _part;
    }

	//Represents function argument in advanced instructions
	//Use static methods like "expression" of "char var" or "variable" to create arguments or make your own template
	//Don't forget to add corresponding ArgumentType
    public class Argument
    {
        private readonly object _value;
        private readonly ArgumentType _type;

        private Argument(object value, ArgumentType type)
        {
            if (value == null)
            {
                Logger.AddMessage("Argument value is null at MSIPL.Argument constructor", Logger.MessageType.CompilationError);
                return;
            }
            _value = value;
            _type = type;
        }

        public static Argument Value(object val, bool sure = true) => sure ?
            throw new Exception("You are not sure. Read how this class works") : new Argument(val, ArgumentType.value);

        public static Argument Variable(Variable v) =>
            new Argument(v, ArgumentType.variable);

        public static Argument CharVar(Variable v) => v.Type == MSIPL.DataType.Int ?
             new Argument(v, ArgumentType.char_variable) : throw new Exception("Cannot interpret this variable as a character");

        public static Argument Expression(Expression e) =>
            new Argument(e, ArgumentType.expression);

        public static Argument String(string s) =>
            new Argument(s, ArgumentType.@string);

        public static Argument DataType(DataType t) =>
            new Argument(t, ArgumentType.data_type);

        public ArgumentType Type => _type;

        public object GetValue(Processor interpreter = null)
        {
            switch (_type)
            {
                case ArgumentType.variable: return (Variable)_value;
                case ArgumentType.char_variable: return (char)(long)((Variable)_value).Get();
                case ArgumentType.@string: return (string)_value;
                case ArgumentType.data_type: return _value;
                case ArgumentType.expression:
                    PushParameters((Expression)_value, interpreter);
                    return ((Expression)_value).Evaluate();
                default: return null;
            }
        }

        public static void PushParameters(Expression exp, Processor interpreter)
        {
			List<string> pars = exp.GetParametersNames();
            for (int i = 0; i < pars.Count; i++)
            {
                Variable v = interpreter.GetPointer(pars[i]);
                if (v.IsNull)
                {
                    interpreter.ThrowError($"Parameter {v.Name} is null at MSIPL.Argument.PushParameters");
                    return;
                }
                if (v.Type == MSIPL.DataType.Part || v.Type == MSIPL.DataType.Component)
                    exp.Parameters[pars[i]] = ((LogicPart)v.Get()).PartParent.InstanceID;
                else
                    exp.Parameters[pars[i]] = v.Get();
            }
        }
    }

	//Represents type of function argument in advanced instructions
    public enum ArgumentType
    {
        value,
        variable,
        char_variable,
        expression,
        @string,
        data_type
    }

	//Processor can't execute comp instruction for part on different ship if at leas one ship doesn't have antenna
	//So this class checks if processor can execute comp/memory
    public static class ShipChecker
    {
        public static bool CanConnect(Ship a, Ship b) =>
            a != null && b != null && (object.Equals(a, b) || (a.HasAntenna() && b.HasAntenna()));

        public static Ship GetShip(this PartComponentBase p) => ((GameplayPart)p.PartParent).ShipParent;

        public static Ship GetShip(this Component c) => c.Part.GetShip();

        public static Ship GetShip(this Variable v)
        {
            if (v == null || v.IsNull) throw new Exception("Variable is null");
            if (v.Type == DataType.Part) return ((LogicPart)v.Get()).GetShip();
            if (v.Type == DataType.Component) return ((Component)v.Get()).GetShip();
            throw new Exception("Invalid variable type");
        }

        public static bool HasAntenna(this Ship s) => false;
    }
}