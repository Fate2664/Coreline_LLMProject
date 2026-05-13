using System;
using System.Collections.Generic;
using UnityEngine;

namespace Coreline
{
    //This is the main state machine class
    public class StateMachine
    {
        private StateNode current;
        public IState CurrentState => current?.State;
        
        //Boolean to check if current state is and instance of type T where T is of IState
        public bool IsInState<T>() where T : IState => current?.State is T;
        
        private Dictionary<Type, StateNode> nodes = new();
        private HashSet<ITransition> anyTransitions = new();

        public void Update()
        {
            //Check if there are any transitions to change to
            var transition = GetTransition();
            if (transition != null)
                ChangeState(transition.To);
            
            //Update state if anything changed
            current.State?.Update();
        }

        public void FixedUpdate()
        {
            current.State?.FixedUpdate();
        }
        
        //This method forces the state machine into a specific state
        public void SetState(IState state)
        {
            //Get the state node from the state given
            current = nodes[state.GetType()];
            current.State?.OnEnter();
        }
        
        //The method handles switching from current state to a new one
        private void ChangeState(IState state)
        {
            if (state == current.State) return;
            
            var previousState = current.State;
            var nextState = nodes[state.GetType()].State;
            
            previousState?.OnExit();
            nextState?.OnEnter();
            //Set current state node to the state given's state node
            current = nodes[state.GetType()];
        }
        
        //This method searches for a transition where the condition is true
        private ITransition GetTransition()
        {
            foreach (var transition in anyTransitions)
                if  (transition.Condition.Evaluate())
                    return transition;
            
            foreach (var transition in current.Transitions)
                if (transition.Condition.Evaluate())
                    return transition;
            
            return null;
        }
        
        //This method adds a transition from one state to another with a condition
        public void AddTransition(IState from, IState to, IPredicate condition)
        {
            GetOrAddNode(from).AddTransition(GetOrAddNode(to).State, condition);
        }
        
        //This method adds a global transition to another state with a condition
        public void AddAnyTransition(IState to, IPredicate condition)
        {
            anyTransitions.Add(new Transition(GetOrAddNode(to).State, condition));
        }
        
        //This method looks to return a state node or add one if there is not one
        private StateNode GetOrAddNode(IState state)
        {
            var node = nodes.GetValueOrDefault(state.GetType());

            if (node == null)
            {
                node = new StateNode(state);
                nodes.Add(state.GetType(), node);
            }
            
            return node;
        }
        
        //This class represents a single state in the state machine
        class StateNode
        {
            public IState State { get; }
            public HashSet<ITransition> Transitions { get; }

            public StateNode(IState state)
            {
                State = state;
                Transitions = new HashSet<ITransition>();
            }

            public void AddTransition(IState to, IPredicate condition)
            {
                Transitions.Add(new Transition(to, condition));
            }
            
        }
    }
}
