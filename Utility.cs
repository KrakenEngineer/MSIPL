using System.Collections.Generic;
using System;

namespace MSIPL
{
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
                    v.Value.Clear();
        }

        private static void Collect(MemoryCell mem)
        {
            for (long i = 0; i < mem.Size; i++)
                if (mem.IsNull(i))
                    mem.Clear(i);
        }
    }

    public static class TypeSystem
    {
        public static DataType TypeOf(string original)
        {
            return original switch
            {
                "int" => DataType.Int,
                "float" => DataType.Float,
                "bool" => DataType.Bool,
                "part" => DataType.Part,
                _ => DataType.None
            };
        }

        public static DataType TypeOf(object value)
        {
            if (value == null)
                return DataType.None;
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
            if (type == typeof(LogicPart))
                return DataType.Part;
            return DataType.None;
        }

        public static object ConvertValue(object value, DataType t)
        {
            if (value is LogicPart) value = (value as LogicPart).InstanceID;
            return t switch
            {
                DataType.Int => Convert.ToInt64(value),
                DataType.Float => Convert.ToDouble(value),
                DataType.Bool => Convert.ToBoolean(value),
                DataType.Part => GameplayController.Instance.PartComponents[Convert.ToUInt64(value)],
                _ => null
            };
        }
    }

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
                ErrorType.variable_doesnt_exist => $"Variable {content[0]} doesn't exist",
                ErrorType.argument_count => $"Function {content[0]}() requires {content[1]} parameters when {content[2]} given",
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

        public static string GenerateDataType(string source, uint line, DataType t, string v) =>
            Generate(ErrorType.data_type, source, line, t, v);

        public static string GenerateValue(string source, uint line) =>
            Generate(ErrorType.value, source, line);

        public static string GenerateVariableDoesntExist(string source, uint line, string name) =>
            Generate(ErrorType.variable_doesnt_exist, source, line, name);

        public static string GenerateArgumentCount(string source, uint line, string func, int req, int argc) =>
            Generate(ErrorType.argument_count, source, line, func, req, argc);

        public static string GenerateReturnVariable(string source, uint line, string func, bool req) =>
            Generate(ErrorType.return_variable, source, line, func, req);

        public static string GeneratePart(string source, uint line, string name) =>
            Generate(ErrorType.part, source, line, name);
    }

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
        variable_doesnt_exist,
        argument_count,
        return_variable,
        part
    }
}