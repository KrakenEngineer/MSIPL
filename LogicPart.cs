using MSIPL;

public class LogicPart : PartComponentBase
{
    public int Usings { get; private set; }

    public override void OnPartCreated()
    {
        base.OnPartCreated();
        Usings = 0;
        GarbageCollector.TryAdd(this);
    }

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