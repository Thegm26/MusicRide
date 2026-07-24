using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicWeather : MonoBehaviour
    {
        private MusicWorldController music;
        private Transform car;
        private ParticleSystem rain;
        private ParticleSystem snow;
        private Material rainMaterial;
        private Material snowMaterial;
        private float rainAccumulator;
        private float snowAccumulator;

        public void Initialize(MusicWorldController musicController, Transform carTransform)
        {
            music = musicController;
            car = carTransform;
            rain = CreateSystem("Sharp Impact Rain", false, out rainMaterial);
            snow = CreateSystem("Soft Tonal Snow", true, out snowMaterial);
        }

        private void Update()
        {
            if (music == null || car == null)
            {
                return;
            }

            AudioFeatureFrame frame = music.Immediate;
            float section = Mathf.Clamp01(frame.sectionLift);
            float softColdTexture = Mathf.Clamp01((1f - frame.brightness) * frame.tonality);
            float sharpTexture = Mathf.Clamp01(frame.sharpness * 0.62f + frame.highImpact * 0.38f);
            float snowStrength = Mathf.Clamp01(softColdTexture * section * 1.65f);
            float rainStrength = Mathf.Clamp01(sharpTexture * section * 1.5f);

            // Do not layer both effects heavily. The stronger acoustic texture wins.
            if (snowStrength > rainStrength + 0.08f)
            {
                rainStrength *= 0.12f;
            }
            else if (rainStrength > snowStrength + 0.08f)
            {
                snowStrength *= 0.12f;
            }

            float hitBurst = frame.onset > 0.76f ? frame.onset * 80f : 0f;
            rainAccumulator += (rainStrength * 150f + hitBurst * rainStrength) * Time.deltaTime;
            snowAccumulator += (snowStrength * 95f + hitBurst * snowStrength) * Time.deltaTime;
            EmitRain(Mathf.FloorToInt(rainAccumulator));
            EmitSnow(Mathf.FloorToInt(snowAccumulator));
            rainAccumulator %= 1f;
            snowAccumulator %= 1f;
        }

        private void EmitRain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams value = new ParticleSystem.EmitParams
                {
                    position = car.position + car.forward * Random.Range(-8f, 28f) +
                        car.right * Random.Range(-15f, 15f) + Vector3.up * Random.Range(8f, 17f),
                    velocity = Vector3.down * Random.Range(20f, 29f) - car.forward * Random.Range(1f, 4f),
                    startLifetime = Random.Range(0.55f, 0.9f),
                    startSize = Random.Range(0.025f, 0.055f),
                    startColor = new Color(0.55f, 0.78f, 1f, 0.78f)
                };
                rain.Emit(value, 1);
            }
        }

        private void EmitSnow(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.EmitParams value = new ParticleSystem.EmitParams
                {
                    position = car.position + car.forward * Random.Range(-7f, 25f) +
                        car.right * Random.Range(-14f, 14f) + Vector3.up * Random.Range(7f, 15f),
                    velocity = Vector3.down * Random.Range(1.8f, 4.2f) +
                        car.right * Random.Range(-0.8f, 0.8f),
                    startLifetime = Random.Range(2.4f, 4.2f),
                    startSize = Random.Range(0.055f, 0.16f),
                    startColor = new Color(0.9f, 0.96f, 1f, 0.9f)
                };
                snow.Emit(value, 1);
            }
        }

        private ParticleSystem CreateSystem(string systemName, bool snowStyle, out Material material)
        {
            GameObject systemObject = new GameObject(systemName);
            systemObject.transform.SetParent(transform, false);
            ParticleSystem system = systemObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = snowStyle ? 700 : 520;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = snowStyle
                ? ParticleSystemRenderMode.Billboard
                : ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = snowStyle ? 1f : 8f;
            renderer.velocityScale = snowStyle ? 0f : 0.12f;

            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            material = shader != null ? new Material(shader) : null;
            if (material != null)
            {
                material.name = $"{systemName} Material";
                material.color = snowStyle
                    ? new Color(0.9f, 0.96f, 1f, 0.9f)
                    : new Color(0.5f, 0.75f, 1f, 0.76f);
                renderer.sharedMaterial = material;
            }
            return system;
        }

        private void OnDestroy()
        {
            if (rainMaterial != null)
            {
                Destroy(rainMaterial);
            }
            if (snowMaterial != null)
            {
                Destroy(snowMaterial);
            }
        }
    }
}
