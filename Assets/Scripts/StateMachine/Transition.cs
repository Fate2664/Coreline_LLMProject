using UnityEngine;

namespace Coreline
{
    //This is the concrete tranistion script that uses the ITransition interface
    public class Transition : ITransition
    {
        public IState To { get; }
        public IPredicate Condition { get; }

        public Transition(IState to, IPredicate condition)
        {
            To = to;
            Condition = condition;
        }
    }
}
