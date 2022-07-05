using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExperimentControl;
using Simulation;
using UnityEngine;
using Random = UnityEngine.Random;

public class RunExperiment : MonoBehaviour
{
    private List<List<Tuple<EyeTracking.EyeTrackingConditions, HallwayCreator.Hallways>>> _blocks = new ()
    {
        new()
        {
            new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway1),
            new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway2),
            new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway3)
        },
        new()
        {
            new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway2),
            new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway3),
            new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway1)
        },
        new()
        {
            new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway3),
            new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway1),
            new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway2)
        }
    };
    private bool trialCompleted = false;
    
    public void StartExperiment()
    {
        // Shuffle hallway-condition matrix
        _blocks = _blocks.OrderBy(_ => Random.value).ToList();
        _blocks[0] = _blocks[0].OrderBy(_ => Random.value).ToList();
        _blocks[1] = _blocks[1].OrderBy(_ => Random.value).ToList();
        _blocks[2] = _blocks[2].OrderBy(_ => Random.value).ToList();
        // setup hooks
        StartCoroutine(StartNewBlock(0));
        // create folders and files
        // ?
    }

    private IEnumerator StartNewBlock(int blockIdx)
    {
        var trials = _blocks[blockIdx];
        foreach (var tuple in trials)
        {
            var condition = tuple.Item1;
            var hallway = tuple.Item2;
            trialCompleted = false;
            StartCoroutine(StartNewTrial(condition, hallway));
            yield return new WaitUntil(() => trialCompleted);
        }
    }

    private IEnumerator StartNewTrial(EyeTracking.EyeTrackingConditions condition, HallwayCreator.Hallways hallway)
    {
        SenorSummarySingletons.GetInstance<InputHandler>().MoveToNewHallway(HallwayCreator.HallwayObjects[hallway]);
        SenorSummarySingletons.GetInstance<PhospheneSimulator>().SetGazeTrackingCondition(condition);
        while (!trialCompleted)
        {
            CheckTrialCompleted();
            yield return null;
        }
    }

    private void CheckTrialCompleted()
    {
        throw new NotImplementedException();
    }
}
