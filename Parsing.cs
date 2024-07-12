using System.Collections.Generic;
using System.Linq;
using System;
using NCalc;
using NCalc.Domain;

namespace MSIPL
{
    //Bunch of string utilites that are often used in Parser
    public static class StringUtility
    {
        public static string BeforeSeparator(this string s, char sep = ' ')
        {
            string output = "";
            for (int i = 0; i < s.Length && s[i] != sep; i++)
                output += s[i];
            return output;
        }

        public static string AfterSeparator(this string s, char sep = ' ')
        {
            string output = "";
            int start = 0;
            while (start < s.Length && s[start] != sep)
                start++;
            start++;
            for (int i = start; i < s.Length; i++)
                output += s[i];
            return output;
        }

		//Removes given amount of characters from left and right sides of string
        public static string RemoveEdges(this string s, uint left = 1, uint right = 1)
        {
            string output = "";
            for (int i = (int)left; i < s.Length - right; i++)
                output += s[i];
            return output;
        }
    }

    //Properties and small methods for Parser
    internal static class ParserProp
    {
		#region string -> enum stuff
		public static InstructionType GetInstruction(this string s)
        {
            return s switch
            {
                "var" => InstructionType.var,
                "set" => InstructionType.set,
                "jump" => InstructionType.jump,
				"label" => InstructionType.label,
                "time" => InstructionType.time,
                "console" => InstructionType.console,
                "comp" => InstructionType.comp,
                _ => InstructionType.none
            };
        }
        public static Console.InstructionType GetCIType(this string s)
        {
            return s switch
            {
                "read" => Console.InstructionType.read,
                "write" => Console.InstructionType.write,
                "pop" => Console.InstructionType.pop,
                "push" => Console.InstructionType.push,
                "can_pop" => Console.InstructionType.can_pop,
                "clear_in" => Console.InstructionType.clear,
                "clear_out" => Console.InstructionType.clear,
                "filter" => Console.InstructionType.filter,
                _ => Console.InstructionType.none
            };
        }
        public static Time.InstructionType GetTIType(this string s)
        {
            return s switch
            {
                "frames_since_launch" => Time.InstructionType.frames_since_launch,
                "seconds_since_launch" => Time.InstructionType.seconds_since_launch,
                "delta_time" => Time.InstructionType.delta_time,
                "wait" => Time.InstructionType.wait,
                _ => Time.InstructionType.none
            };
        }
		#endregion

		public static bool IsVariableNameValid(this string name, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(name) || double.TryParse(name, out double d) || bool.TryParse(name, out bool b))
                return false;
            if (name[0] == '{' && name[^1] == '}')
            {
                name = name.RemoveEdges();
                if (string.IsNullOrEmpty(name) || double.TryParse(name, out double d1) || bool.TryParse(name, out bool b1))
                    return false;
            }

            if (name.GetInstruction() != InstructionType.none)
            {
                error = $"You can't just name a variable like an instruction {name}!";
                return false;
            }
            if (name == "this")
            {
                error = "Name \"this\" is reserved at MSIPL.Parser.IsVariableNameValid";
                return false;
            }

