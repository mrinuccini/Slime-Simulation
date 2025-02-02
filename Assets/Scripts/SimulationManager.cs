using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public enum SpawnMode
{
    Random = 0,
    InwardCircle,
    OutwardCircle,
    RandomInCircle
}

// ReSharper disable SpecifyACultureInStringConversionExplicitly
public class SimulationManager : MonoBehaviour
{
    private static readonly int TrailMap = Shader.PropertyToID("TrailMap");
    private static readonly int ColorMap = Shader.PropertyToID("ColorMap");
    private static readonly int Agents = Shader.PropertyToID("Agents");
    private static readonly int Resolution1 = Shader.PropertyToID("resolution");
    private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
    private static readonly int EvaporationSpeed = Shader.PropertyToID("evaporationSpeed");
    private static readonly int DiffusionSpeed = Shader.PropertyToID("diffusionSpeed");
    private static readonly int ElapsedTime = Shader.PropertyToID("elapsedTime");
    private static readonly int Color1 = Shader.PropertyToID("color");
    private static readonly int Species = Shader.PropertyToID("Species");

    [SerializeField] private SimulationProfile[] profiles;
    [SerializeField] private ComputeShader simulationShader;
    [SerializeField] private RenderTexture trailMap;
    [SerializeField] private RenderTexture colorMap;

    [SerializeField] private SimulationProfile profile;

    [SerializeField] private Vector2Int simulationResolution = new(320, 240);
    
    private Agent[] m_agents;
    private bool m_running;
    private bool m_showUi = true;

    private void Start()
    {
        Vector2 center = new Vector2((float)simulationResolution.x / 2, (float)simulationResolution.y / 2);
        m_agents = new Agent[profile.agentCount];
        
        for (int i = 0; i < profile.agentCount; i++)
        {
            Vector2 pos = new();
            float angle = 0f;
            
            switch (profile.spawnMode)
            {
                case SpawnMode.Random:
                    pos = new Vector2(Random.Range(0, simulationResolution.x), Random.Range(0, simulationResolution.y));
                    var direction = new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f));
                    angle = Mathf.Atan2(direction.y, direction.x);
                    break;
                case SpawnMode.InwardCircle:
                    pos = center + Random.insideUnitCircle * (simulationResolution.y * 0.5f);
                    angle = Mathf.Atan2((center - pos).normalized.y, (center - pos).normalized.x);
                    break;
                case SpawnMode.OutwardCircle:
                    pos = new Vector2(simulationResolution.x / 2f, simulationResolution.y / 2f);
                    angle = Random.Range(0f, Mathf.PI * 2f);
                    break;
                case SpawnMode.RandomInCircle:
                    pos = Random.insideUnitCircle * (simulationResolution.y * 0.4f) + (Vector2)simulationResolution / 2f; 
                    angle = Random.Range(0f, Mathf.PI * 2f);
                    break;
            }
            
