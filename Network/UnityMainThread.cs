using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThread : MonoBehaviour
{
    internal static UnityMainThread wkr;
    Queue<Action> jobs = new Queue<Action>();

    void Awake() 
    {
        if (wkr == null) wkr = this;
        else enabled = false;
    }

    void Update() 
    {
        while (jobs.Count > 0) 
            lock(jobs) jobs.Dequeue().Invoke();
    }

    internal void AddJob(Action newJob) {
        lock(jobs) jobs.Enqueue(newJob);
    }
}