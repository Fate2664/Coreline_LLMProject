using UnityEngine.Events;

public interface IMineable
{
    bool Mine(MiningHit hit);
    public event UnityAction OnDepleted;
}