            m_agents[i] = new Agent(pos, angle, Random.Range(0, profile.species.Length));
        }
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            m_showUi = !m_showUi;
        
        if (!m_running) return;
        
        Debug.Log($"Agent off screen : {m_agents.Count(x => x.position.x < 0 || x.position.x > simulationResolution.x || x.position.y < 0 || x.position.y > simulationResolution.y)}");
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Simulation(destination);
    }

    private void Simulation(RenderTexture destination)
    {
        if (!m_running) return;
        
        if (trailMap == null)
        {
            trailMap = new RenderTexture(simulationResolution.x, simulationResolution.y, 0, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true,
            };

            trailMap.Create();
        }
        
        if (colorMap == null)
        {
            colorMap = new RenderTexture(simulationResolution.x, simulationResolution.y, 0)
            {
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };

            colorMap.Create();
        }

        for (int i = 0; i < profile.numStepsPerFrame; i++)
        {
            ComputeBuffer agentBuffer = new ComputeBuffer(profile.agentCount, sizeof(float) * 3 + sizeof(int));
            agentBuffer.SetData(m_agents);

            ComputeBuffer speciesBuffer = new ComputeBuffer(profile.species.Length, sizeof(float) * 9 + sizeof(int) * 3);
            speciesBuffer.SetData(profile.species);

            simulationShader.SetFloat(DeltaTime, Time.deltaTime);
            simulationShader.SetFloat(ElapsedTime, Time.time);
            simulationShader.SetFloat(DiffusionSpeed, profile.diffusionSpeed);
            simulationShader.SetFloat(EvaporationSpeed, profile.evaporationSpeed);
            simulationShader.SetVector(Color1, profile.color);
            simulationShader.SetTexture(1, TrailMap, trailMap);
            simulationShader.SetTexture(2, ColorMap, colorMap);
            simulationShader.Dispatch(1, simulationResolution.x / 10, simulationResolution.y / 10, 1);
            simulationShader.Dispatch(2, simulationResolution.x / 10, simulationResolution.y / 10, 1);

            simulationShader.SetBuffer(0, Agents, agentBuffer);
            simulationShader.SetBuffer(0, Species, speciesBuffer);
            simulationShader.SetInts(Resolution1, simulationResolution.x, simulationResolution.y);
            simulationShader.SetTexture(0, TrailMap, trailMap);
            simulationShader.SetTexture(0, ColorMap, colorMap);
            simulationShader.Dispatch(0, profile.agentCount / 100, 1, 1);

            agentBuffer.GetData(m_agents);

            agentBuffer.Dispose();
            speciesBuffer.Dispose();
        }

        Graphics.Blit(colorMap, destination);
    }
    
    private void OnGUI()
    {
        if (!m_showUi) return;
        
        GUILayout.BeginArea(new Rect(0, 0, 300, 1080), "Simulation Settings");
        GUILayout.Label("Press space to hide this menu");
        if (!m_running)
        {
            if (GUILayout.Button("Start"))
            {
                m_running = true;
            }
        }
        else
        {
            if (GUILayout.Button("Restart"))
            {
                Start();
            }
        }
        
        GUILayout.Label("Simulation Profiles");
        foreach (SimulationProfile simulationProfile in profiles)
        {
            if (GUILayout.Button(simulationProfile.name))
            {
                profile = simulationProfile;
                trailMap = null;
                colorMap = null;
                Start();
            }
        }

        return;

        GUILayout.Space(15);
        
        GUILayout.Label("Edit Profile");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Agent Count");
        profile.agentCount = int.Parse(GUILayout.TextField(profile.agentCount.ToString()));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Evaporation Speed");
        profile.evaporationSpeed = float.Parse(GUILayout.TextField(profile.evaporationSpeed.ToString()));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Diffusion Speed");
        profile.diffusionSpeed = float.Parse(GUILayout.TextField(profile.diffusionSpeed.ToString()));
        GUILayout.EndHorizontal();

        GUILayout.Space(15);
        
        for (int i = 0; i < profile.species.Length; i++) 
        {
            GUILayout.Label($"Specie n°{i + 1}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Specie Speed");
            profile.species[i].speed = float.Parse(GUILayout.TextField(profile.species[i].speed.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensor Distance");
            profile.species[i].sensorDistance =
                float.Parse(GUILayout.TextField(profile.species[i].sensorDistance.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensor Angle");
            profile.species[i].sensorAngle =
                float.Parse(GUILayout.TextField(profile.species[i].sensorAngle.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensor radius");
            profile.species[i].sensorRadius =
                float.Parse(GUILayout.TextField(profile.species[i].sensorRadius.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Turning Speed");
            profile.species[i].turningSpeed =
                float.Parse(GUILayout.TextField(profile.species[i].turningSpeed.ToString()));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("R");
            profile.species[i].color.x = float.Parse(GUILayout.TextField(profile.species[i].color.x.ToString()));
            GUILayout.Label("G");
            profile.species[i].color.y = float.Parse(GUILayout.TextField(profile.species[i].color.y.ToString()));
            GUILayout.Label("B");
            profile.species[i].color.z = float.Parse(GUILayout.TextField(profile.species[i].color.z.ToString()));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
        }

        GUILayout.EndArea();
    }

    [Serializable]
    private struct Agent
    {
        public Vector2 position;
        public float direction;
        private int m_specieIndex;

        public Agent(Vector2 position, float direction, int specieIndex)
        {
            this.position = position;
            this.direction = direction;
            m_specieIndex = specieIndex;
        }
    }

    [Serializable]
    public struct Specie
    {
        public float speed;
        public float sensorDistance;
        public float sensorAngle;
        public float sensorRadius;
        public float turningSpeed;
        public Vector3Int speciesMask;
        public Vector4 color;
    }
}
