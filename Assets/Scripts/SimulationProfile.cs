using UnityEngine;

[CreateAssetMenu(fileName = "SimulationProfile", menuName = "Simulation Profile")]
public class SimulationProfile : ScriptableObject
{
     public int numStepsPerFrame;
     public int agentCount;
     public float evaporationSpeed;
     public float diffusionSpeed;
     public SpawnMode spawnMode;
     public Vector4 color;
     public SimulationManager.Specie[] species;
}
