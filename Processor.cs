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
		[SerializeField]
		private string _script =
			"comp create(\"m\", \"memory\")\n" +//0
			"comp this.get_component(m, 0)\n" +//1
			"comp create(\"proc\", \"processor\")\n" +//2
			"comp this.get_component(proc, 0)\n" +//3
			"var float arg 0.0\n" +//4
			"var float exp 0.0\n" +//5
			"var float p 1\n" +//6
			"var int adr 0\n" +//7
			"var int i 0\n" +//8
			"var int f 1\n" +//9
			"var bool nl false\n" +//10
			"label main\n" +//11
			"jump calc_exp true\n" +//12
			"comp m.set_value(adr, exp)\n" +//13
			"set adr adr+1\n" +//14
			"set arg arg+0.001\n" +//15
			"set i 0\n" +//16
			"set f 1\n" +//17
			"set p 1\n" +//18
			"set exp 0\n" +//19
			"jump main arg<1\n" +//20
			"set adr 0\n" +//21
			"jump print true\n" +//22
			"label calc_exp\n" +//23
			"set exp exp+p/f\n" +//24
			"set i i+1\n" +//25
			"set f f*i\n" +//26
			"set p p*arg\n" +//27
			"jump calc_exp i<=10\n" +//28
			"jump main true\n" +//29
			"label print\n" +//30
			"comp m.is_null(adr) nl\n" +//31
			"jump end nl\n" +//32
			"comp m.get_value(adr) exp\n" +//33
			"console push(\"Exponent at \", adr, \" is \", exp)\n" +//34
			"console write()\n" +//35
			"console clear_out()\n" +//36
			"set adr adr+1\n" +//37
			"jump print true\n" +//38
			"comp proc.set(\"enabled\", false)\n" +//39
			"label end";//40

        private bool _thrownError = false;
        private uint _currentLine = 0;
        private uint _currentDelay = 0;
        private int _linesLeft = 0;

        private double _secondsOnLaunch;
        public double SecondsSinceLaunch => UnityEngine.Time.realtimeSinceStartup - _secondsOnLaunch;
        public override string Name => "processor";
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
            if ((bool)Properties["completed"].Value || !enabled || _thrownError) return;
            if (_currentDelay > 0)
                _currentDelay--;
            else
            {
                Execute();
                Logger.Log();
            }
        }

        protected override void GetProperties(out Dictionary<string, PartProperty> prop)
        {
            base.GetProperties(out prop);
            prop.Add("enabled", PartProperty.ControlFlag(true));
            prop.Add("completed", PartProperty.ROFlag(false));
        }

        public void Execute()
        {
            _linesLeft = (ParentConfig as ModuleProcessor).LinesInFrame;

            while (!(bool)Properties["completed"].Value && _linesLeft > 0 && _currentDelay == 0 && enabled && !_thrownError)
            {
				Debug.Log($"line {CurrentLine} variables {Variables}");
                int retCode = ExecuteCurrentLine();
                _currentLine++;
				if (_currentLine >= Script.Length)
				{
					Properties["completed"].Value = true;
					break;
				}
				if (retCode == -1) break;
                if (retCode != 1) _linesLeft--;
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
            Variables.Exists(name) ? Variables.GetPointer(name).Type : DataType.Void;

        public Variable GetPointer(string name) =>
            Variables.GetPointer(name);

        public void SetValue(string name, object value) =>
            Variables.SetValue(name, value);

        public void Jump(uint pointer)
        {
            if (pointer < 0)
                ThrowError("Invalid line for jump at MSIPL.Processor.Jump");
            else _currentLine = pointer;
        }

        private int ExecuteLine(uint pointer)
        {
			if (pointer < 0 || pointer > Script.Length)
			{
				ThrowError("Invalid line index for execution at MSIPL.Processor.ExecuteLine");
				return -1;
			}
			return Script[pointer].Execute();
        }

        public int ExecuteCurrentLine() => ExecuteLine(_currentLine);

        public void Wait(uint t) => _currentDelay += t;

        public void Read() => throw new NotImplementedException();
        public void Write() => Debug.Log(TryTellDev(_outputStream, out string a) ?
			$"[MSIPL CREATOR]: You told me \"{_outputStream.AfterSeparator(' ')}\" and i tell you \"{a}\""
			: "[PROCESSOR MESSAGE]: " + _outputStream);
		private bool TryTellDev(string q, out string a)
		{
			a = "";
			if (string.IsNullOrEmpty(q))
				return false;
            if (q.StartsWith("@max_sqrt7 "))
            {
				a = q.AfterSeparator(' ').ToUpper();
				if (a == "IDDQD?")
					a = "No iddqd";
				else if (a == "GREAT JOB!")
					a = "Thanks!";
				else if (a == "HOW IS IT GOING?")
					a = "Great!";
				else
					a = "No easter eggs this time";
				return true;
            }
            return false;
		}
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
            //{ "cockpit", new Vector2Int(0, 1) }
        };
    }
}