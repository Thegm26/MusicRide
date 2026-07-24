using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicRoadBootstrap : MonoBehaviour
    {
        [SerializeField] private Shader runtimeShader;

        public void SetRuntimeShader(Shader shader)
        {
            runtimeShader = shader;
        }

        private void Awake()
        {
            if (FindAnyObjectByType<ArcadeCarController>() != null)
            {
                return;
            }

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Physics.gravity = new Vector3(0f, -18f, 0f);

            Shader shader = runtimeShader != null ? runtimeShader : Shader.Find("MusicRoad/Reactive");
            Material roadMaterial = CreateMaterial(shader, "Road", new Color(0.09f, 0.1f, 0.14f), 0.25f);
            Material shoulderMaterial = CreateMaterial(shader, "Shoulder", new Color(0.19f, 0.35f, 0.27f), 0f);
            Material edgeMaterial = CreateMaterial(shader, "Music Lane Strips", Color.white, 0.4f, true);
            Material carMaterial = CreateMaterial(shader, "Toy Car", new Color(1f, 0.18f, 0.16f), 0.65f);
            Material darkMaterial = CreateMaterial(shader, "Toy Dark", new Color(0.025f, 0.03f, 0.045f), 0.35f);
            Material glassMaterial = CreateMaterial(shader, "Toy Glass", new Color(0.18f, 0.65f, 0.9f), 0.8f);
            Material flameMaterial = CreateMaterial(shader, "Nitro Flame", new Color(1f, 0.58f, 0.04f), 0f, true);
            Material windMaterial = CreateMaterial(shader, "Peak Wind", new Color(0.88f, 0.96f, 1f), 0f, true);

            AudioCaptureService capture = new GameObject("AudioCaptureService").AddComponent<AudioCaptureService>();
            Light sun = CreateLighting();
            MusicWorldController world = new GameObject("Music World Controller").AddComponent<MusicWorldController>();
            world.Initialize(capture, sun, edgeMaterial);

            GameObject carObject = CreateToyCar(carMaterial, darkMaterial, glassMaterial, flameMaterial);
            ArcadeCarController car = carObject.GetComponent<ArcadeCarController>();

            RoadGenerator road = new GameObject("Procedural Music Road").AddComponent<RoadGenerator>();
            road.Initialize(world, carObject.transform, roadMaterial, shoulderMaterial, edgeMaterial);
            car.Initialize(road);
            car.PlaceAtStart();

            CreateCamera(carObject.transform, world);
            MusicPeakWind wind = new GameObject("Maximum Music Wind").AddComponent<MusicPeakWind>();
            wind.Initialize(world, carObject.transform, windMaterial);

            RunManager run = new GameObject("Run Manager").AddComponent<RunManager>();
            run.Initialize(capture, car);
        }

        private static Material CreateMaterial(Shader shader, string name, Color color, float smoothness, bool emissive = false)
        {
            if (shader == null)
            {
                throw new System.InvalidOperationException("MusicRoad/Reactive shader is missing. Refresh the main scene from the Music Road editor menu.");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }

            return material;
        }

        private static Light CreateLighting()
        {
            GameObject lightObject = new GameObject("Music Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = light;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            return light;
        }

        private static GameObject CreateToyCar(Material bodyMaterial, Material darkMaterial, Material glassMaterial, Material flameMaterial)
        {
            GameObject root = new GameObject("Player Toy Car");
            root.transform.position = new Vector3(0f, 1f, 4f);
            Rigidbody rigidbody = root.AddComponent<Rigidbody>();
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.8f, 0.7f, 3.3f);
            collider.center = new Vector3(0f, 0.15f, 0f);
            root.AddComponent<ArcadeCarController>();

            AddPrimitive(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(1.85f, 0.55f, 3.25f), bodyMaterial);
            AddPrimitive(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.67f, -0.28f), new Vector3(1.45f, 0.72f, 1.55f), glassMaterial);
            AddPrimitive(root.transform, "Front Bumper", PrimitiveType.Cube, new Vector3(0f, -0.02f, 1.72f), new Vector3(1.95f, 0.18f, 0.22f), darkMaterial);
            AddPrimitive(root.transform, "Rear Bumper", PrimitiveType.Cube, new Vector3(0f, -0.02f, -1.72f), new Vector3(1.95f, 0.18f, 0.22f), darkMaterial);

            Vector3[] wheelPositions =
            {
                new Vector3(-1f, -0.18f, 1.08f),
                new Vector3(1f, -0.18f, 1.08f),
                new Vector3(-1f, -0.18f, -1.08f),
                new Vector3(1f, -0.18f, -1.08f)
            };

            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject wheel = AddPrimitive(root.transform, $"Wheel {i + 1}", PrimitiveType.Cylinder, wheelPositions[i], new Vector3(0.52f, 0.28f, 0.52f), darkMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            Transform[] nitroFlames =
            {
                CreateNitroFlame(root.transform, "Left Nitro Flame", new Vector3(-0.5f, -0.02f, -1.8f), flameMaterial),
                CreateNitroFlame(root.transform, "Right Nitro Flame", new Vector3(0.5f, -0.02f, -1.8f), flameMaterial)
            };
            root.GetComponent<ArcadeCarController>().ConfigureNitroFlames(nitroFlames);

            rigidbody.centerOfMass = new Vector3(0f, -0.45f, 0f);
            return root;
        }

        private static Transform CreateNitroFlame(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject flame = new GameObject(name);
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = position;

            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.2f, -0.2f, 0f),
                new Vector3(0.2f, -0.2f, 0f),
                new Vector3(0.2f, 0.2f, 0f),
                new Vector3(-0.2f, 0.2f, 0f),
                new Vector3(0f, 0f, -1.65f)
            };
            mesh.triangles = new[]
            {
                0, 1, 4,
                1, 2, 4,
                2, 3, 4,
                3, 0, 4,
                0, 3, 2,
                0, 2, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            flame.AddComponent<MeshFilter>().sharedMesh = mesh;
            flame.AddComponent<MeshRenderer>().sharedMaterial = material;
            return flame.transform;
        }

        private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Destroy(primitiveCollider);
            }

            return primitive;
        }

        private static void CreateCamera(Transform car, MusicWorldController world)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 230f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.22f, 0.34f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ChaseCamera>().Initialize(car, world);
        }

    }
}
