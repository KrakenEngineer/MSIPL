using NCalc;
using NCalc.Domain;
using System;

namespace MSIPL
{
    //Basic class for all MSIPL instructions
    public abstract class Instruction
    {
        protected Processor _interpreter;

        public abstract InstructionType Type { get; }

        public abstract void Execute();

        protected void PushParameters(Expression exp)
        {
            string[] pars = exp.GetParametersNames();
            for (int i = 0; i < pars.Length; i++)
            {
                Variable v = _interpreter.GetPointer(pars[i]);
                if (v.IsNull)
                {
                    _interpreter.ThrowError($"Parameter {v.Name} is null at {GetType().FullName}.PushParameters");
                    return;
                }
                if (v.Type == DataType.Part)
                    exp.Parameters[pars[i]] = (v.Get() as LogicPart).PartParent.InstanceID;
                else
                    exp.Parameters[pars[i]] = v.Get();
            }
        }
    }

    //"set" instruction
    public class Set : Instruction
    {
        private readonly Variable _dest;
        private readonly object _val;

        public override InstructionType Type => InstructionType.set;

        public Set(Processor i, Variable v, object val)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            _dest = v;
            _val = val;
        }

        public override void Execute()
        {
            if (_dest.Type == DataType.Part)
            {
                _dest.Set((_val as Variable).Get());
                return;
            }
            PushParameters(_val as Expression);
            object val = TypeSystem.ConvertValue((_val as Expression).Evaluate(), _dest.Type);
            _dest.Set(val);
        }
    }

    //"var" instruction
    public class Var : Set
    {
        private bool _done = false;

        public override InstructionType Type => InstructionType.var;

        public Var(Processor i, Variable v, object val) : base(i, v, val)
        {
            if (!i.Variables.TryAdd(v))
            {
                i.ThrowError($"Cannot create a variable {v.Name} at MSIPL.Instruction.CreateVariable", Logger.MessageType.CompilationError);
                return;
            }
        }

        public override void Execute()
        {
            if (_done) return;
            base.Execute();
            _done = true;
        }
    }

    //"jump" instruction
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

        public override void Execute()
        {
            PushParameters(_exp);
            object cond = _exp.Evaluate();
            if (!(cond is bool))
            {
                _interpreter.ThrowError($"Invalid condition at MSIPL.Jump.Execute, line number is {_interpreter.CurrentLine}");
                _interpreter.enabled = false;
                return;
            }
            if ((bool)cond)
            {
                _interpreter.Jump(_line);
                _interpreter.ExecuteCurrentLine();
            }
        }
    }

    //"stop" instruction
    public class Stop : Instruction
    {
        public Stop(Processor i)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
        }

        public override InstructionType Type => InstructionType.stop;

        public override void Execute()
        {
            _interpreter.enabled = false;
        }
    }

    //all "time" instructions
    public class Time : Instruction
    {
        private object _arg;
        private InstructionType _tType;

        public Time(Processor i, InstructionType t, object arg)
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

        public override void Execute()
        {
            switch (_tType)
            {
                case InstructionType.frames_since_launch:
                    ((Variable)_arg).Set(TypeSystem.ConvertValue(_interpreter.FramesSinceLaunch, ((Variable)_arg).Type));
                    break;

                case InstructionType.seconds_since_launch:
                    ((Variable)_arg).Set(TypeSystem.ConvertValue(_interpreter.SecondsSinceLaunch, ((Variable)_arg).Type));
                    break;

                case InstructionType.delta_time:
                    ((Variable)_arg).Set(TypeSystem.ConvertValue(UnityEngine.Time.deltaTime, ((Variable)_arg).Type));
                    break;

                case InstructionType.wait:
                    PushParameters((Expression)_arg);
                    long t = (long)TypeSystem.ConvertValue(((Expression)_arg).Evaluate(), DataType.Int);
                    if (t <= 0) return;
                    _interpreter.Wait((uint)t);
                    break;

                default:
                    break;
            }
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

    //all "console" instructions
    public class Console : Instruction
    {
        private readonly InstructionType _cType;
        private readonly object[] _args;

        public Console(Processor i, InstructionType t, object[] args)
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

        public override void Execute()
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
                        return;
                    }
                    if ((DataType)_args[1] == DataType.Char)
                    {
                        ((Variable)_args[0]).Set((long)_interpreter.InputStream[0]);
                        _interpreter.CutInput(0, 1);
                        return;
                    }
                    string val = FindNumber(_interpreter.InputStream,
                        (DataType)_args[1] == DataType.Float, out int start, out int end);
                    if (val == "")
                    {
                        _interpreter.ThrowError("Can't read a number at MSIPL.Console.Execute case pop");
                        _interpreter.enabled = false;
                        return;
                    }
                    var d = double.Parse(val);
                    ((Variable)_args[0]).Set(TypeSystem.ConvertValue(d, (DataType)_args[1]));
                    _interpreter.CutInput(0, end);
                    break;

                case InstructionType.push:
                    for (int i = 0; i < _args.Length; i++)
                    {
                        if (_args[i] is string)
                            _interpreter.AddToOutput(_args[i]);
                        else if (_args[i] is Variable)
                            _interpreter.AddToOutput((char)(long)((Variable)_args[i]).Get());
                        else if (_args[i] is Expression)
                        {
                            PushParameters((Expression)_args[i]);
                            object result = ((Expression)_args[i]).Evaluate();
                            if (result is bool) result = (bool)result ? 1 : 0;
                            _interpreter.AddToOutput(result);
                        }
                    }
                    break;

                case InstructionType.clear:
                    _interpreter.ClearStream((string)_args[0]);
                    break;

                case InstructionType.can_pop:
                    if ((DataType)_args[0] == DataType.Char)
                    {
                        ((Variable)_args[1]).Set(!string.IsNullOrEmpty(_interpreter.InputStream));
                        break;
                    }
                    val = FindNumber(_interpreter.InputStream,
                        (DataType)_args[0] == DataType.Float, out start, out end);
                    ((Variable)_args[1]).Set(!string.IsNullOrEmpty(val));
                    break;

                case InstructionType.filter:
                    throw new NotImplementedException();

                default:
                    break;
            }
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

    //all "memory" instructions
    public class Memory : Instruction
    {
        private InstructionType _mType;
        private LogicPart _mem;
        private Expression _adr;
        private object _val;

        public Memory(Processor i, InstructionType t, LogicPart mem, Expression adr, object v)
        {
            if (i == null)
                throw new Exception("Interpreter is null");
            _interpreter = i;
            if (t == InstructionType.none)
            {
                i.ThrowError("Invalid instruction at MSIPL.Memory constructor", Logger.MessageType.CompilationError);
                return;
            }
            _mType = t;
            if (mem == null)
                throw new Exception("Memory cell is null");
            _mem = mem;
            if (adr == null)
                throw new Exception("Address expression is null");
            _adr = adr;
            if (v == null && t != InstructionType.clear)
                throw new Exception("Value/variable is null");
            _val = v;
        }

        public override MSIPL.InstructionType Type => MSIPL.InstructionType.memory;

        public override void Execute()
        {
            if (_mem == null || _mem.PartParent == null)
            {
                _mem = null;
                _interpreter.ThrowError("Memory is null at MSIPL.Memory.Execute");
                return;
            }
            if (!_mem.PartParent.TryGetComponent(out MemoryCell mem))
            {
                _interpreter.ThrowError("This part is not a memory cell at MSIPL.Memory.Execute");
                return;
            }

            PushParameters(_adr);
            long adr = Convert.ToInt64(_adr.Evaluate());
            switch (_mType)
            {
                case InstructionType.get:
                    mem.Get(adr, (Variable)_val);
                    break;

                case InstructionType.set:
                    PushParameters((Expression)_val);
                    mem.Set(adr, ((Expression)_val).Evaluate());
                    break;

                case InstructionType.get_type:
                    mem.GetType(adr, (Variable)_val);
                    break;

                case InstructionType.clear:
                    
                    mem.Clear(adr);
                    break;

                default:
                    break;
            }
        }

        public enum InstructionType
        {
            none,
            get,
            set,
            clear,
            get_type
        }
    }

    public enum InstructionType
    {
        none,
        var, set,
        jump,
        time,
        console,
        memory,
        stop
    }
}