            for (int j = 0; j < name.Length; j++)
                if (!IsSymbolValid(name[j]))
                    return false;
            return true;
        }
        private static bool IsSymbolValid(char s) => s == '_' || ('0' <= s && s <= '9') ||
            ('a' <= s && s <= 'z') || ('A' <= s && s <= 'Z');
        public static bool IsComment(this string s) => string.IsNullOrEmpty(s) || s[0] == '#';
        public static bool IsLabel(this string s) => !IsComment(s) && s.BeforeSeparator() == "label";
        public static Argument GetArgument(this string arg, Processor i)
        {
            if (arg.Contains('"'))
            {
                if (arg.Length < 2 || !(arg[0] == '"' && arg[^1] == '"'))
                    return null;
                return Argument.String(arg.RemoveEdges());
            }
            if (arg.Contains('\''))
            {
                if (arg.Length < 2 || !(arg[0] == '\'' && arg[^1] == '\''))
                    return null;
                arg = arg.RemoveEdges();
                if (!i.Variables.Exists(arg))
                    return null;
                return Argument.CharVar(i.GetPointer(arg));
            }
			if (i.Variables.Exists(arg) && !i.GetType(arg).IsValueType()) return Argument.Variable(i.GetPointer(arg));
            var exp = new Expression(arg, ExpressionOptions.OverflowProtection);
            if (exp.HasErrors() || exp.HasUnknownParameters(i.Variables))
                return null;
            return Argument.Expression(exp);
        }
    }

    //Stores function as instruction name, function name, arguments and variable for return value
	//Use RawFunction.Parse in Parser.Parse instruction case [your new instruction] to make your instruction be advanced
    internal class RawFunction
    {
        public readonly string Instruction;
        public readonly string Name;
        public readonly string[] Args;
        public readonly string ReturnVar;

        private RawFunction(string instruction, string name, string[] args, string returnVar)
        {
            Instruction = instruction;
            Name = name;
            Args = args;
            ReturnVar = returnVar;
        }

        public static RawFunction Parse(string s, uint l, Processor i)
        {
            if (s.IsComment())
            {
                i.ThrowError($"Comment is not a function at MSIPL.RawFunc.Parse, line number is {l}", Logger.MessageType.CompilationError);
                return null;
            }

            int p = 0;
            if (s[p] == ' ' || s[p] == '(' || s[p] == ')' || s[p] == '"')
            {
                i.ThrowError($"Unexpected '{s[p]}' {s} at MSIPL.RawFunc.Parse at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                return null;
            }

            //instruction name
            string ins = "";
            while (p < s.Length && s[p] != ' ')
            {
                if (s[p] == '(' || s[p] == ')')
                {
                    i.ThrowError($"Bracket unexpected {s} at MSIPL.RawFunc.Parse at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                    return null;
                }
                ins += s[p];
                p++;
            }
            p++;

            //function name
            string func = "";
            while (p < s.Length && s[p] != '(')
            {
                if (s[p] == ' ')
                {
                    i.ThrowError($"Space unexpected {s} at MSIPL.RawFunc.Parse at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                    return null;
                }
                func += s[p];
                p++;
            }
            p++;

            //arguments
            int brackets = 1;
            bool space = false;
            bool quote = false;
            var args = new List<string>() { "" };
            while (p < s.Length && brackets > 0)
            {
                switch (s[p])
                {
                    case '(':
                        if (space)
                        {
                            i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse case ( at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        brackets++;
                        args[^1] += '(';
                        break;

                    case ')':
                        if (space)
                        {
                            i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse case ) at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        brackets--;
                        if (brackets > 0)
                            args[^1] += ')';
                        break;

                    case ' ':
                        if (brackets > 1 || quote)
                        {
                            args[^1] += ' ';
                            break;
                        }
                        if (!space)
                        {
                            i.ThrowError($"Space unexpected {s} at MSIPL.RawFunc.Parse case ' ' at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        space = false;
                        args.Add("");
                        break;

                    case '"':
                        if (space)
                        {
                            i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse case \" at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        args[^1] += '"';
                        quote = !quote;
                        break;

                    case ',':
                        if (space)
                        {
                            i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse case , at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        if (brackets > 1 || quote)
                        {
                            args[^1] += ',';
                            break;
                        }
                        space = true;
                        break;

                    default:
                        if (space)
                        {
                            i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse default at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        args[^1] += s[p];
                        break;
                }
                p++;
            }
            if (args.Count == 1 && string.IsNullOrEmpty(args[0])) args.RemoveAt(0);

            if (quote)
            {
                i.ThrowError($"Invalid quotes {s} at MSIPL.RawFunc.Parse, line number is {l}", Logger.MessageType.CompilationError);
                return null;
            }
            if (brackets > 0)
            {
                i.ThrowError($"Invalid brackets count({brackets}) {s} at MSIPL.RawFunc.Parse, line number is {l}", Logger.MessageType.CompilationError);
                return null;
            }

            //variable for return value
            string ret = "";
            if (p < s.Length)
            {
                if (s[p] != ' ')
                {
                    i.ThrowError($"Space expected {s} at MSIPL.RawFunc.Parse default, line number is {l}", Logger.MessageType.CompilationError);
                    return null;
                }
                p++;
                while (p < s.Length)
                {
                    if (s[p] == ' ' || s[p] == '(' || s[p] == ')' || s[p] == '"')
                    {
                        i.ThrowError($"Unexpected '{s[p]}' {s} at MSIPL.RawFunc.Parse at position {p}, line number is {l}", Logger.MessageType.CompilationError);
                        return null;
                    }
                    ret += s[p];
                    p++;
                }
            }

            var f = new RawFunction(ins, func, args.ToArray(), ret);
            if (!f.IsValid)
            {
                Logger.AddMessage($"Invalid function {s} at MSIPL.RawFunc.Parse, line number is {l}\n" +
                    $"Has instruction name: {f.HasIns}, has name: {f.HasName}, args valid: {f.ArgsValid}", Logger.MessageType.CompilationError);
                return null;
            }

            return f;
        }

        public override string ToString()
        {
            string s = $"ins: {Instruction}, name: {Name}, ret: {ReturnVar}, args:";
            for (int i = 0; i < ArgsCount; i++)
                s += ' ' + Args[i] + ',';
            return s;
        }

        public int ArgsCount => Args.Length;
        public bool HasArgs => Args.Length > 0;

        public bool HasIns => !string.IsNullOrEmpty(Instruction);
        public bool HasName => !string.IsNullOrEmpty(Name);
        public bool ArgsValid => !HasArgs || !Args.Any(s => string.IsNullOrEmpty(s));
        public bool HasReturnVar => !string.IsNullOrEmpty(ReturnVar);
        public bool IsValid => HasIns && HasName && ArgsValid;
    }

    //Converts raw code into instructions (from Instructions.cs)
    public static class Parser
    {
        public static Instruction[] ParseScript(string script, Processor i) =>
            ParseScript(script.Split('\n'), i);

        public static Instruction[] ParseScript(string[] lines, Processor i)
        {
            uint lc = 0;
            for (int k = 0; k < lines.Length; k++)
            {
                lines[k] = lines[k].Trim().Trim('\t');
                if (lines[k].IsComment())
                    continue;
                if (lines[k].IsLabel())
                {
                    if (lines[k].Split().Length != 2)
                    {
                        i.ThrowError("Invalid label creation at MSIPL.Parser.ParseScript", Logger.MessageType.CompilationError);
                        continue;
                    }
                    string ln = lines[k].Split()[1];
                    if (!ln.IsVariableNameValid(out string err) || ln[0] == '{')
                    {
                        i.ThrowError(err == "" ? "Invalid name of label at MSIPL.Parser.ParseScript" : err, Logger.MessageType.CompilationError);
                        continue;
                    }
                    if (!i.TryAddLabel(lc, ln))
                    {
                        i.ThrowError("Invalid label at MSIPL.Parser.ParseScript", Logger.MessageType.CompilationError);
                        continue;
                    }
                }
                lc++;
            }
            var output = new List<Instruction>();
            lc = 0;
            for (int k = 0; k < lines.Length; k++)
            {
                if (lines[k].IsComment())
                    continue;
                Instruction ins = ParseInstruction(lines[k], i, lc);
                output.Add(ins);
                lc++;
            }
            return output.ToArray();
        }

        private static Instruction ParseInstruction(string s, Processor i, uint line_num)
        {
            if (s.IsComment())
                return null;
            
            InstructionType type = s.BeforeSeparator().GetInstruction();
            switch (type)
            {
                case InstructionType.var:
                    return ParseVar(s.Split(' ', 4), i, line_num);

                case InstructionType.set:
                    return ParseSet(s.Split(' ', 3), i, line_num);

                case InstructionType.jump:
                    return ParseJump(s.Split(' ', 3), i, line_num);

				case InstructionType.label:
					return new Label();

                case InstructionType.time:
                    return ParseTime(RawFunction.Parse(s, line_num, i), i, line_num);

                case InstructionType.console:
                    return ParseConsole(RawFunction.Parse(s, line_num, i), i, line_num);

                case InstructionType.comp:
                    return ParseComp(RawFunction.Parse(s, line_num, i), i, line_num);

                default:
                    i.ThrowError($"Unknown instruction {type} at MSIPL.Parser.ParseInstruction, line number is {line_num}", Logger.MessageType.CompilationError);
                    return null;
            }
        }

		#region parsing methods for each instruction
		private static Var ParseVar(string[] args, Processor i, uint line_num)
        {
            if (args.Length != 4)
            {
                i.ThrowError(ErrorGenerator.GenerateSyntax("Parser.ParseVar", line_num, "variable creation"), Logger.MessageType.CompilationError);
                return null;
            }

            DataType t = TypeSystem.TypeOf(args[1]);
            bool isNameValid = args[2].IsVariableNameValid(out string err);
            if (!isNameValid)
            {
                i.ThrowError(ErrorGenerator.GenerateSyntax(err == "" ? "Parser.ParseVar" : err, line_num, "variable name"), Logger.MessageType.CompilationError);
                return null;
            }
            bool isReadonly = args[2][0] == '{';
            if (isReadonly) args[2] = args[2].RemoveEdges();
            if (!t.IsValueType())
            {
                i.ThrowError($"Value type is reqired at MSIPL.Parser.ParseVar, line number is {line_num}", Logger.MessageType.CompilationError);
                return null;
            }
            Variable _var = null;
            var exp = new Expression(args[^1], ExpressionOptions.OverflowProtection);
            if (exp.HasErrors() || exp.HasUnknownParameters(i.Variables))
            {
                i.ThrowError(ErrorGenerator.GenerateExpression("Parser.ParseVar", line_num, args[^1]), Logger.MessageType.CompilationError);
                return null;
            }
            if (t == DataType.Float) _var = Variable.Create(args[2], 0d, isReadonly);
            else if (t == DataType.Int) _var = Variable.Create(args[2], 0L, isReadonly);
            else if (t == DataType.Bool) _var = Variable.Create(args[2], false, isReadonly);
            else
            {
                i.ThrowError(ErrorGenerator.GenerateParameter("Parser.ParseVar", line_num, args[1], "type for a variable"), Logger.MessageType.CompilationError);
                return null;
            }
            return new Var(i, _var, Argument.Expression(exp));
        }

        private static Set ParseSet(string[] args, Processor i, uint line_num)
        {
            if (args.Length != 3)
            {
                i.ThrowError(ErrorGenerator.GenerateSyntax("Parser.ParseSet", line_num, "variable assign"), Logger.MessageType.CompilationError);
                return null;
            }
            if (!i.Variables.Exists(args[1]))
            {
                i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseSet", line_num, args[1], false), Logger.MessageType.CompilationError);
                return null;
            }
            if (i.GetPointer(args[1]).IsReadonly)
            {
                i.ThrowError(ErrorGenerator.GenerateReadonly("Parser.ParseSet", line_num, args[1]), Logger.MessageType.CompilationError);
                return null;
            }
            DataType t = i.GetType(args[1]);
            if (t == DataType.Void)
            {
                i.ThrowError(ErrorGenerator.GenerateDataType("Parser.ParseSet", line_num, t, args[1]), Logger.MessageType.CompilationError);
                return null;
            }
            if (!t.IsValueType())
            {
                i.ThrowError($"Value type is reqired at MSIPL.Parser.ParseSet, line number is {line_num}", Logger.MessageType.CompilationError);
                return null;
            }
            var exp = new Expression(args[^1], ExpressionOptions.OverflowProtection);
            if (exp.HasErrors() || exp.HasUnknownParameters(i.Variables))
            {
                i.ThrowError(ErrorGenerator.GenerateExpression("Parser.ParseSet", line_num, args[^1]), Logger.MessageType.CompilationError);
                return null;
            }
            return new Set(i, i.GetPointer(args[1]), Argument.Expression(exp));
        }

        private static Jump ParseJump(string[] args, Processor i, uint line_num)
        {
            if (args.Length != 3)
            {
                i.ThrowError(ErrorGenerator.GenerateSyntax("Parser.ParseJump", line_num, "jump"), Logger.MessageType.CompilationError);
                return null;
            }
            if (i.GetType(args[1]) != DataType.Label)
            {
                i.ThrowError($"Label {args[1]} doesn't exist at MSIPL.Parser.ParseJump, line number is {line_num}", Logger.MessageType.CompilationError);
                return null;
            }
            var exp = new Expression(args[^1], ExpressionOptions.OverflowProtection);
            if (exp.HasErrors() || exp.HasUnknownParameters(i.Variables))
            {
                i.ThrowError(ErrorGenerator.GenerateExpression("Parser.ParseJump", line_num, args[^1]), Logger.MessageType.CompilationError);
                return null;
            }
            return new Jump(i, (uint)i.GetValue(args[1]), exp);
        }

        private static Time ParseTime(RawFunction f, Processor i, uint line_num)
        {
            var type = f.Name.GetTIType();
            switch (type)
            {
                case Time.InstructionType.frames_since_launch:
                case Time.InstructionType.seconds_since_launch:
                case Time.InstructionType.delta_time:
                    if (f.HasArgs)
                    {
                        i.ThrowError(ErrorGenerator.GenerateArgumentCount($"Parser.ParseTime case {type}", line_num, $"time {type}", 0, f.ArgsCount), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (!f.HasReturnVar)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReturnVariable($"Parser.ParseTime case {type}", line_num, $"time {type}", true), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (!i.Variables.Exists(f.ReturnVar))
                    {
                        i.ThrowError(ErrorGenerator.GenerateVariableExistance($"Parser.ParseTime case {type}", line_num, f.ReturnVar, false), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (i.GetPointer(f.ReturnVar).IsReadonly)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReadonly($"Parser.ParseTime case {type}", line_num, f.ReturnVar), Logger.MessageType.CompilationError);
                        return null;
                    }
                    DataType t = i.GetType(f.ReturnVar);
                    if (!(t == DataType.Float || (type == Time.InstructionType.frames_since_launch && t == DataType.Int)))
                    {
                        i.ThrowError(ErrorGenerator.GenerateDataType($"Parser.ParseTime case {type}", line_num, t, f.ReturnVar), Logger.MessageType.CompilationError);
                        return null;
                    }
                    return new Time(i, type, Argument.Variable(i.GetPointer(f.ReturnVar)));

                case Time.InstructionType.wait:
                    if (f.ArgsCount != 1)
                    {
                        i.ThrowError(ErrorGenerator.GenerateArgumentCount("Parser.ParseTime case wait", line_num, "time wait", 1, f.ArgsCount), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (f.HasReturnVar)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReturnVariable("Parser.ParseTime case wait", line_num, "time wait", false), Logger.MessageType.CompilationError);
                        return null;
                    }
                    var exp = new Expression(f.Args[0], ExpressionOptions.OverflowProtection);
                    if (exp.HasErrors() || exp.HasUnknownParameters(i.Variables))
                    {
                        i.ThrowError(ErrorGenerator.GenerateExpression("Parser.ParseTime case wait", line_num, f.Args[0]), Logger.MessageType.CompilationError);
                        return null;
                    }
                    return new Time(i, Time.InstructionType.wait, Argument.Expression(exp));

                default:
                    i.ThrowError(ErrorGenerator.GenerateFunctionName("Parser.ParseTime", line_num, "wait", f.Name), Logger.MessageType.CompilationError);
                    return null;
            }
        }

        private static Console ParseConsole(RawFunction f, Processor i, uint line_num)
        {
            Console.InstructionType type = f.Name.GetCIType();
            switch (type)
            {
                case Console.InstructionType.read:
                case Console.InstructionType.write:
                    if (f.HasArgs || f.HasReturnVar)
                    {
                        i.ThrowError($"Instrucion console {type}() doesn't require parameters at MSIPL.Parser.ParseConsole, line number is {line_num}", Logger.MessageType.CompilationError);
                        return null;
                    }
                    return new Console(i, type, null);

                case Console.InstructionType.pop:
                    if (f.HasArgs)
                    {
                        i.ThrowError(ErrorGenerator.GenerateArgumentCount("Parser.ParseConsole case pop", line_num, "console pop", 0, f.ArgsCount), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (!f.HasReturnVar)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReturnVariable("Parser.ParseConsole case pop", line_num, "console pop", true), Logger.MessageType.CompilationError);
                        return null;
                    }

                    string var_name = f.ReturnVar;
                    bool ch = var_name[0] == '\'' && var_name[^1] == '\'';
                    if (ch) var_name = var_name.RemoveEdges();
                    if (!i.Variables.Exists(var_name))
                    {
                        i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseConsole case pop", line_num, var_name, false), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (i.GetPointer(var_name).IsReadonly)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReadonly("Parser.ParseConsole case pop", line_num, var_name), Logger.MessageType.CompilationError);
                        return null;
                    }
                    DataType t = i.GetType(var_name);
                    if (ch && t != DataType.Int)
                    {
                        i.ThrowError(ErrorGenerator.GenerateDataType("Parser.ParseConsole case pop", line_num, t, var_name), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (ch) t = DataType.Str;
                    return new Console(i, Console.InstructionType.pop, new Argument[2] { Argument.Variable(i.GetPointer(var_name)), Argument.DataType(t) });

                case Console.InstructionType.push:
                    if (!f.HasArgs)
                    {
                        i.ThrowError($"Instrucion console push() requires arguments at MSIPL.Parser.ParseConsole case push, line number is {line_num}", Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (f.HasReturnVar)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReturnVariable("Parser.ParseConsole case push", line_num, "console push", false), Logger.MessageType.CompilationError);
                        return null;
                    }

                    var args = new Argument[f.ArgsCount];
                    for (int k = 0; k < f.Args.Length; k++)
                    {
                        Argument arg = f.Args[k].GetArgument(i);
                        if (arg == null)
                        {
                            i.ThrowError($"Push argument {f.Args[k]} is invalid at MSIPL.Parser.ParseConsole case push, line number is {line_num}", Logger.MessageType.CompilationError);
                            return null;
                        }
                        args[k] = arg;
                    }
                    return new Console(i, Console.InstructionType.push, args);

                case Console.InstructionType.clear:
                    if (f.HasArgs || f.HasReturnVar)
                    {
                        i.ThrowError($"Instrucion console clear() doesn't require parameters at MSIPL.Parser.ParseConsole, line number is {line_num}", Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (f.Name == "clear_in") return new Console(i, Console.InstructionType.clear, new Argument[1] { Argument.String("in") });
                    else if (f.Name == "clear_out") return new Console(i, Console.InstructionType.clear, new Argument[1] { Argument.String("out") });
                    else return null;

                case Console.InstructionType.can_pop:
                    if (f.ArgsCount != 1)
                    {
                        i.ThrowError(ErrorGenerator.GenerateArgumentCount("Parser.ParseConsole case can_pop", line_num, "console can_pop", 1, f.ArgsCount), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (!f.HasReturnVar)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReturnVariable("Parser.ParseConsole case can_pop", line_num, "console can_pop", true), Logger.MessageType.CompilationError);
                        return null;
                    }

                    var_name = f.Args[0];
                    ch = !string.IsNullOrEmpty(var_name) && var_name[0] == '\'' && var_name[^1] == '\'';
                    if (ch) var_name = var_name.RemoveEdges();
                    if (!i.Variables.Exists(var_name))
                    {
                        i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseConsole case can_pop", line_num, var_name, false), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (i.GetPointer(f.ReturnVar).IsReadonly)
                    {
                        i.ThrowError(ErrorGenerator.GenerateReadonly("Parser.ParseConsole case can_pop", line_num, f.ReturnVar), Logger.MessageType.CompilationError);
                        return null;
                    }
                    t = i.GetType(var_name);
                    if (ch && t != DataType.Int)
                    {
                        i.ThrowError(ErrorGenerator.GenerateDataType("Parser.ParseConsole case can_pop", line_num, t, var_name), Logger.MessageType.CompilationError);
                        return null;
                    }
                    if (ch) t = DataType.Str;
                    if (i.GetType(f.ReturnVar) != DataType.Bool)
                    {
                        i.ThrowError($"Invalid variable {f.ReturnVar} for return value at MSIPL.Parser.ParseConsole case can_, line number is {line_num}", Logger.MessageType.CompilationError);
                        return null;
                    }
                    return new Console(i, type, new Argument[2] { Argument.DataType(t), Argument.Variable(i.GetPointer(f.ReturnVar)) });

                default:
                    i.ThrowError(ErrorGenerator.GenerateFunctionName("Parser.ParseConsole", line_num, "console", f.Name), Logger.MessageType.CompilationError);
                    return null;
            }
        }

        private static Comp ParseComp(RawFunction f, Processor i, uint line_num)
        {
            string func_name = f.Name;
            if (func_name == "create")
            {
                if (f.ArgsCount != 2)
                {
                    i.ThrowError(ErrorGenerator.GenerateArgumentCount("Parser.ParseComp", line_num, func_name, 2, f.ArgsCount), Logger.MessageType.CompilationError);
                    return null;
                }
                Argument arg1 = f.Args[0].GetArgument(i);
                if (arg1.Type != ArgumentType.@string)
                {
                    i.ThrowError(ErrorGenerator.GenerateArgumentType("Parser.ParseComp", line_num, func_name, 0, ArgumentType.@string, arg1.Type), Logger.MessageType.CompilationError);
                    return null;
                }
                if (i.Variables.Exists(arg1.GetValue() as string))
                {
                    i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseComp", line_num, func_name, true), Logger.MessageType.CompilationError);
                    return null;
                }
                Argument arg2 = f.Args[1].GetArgument(i);
                if (arg2.Type != ArgumentType.@string)
                {
                    i.ThrowError(ErrorGenerator.GenerateArgumentType("Parser.ParseComp", line_num, func_name, 1, ArgumentType.@string, arg2.Type), Logger.MessageType.CompilationError);
                    return null;
                }
                if (string.IsNullOrEmpty(arg2.GetValue() as string) || !GameData.NameToComponent.ContainsKey(arg2.GetValue() as string))
                {
                    i.ThrowError($"Component {arg2.GetValue()} doesn't exist at MSIPL.Parser.ParseComp, line number is {line_num}", Logger.MessageType.CompilationError);
                    return null;
                }
                i.Variables.TryAdd(Variable.Component(arg1.GetValue() as string, Component.Create(GameData.NameToComponent[arg2.GetValue() as string])));
                return Comp.Idle;
            }
            if (f.Name.Where(ch => ch == '.').Count() != 1)
            {
                i.ThrowError($"Invalid dot symbol count at MSIPL.Parser.ParseComp, line numner is {line_num}", Logger.MessageType.CompilationError);
                return null;
            }
            string partName = func_name.BeforeSeparator('.');
            if (!i.Variables.Exists(partName))
            {
                i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseComp", line_num, func_name, false), Logger.MessageType.CompilationError);
                return null;
            }
            Variable part = i.GetPointer(partName);
            Variable ret = null;
            if (f.HasReturnVar)
            {
                if (!i.Variables.Exists(f.ReturnVar))
                {
                    i.ThrowError(ErrorGenerator.GenerateVariableExistance("Parser.ParseComp", line_num, f.ReturnVar, false), Logger.MessageType.CompilationError);
                    return null;
                }
                ret = i.GetPointer(f.ReturnVar);
                if (ret.IsReadonly)
                {
                    i.ThrowError(ErrorGenerator.GenerateReadonly("Parser.ParseComp", line_num, f.ReturnVar), Logger.MessageType.CompilationError);
                    return null;
                }
            }
            var args = new Argument[f.ArgsCount];
			func_name = func_name.AfterSeparator('.');
            for (int j = 0; j < f.ArgsCount; j++)
                args[j] = f.Args[j].GetArgument(i);
            if (part.Type == DataType.Part)
			{
				if (!GameData.TypeToPartMethods[typeof(Part)].ContainsKey(func_name))
				{
					i.ThrowError($"Part method {func_name} is not defined at MSIPL.Parser.ParseComp, line number is {line_num}", Logger.MessageType.CompilationError);
					return null;
				}
				PartMethod m = GameData.TypeToPartMethods[typeof(Part)][func_name];
				return new Comp(part, m, args, ret, i);
			}
			if (part.Type == DataType.Component)
			{
				Type t = ((Component)part.Get()).Type;
				if (!GameData.TypeToPartMethods[t].ContainsKey(func_name))
				{
					i.ThrowError($"Method {func_name} of component {t.FullName} is not defined at MSIPL.Parser.ParseComp, line number is {line_num}", Logger.MessageType.CompilationError);
					return null;
				}
				PartMethod m = GameData.TypeToPartMethods[t][func_name];
				return new Comp(part, m, args, ret, i);
			}
            i.ThrowError(ErrorGenerator.GenerateDataType("Parser.ParseComp", line_num, part.Type, partName), Logger.MessageType.CompilationError);
            return null;
        }
		#endregion
	}
}
