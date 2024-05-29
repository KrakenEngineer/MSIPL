using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSIPL
{
    //Contains variables, script and program counter and gives (un)safe access to them
    [Serializable]
    public sealed class Processor : PartComponentBase
    {
        [SerializeField] private string _inputStream = "";
        [SerializeField] private string _outputStream = "";
        [SerializeField] private string _script =
            "var int fact 1\n" +
            "var int n 0\n" +
            "var int {count} 6\n" +
            "label start\n" +
            "set n n+1\n" +
            "set fact fact*n\n" +
            "memory mem.set(n, fact)\n" +
            "jump start n<count\n" +
            "set n 0\n" +
            "label start1\n" +
            "set n n+1\n" +
            "memory mem.get(n) fact\n" +
            "console push(fact)\n" +
            "console write()\n" +
            "console clear_out()\n" +
            "jump start1 n<count\n"+
            "stop";

        private bool _thrownError = false;
        private uint _currentLine = 0;
        private uint _currentDelay = 0;
        private uint _linesLeft = 0;

        private double _secondsOnLaunch;
        public double SecondsSinceLaunch => UnityEngine.Time.realtimeSinceStartup - _secondsOnLaunch;
        public long FramesSinceLaunch { get; private set; }

        public Instruction[] Script { get; private set; }
        public VariableStorage Variables { get; private set; }
        public int ErrorsCount { get; private set; }

        public override void OnPartCreated()
        {
            base.OnPartCreated();
            if (PartParent.Mode != Part.PartMode.gameplay)
                return;
            Variables = new VariableStorage();
        }

        public override void LateOnPartCreated()
        {
            if (PartParent.Mode != Part.PartMode.gameplay)
                return;

            Script = Parser.ParseScript(_script, this);
            FramesSinceLaunch = 0;
            Logger.Log();
        }

        public override void OnPartGameplayUpdate()
        {
            FramesSinceLaunch++;
            if (!enabled || _thrownError) return;
            if (_currentDelay > 0)
                _currentDelay--;
            else
            {
                Execute();
                Logger.Log();
            }
        }

        public void Execute()
        {
            _linesLeft = (uint)(ParentConfig as ModuleProcessor).LinesInFrame;

            while (_linesLeft > 0 && _currentDelay == 0 && enabled && !_thrownError)
            {
                Script[_currentLine].Execute();
                _currentLine++;
                _linesLeft--;
                if (_currentLine >= Script.Length)
                    _currentLine = (uint)(_currentLine % Script.Length);
            }
        }

        public bool TryAddLabel(uint i, string name) =>
            Variables.TryAdd(Variable.Label(name, i));

        public bool TryConnectParts(Ship s)
        {
            PartParent.TryAddLogic();
            PartParent.Logic.TryConnect(this, "this");
            foreach (var pos in ((ModuleProcessor)ParentConfig).PartPositions)
            {
                if (pos.Key == "this")
                {
                    ThrowError("Name \"this\" is reserved at MSIPL.Processor.TryConnectParts", Logger.MessageType.CompilationError);
                    return false;
                }

                GameplayPart part = s.ShipParts.Where(p => p.position == pos.Value).FirstOrDefault();
                if (part == null)
                {
                    ThrowError($"Part at position {pos.Value} is missing at MSIPL.Processor.TryConnectParts", Logger.MessageType.CompilationError);
                    return false;
                }

                part.TryAddLogic();
                if (!part.Logic.TryConnect(this, pos.Key))
                {
                    ThrowError($"Cannot connect a part {pos.Key} at MSIPL.Processor.TryConnectParts", Logger.MessageType.CompilationError);
                    part.TryRemoveLogic();
                    return false;
                }
            }
            return true;
        }

        public void ThrowError(string message, Logger.MessageType t = Logger.MessageType.RuntimeError)
        {
            Logger.AddMessage(message, t);
            ErrorsCount++;
            _thrownError = true;
        }

        public object GetValue(string name) => Variables.GetValue(name);
        public DataType GetType(string name) =>
            Variables.Exists(name) ? Variables.GetPointer(name).Type : DataType.None;

        public Variable GetPointer(string name) =>
            Variables.GetPointer(name);

        public void SetValue(string name, object value) =>
            Variables.SetValue(name, value);

        public void Jump(uint pointer)
        {
            if (pointer < 0 || pointer >= Script.Length)
                ThrowError("Invalid line for jump at MSIPL.Interpreter.Jump");
            else _currentLine = pointer;
        }

        private void ExecuteLine(uint pointer)
        {
            if (pointer < 0 || pointer > Script.Length)
                ThrowError("Invalid line index for execution at MSIPL.Interpreter.ExecuteLine");
            else Script[pointer].Execute();
        }

        public void ExecuteCurrentLine() => ExecuteLine(_currentLine);

        public void Wait(uint t) => _currentDelay += t;

        public void Read() => throw new NotImplementedException();
        public void Write() => Debug.Log("[PROCESSOR MESSAGE]: " + _outputStream);
        public void CutInput(int start, int end)
        {
            string s = "";
            for (int i = 0; i < start; i++)
                s += _inputStream[i];
            for (int i = end; i < _inputStream.Length; i++)
                s += _inputStream[i];
            _inputStream = s;
        }
        public void AddToOutput(object val) => _outputStream += val;
        public void ClearStream(string stream)
        {
            switch (stream)
            {
                case "in":
                    _inputStream = "";
                    break;
                case "out":
                    _outputStream = "";
                    break;
                default:
                    ThrowError("Invalid stream name at MSIPL.ProcessorTemp.ClearStream");
                    break;
            }
        }

        public uint CurrentLine => _currentLine;
        public string InputStream => _inputStream;
        public string OutputStream => _outputStream;
    }

    [Serializable]
    public sealed class ModuleProcessor : ModuleBase
    {
        public int LinesInFrame;
        public Dictionary<string, Vector2Int> PartPositions = new Dictionary<string, Vector2Int>()
        {
            { "mem", new Vector2Int(0, 1) }
        };
    }
}