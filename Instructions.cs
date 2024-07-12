using NCalc;
using System;
using UnityEngine;

namespace MSIPL
{
    //Basic class for all MSIPL instructions
    public abstract class Instruction
    {
		//Processor is called "interpreter" or "i" everyvere. Don't ask why
        protected Processor _interpreter;

        public abstract InstructionType Type { get; }

		//Well, executes the instruction
		//Return 1 if you want not to increment count of instructions executed in this frame
		//Return -1 if instruction thrown an error
		//Return 0 in other case
        public abstract int Execute();

		//Pushes parameters from variable storage to expression to evaluate it
        protected void PushParameters(Expression exp) => Argument.PushParameters(exp, _interpreter);
    }

    //"set" instruction, evaluates an expression and pushes result into a variable
    public class Set : Instruction
    {
        private readonly Variable _dest;
        private readonly Argument _arg;

        public override InstructionType Type => InstructionType.set;

        public Set(Processor i, Variable v, Argument arg)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            _dest = v;
            _arg = arg;
        }

        public override int Execute()
        {
            if (_dest.Type == DataType.Part || _dest.Type == DataType.Component)
            {
                _dest.Set(_arg.GetValue());
                return 0;
            }
            object val = _arg.GetValue(_interpreter);
            _dest.Set(val);
            return 0;
        }
    }

    //"var" instruction, creates a variable and initializes it with result of some expression
    public class Var : Set
    {
        public override InstructionType Type => InstructionType.var;

        public Var(Processor i, Variable v, Argument val) : base(i, v, val)
        {
            if (!i.Variables.TryAdd(v))
            {
                i.ThrowError($"Cannot create a variable {v.Name} at MSIPL.Instruction.CreateVariable", Logger.MessageType.CompilationError);
                return;
            }
        }

        public override int Execute()
        {
            return base.Execute();
        }
    }

    //"jump" instruction, jumps to a label if condition is true. Throws an error if condition doesn't return bool
    public class Jump : Instruction
    {
        private readonly uint _line;
        private readonly Expression _exp;

        public override InstructionType Type => InstructionType.jump;

        public Jump(Processor i, uint line, Expression exp)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            _line = line;
            _exp = exp;
        }

        public override int Execute()
        {
			PushParameters(_exp);
			object cond = _exp.Evaluate();
			if (cond is not bool)
            {
                _interpreter.ThrowError($"Invalid condition at MSIPL.Jump.Execute, line number is {_interpreter.CurrentLine}");
                _interpreter.enabled = false;
                return -1;
            }
            if ((bool)cond)
                _interpreter.Jump(_line);
            return 0;
        }
    }

	//"label" instruction, just exists
	public class Label : Instruction
	{
		public override InstructionType Type => InstructionType.label;

		public override int Execute() => 1;
	}

    //"time" instruction, advanced one
	//Allows to know time since launch in seconds/frames, time between this and previous frame and wait for some frames
    public class Time : Instruction
    {
        private readonly Argument _arg;
        private readonly InstructionType _tType;

        public Time(Processor i, InstructionType t, Argument arg)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            if (t == InstructionType.none)
            {
                i.ThrowError("Invalid instruction at MSIPL.Time constructor", Logger.MessageType.CompilationError);
                return;
            }

            _tType = t;
            if (arg == null)
                throw new Exception("Argument is null");
            _arg = arg;
        }

        public override MSIPL.InstructionType Type => MSIPL.InstructionType.time;

        public override int Execute()
        {
            switch (_tType)
            {
                case InstructionType.frames_since_launch:
                    ((Variable)_arg.GetValue()).Set(_interpreter.FramesSinceLaunch);
                    break;

                case InstructionType.seconds_since_launch:
                    ((Variable)_arg.GetValue()).Set(_interpreter.SecondsSinceLaunch);
                    break;

                case InstructionType.delta_time:
                    ((Variable)_arg.GetValue()).Set((double)UnityEngine.Time.deltaTime);
                    break;

                case InstructionType.wait:
                    object t = _arg.GetValue(_interpreter);
					if ((t is not int i || i < 0) && (t is not long l || l < 0))
					{
						_interpreter.ThrowError($"Invalid delay {t} with type {t.GetType()} at MSIPL.Wait.Execute, line number is {_interpreter.CurrentLine}");
						return -1;
					}
                    _interpreter.Wait((uint)(t is int ? (int)t : (int)(long)t));
                    break;

                default:
                    break;
            }
            return 0;
        }

        public enum InstructionType
        {
            none,
            frames_since_launch,
            seconds_since_launch,
            delta_time,
            wait
        }
    }

    //"console" instruction, advanced one
	//Allows to read/write messages to chat (if it is implemented)
    public class Console : Instruction
    {
        private readonly InstructionType _cType;
        private readonly Argument[] _args;

        public Console(Processor i, InstructionType t, Argument[] args)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            if (t == InstructionType.none)
            {
                i.ThrowError("Invalid instruction at MSIPL.Console constructor", Logger.MessageType.CompilationError);
                return;
            }
            _cType = t;
            if (args == null && t != InstructionType.read && t != InstructionType.write)
            {
                i.ThrowError("Args are null", Logger.MessageType.CompilationError);
                return;
            }
            _args = args;
        }

        public override MSIPL.InstructionType Type => MSIPL.InstructionType.console;

        public override int Execute()
        {
            switch (_cType)
            {
                case InstructionType.read:
                    _interpreter.Read();
                    break;

                case InstructionType.write:
                    _interpreter.Write();
                    break;

                case InstructionType.pop:
                    if (_interpreter.InputStream.Length == 0)
                    {
                        _interpreter.ThrowError("Input stream is empty at MSIPL.Console.Execute case pop");
                        _interpreter.enabled = false;
                        return -1;
                    }
                    if ((DataType)_args[1].GetValue() == DataType.Str)
                    {
                        ((Variable)_args[0].GetValue()).Set((long)_interpreter.InputStream[0]);
                        _interpreter.CutInput(0, 1);
                        return -1;
                    }
                    string val = FindNumber(_interpreter.InputStream,
                        (DataType)_args[1].GetValue() == DataType.Float, out int start, out int end);
                    if (val == "")
                    {
                        _interpreter.ThrowError("Can't read a number at MSIPL.Console.Execute case pop");
                        _interpreter.enabled = false;
                        return -1;
                    }
                    var d = double.Parse(val);
                    ((Variable)_args[0].GetValue()).Set(TypeSystem.ConvertValue(d, (DataType)_args[1].GetValue()));
                    _interpreter.CutInput(0, end);
                    break;

                case InstructionType.push:
                    for (int i = 0; i < _args.Length; i++)
                    {
                        if (_args[i].Type == ArgumentType.@string || _args[i].Type == ArgumentType.char_variable)
                            _interpreter.AddToOutput(_args[i].GetValue());
                        else if (_args[i].Type == ArgumentType.expression)
                        {
                            var result = (double)TypeSystem.ConvertValue(_args[i].GetValue(_interpreter), DataType.Float);
                            _interpreter.AddToOutput(result % 1 == 0 ? (long)result : result);
                        }
                    }
                    break;

                case InstructionType.clear:
                    _interpreter.ClearStream((string)_args[0].GetValue());
                    break;

                case InstructionType.can_pop:
                    if (_args[0].Type == ArgumentType.char_variable)
                    {
                        ((Variable)_args[1].GetValue()).Set(!string.IsNullOrEmpty(_interpreter.InputStream));
                        break;
                    }
                    val = FindNumber(_interpreter.InputStream,
                        (DataType)_args[0].GetValue() == DataType.Float, out start, out end);
                    ((Variable)_args[1].GetValue()).Set(!string.IsNullOrEmpty(val));
                    break;

                case InstructionType.filter:
                    throw new NotImplementedException();

                default:
                    break;
            }
            return 0;
        }

        private string FindNumber(string ins, bool isFloat, out int start, out int end)
        {
            bool comma = isFloat;
            string val = "";
            start = -1; end = -1;

            for (int i = 0; i < ins.Length; i++)
            {
                if ('0' <= ins[i] && ins[i] <= '9')
                {
                    start = i;
                    break;
                }
            }
            if (start == -1) return "";

            for (int i = start; i < ins.Length; i++)
            {
                if ('0' <= ins[i] && ins[i] <= '9')
                    val += ins[i];
                else if (ins[i] == '.')
                {
                    if (!comma)
                    {
                        end = i;
                        break;
                    }
                    val += ".";
                    comma = false;
                }
                else
                {
                    end = i;
                    break;
                }
            }
            if (end == -1) end = ins.Length;
            return val;
        }

        public enum InstructionType
        {
            none,
            read, write,
            pop, push,
            can_pop,
            clear,
            filter
        }
    }

    //"comp" instruction, advanced one
	//Allows to control parts ant their components and get information from them with methods and properties
	//Most dificult to implement in fact
    public class Comp : Instruction
    {
        private readonly Variable _part;
        private readonly PartMethod _method;
        private readonly Argument[] _args;
        private readonly Variable _returnVar;

        public Comp(Variable part, PartMethod method, Argument[] args, Variable returnVar, Processor i)
        {
			_part = part;
            _method = method;
			_args = args;
            _returnVar = returnVar;
			_interpreter = i;
        }

        public static Comp Idle => new Comp(null, null, null, null, null);

        public override InstructionType Type => InstructionType.comp;

        public override int Execute()
        {
            if (_method == null) return 1;
            if (_part.IsNull)
            {
                _part.Clear(false);
                _interpreter.ThrowError("Part is null at MSIPL.Comp.Execute");
                return -1;
            }
            if (!ShipChecker.CanConnect(_interpreter.GetShip(), _part.GetShip()))
            {
                _interpreter.ThrowError("Unable to connect to this part at MSIPL.Comp.Execute");
                return -1;
            }
            if (_returnVar == null)
            {
                _method.Invoke(_part, _args, _interpreter);
                return 0;
            }
            object val = _method.Invoke(_part, _args, _interpreter);
            if (val == null)
            {
                _interpreter.ThrowError("Part method returned null at MSIPL.Comp.Execute");
                return -1;
            }
            _returnVar.Set(val);
            return 0;
        }
    }

	//Represents type of instruction as enum, useful in parsing
    public enum InstructionType
    {
        none,
        var, set,
        jump, label,
        time, console,
        comp
    }
}
