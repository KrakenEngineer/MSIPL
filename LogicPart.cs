using MSIPL;
using System.Collections.Generic;

//Allows to store part in MSIPL variable without direct reference
//So garbage collector (real, not MSIPL one) can collect it if destroyed
public class LogicPart : PartComponentBase
{
    public int Usings { get; private set; }

    public override void OnPartCreated()
    {
        base.OnPartCreated();
        Usings = 0;
        GarbageCollector.TryAdd(this);
    }

    protected override void GetProperties(out Dictionary<string, PartProperty> prop) { prop = null; }

    public bool TryConnect(Processor i, string name)
    {
        if (!i.Variables.TryAdd(Variable.Create(name, this, true)))
            return false;

        Usings++;
        return true;
    }

    public void AddUsing() => Usings++;

    public void RemoveUsing()
    {
        Usings--;
        if (Usings == 0)
            PartParent.TryRemoveLogic();
    }
}