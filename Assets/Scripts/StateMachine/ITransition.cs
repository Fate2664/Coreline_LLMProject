using UnityEngine;

namespace Coreline
{
    //This is the transition interface. All transitions should have a state to transition to and a predicate condition that needs to be met
    public interface ITransition
    {
        IState To { get; }
        IPredicate Condition { get; }
    }
}
