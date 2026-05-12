using System;
using UnityEngine;

namespace Coreline
{
    //This func predicate class is used to convert a boolean condition into a predicate that the state machine can understand. 
    public class FuncPredicate : IPredicate
    {
        private readonly Func<bool> func;

        public FuncPredicate(Func<bool> func)
        {
            this.func = func;
        }

        public bool Evaluate() => func.Invoke();
    }
}
