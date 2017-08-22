using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kodai.Fluid.SPH {

    public enum NumParticleEnum {
        NUM_1K = 1024,
        NUM_2K = 1024 * 2,
        NUM_4K = 1024 * 4,
        NUM_8K = 1024 * 8
    };

    struct FluidParticleDensity {
        public float Density;
    };

    struct FluidParticleForces {
        public Vector2 Acceleration;
    };

    public abstract class FluidBase<T> : MonoBehaviour where T : struct {

        [SerializeField] protected NumParticleEnum particleNum = NumParticleEnum.NUM_8K;
        [SerializeField] protected float smoothlen = 0.012f;
        [SerializeField] private float pressureStiffness = 200.0f;
        [SerializeField] protected float restDensity = 1000.0f;
        [SerializeField] protected float particleMass = 0.0002f;
        [SerializeField, Range(1, 80)] protected float viscosity = 0.1f;
        [SerializeField] protected float maxAllowableTimestep = 0.005f;
        [SerializeField] protected float wallStiffness = 3000.0f;
        [SerializeField] protected int iterations = 4;
        [SerializeField] protected Vector2 gravity = new Vector2(0.0f, -0.5f);
        [SerializeField] protected Vector2 range = new Vector2(1, 1);
        [SerializeField] protected bool simulate = true;

        private int numParticles;
        private float timeStep;
        private float densityCoef;
        private float gradPressureCoef;
        private float lapViscosityCoef;
        private static readonly float soundSpeed = 340.29f;

        #region DirectCompute
        private ComputeShader fluidCS;
        private static readonly int THREAD_SIZE_X = 1024;
        private ComputeBuffer particlesBufferRead;
        private ComputeBuffer particlesBufferWrite;
        private ComputeBuffer particleDensitiesBuffer;
        private ComputeBuffer particleForcesBuffer;
        #endregion

        #region Accessor
        public int NumParticles {
            get { return numParticles; }
        }

        public float Viscosity {
            get { return viscosity; }
            set { viscosity = value; }
        }

        public Vector2 Gravity {
            get { return gravity; }
            set { gravity = value; }
        }

        public Vector2 Range {
            get { return range; }
            set { range = value; }
        }
        
        public bool Simulate {
            get { return simulate; }
            set { simulate = value; }
        }

        public ComputeBuffer ParticlesBufferRead {
            get { return particlesBufferRead; }
        }

        public ComputeBuffer ParticleDensitiesBuffer {
            get { return particleDensitiesBuffer; }
        }
        #endregion
        
        protected virtual void Awake() {
            fluidCS = (ComputeShader)Resources.Load("SPH2D");
            numParticles = (int)particleNum;
        } 

        protected virtual void Start() {
            InitBuffers();
        }

        private void Update() {

            if (!simulate) {
                return;
            }

            // Adaptive user modification
            timeStep = Mathf.Min(maxAllowableTimestep, Time.deltaTime);

            // 2D
            densityCoef = particleMass * 4f / (Mathf.PI * Mathf.Pow(smoothlen, 8));
            gradPressureCoef = particleMass * -30.0f / (Mathf.PI * Mathf.Pow(smoothlen, 5));
            lapViscosityCoef = particleMass * 20f / (3 * Mathf.PI * Mathf.Pow(smoothlen, 5));

            fluidCS.SetInt("_NumParticles", numParticles);
            fluidCS.SetFloat("_TimeStep", timeStep);
            fluidCS.SetFloat("_Smoothlen", smoothlen);
            fluidCS.SetFloat("_PressureStiffness", pressureStiffness);
            fluidCS.SetFloat("_RestDensity", restDensity);
            fluidCS.SetFloat("_Viscosity", viscosity);
            fluidCS.SetFloat("_DensityCoef", densityCoef);
            fluidCS.SetFloat("_GradPressureCoef", gradPressureCoef);
            fluidCS.SetFloat("_LapViscosityCoef", lapViscosityCoef);
            fluidCS.SetFloat("_WallStiffness", wallStiffness);
            fluidCS.SetVector("_Range", range);
            fluidCS.SetVector("_Gravity", gravity);

            AdditionalCSParams(fluidCS);

            for (int i = 0; i<iterations; i++) {
                RunFluidSolver();
            }
        }

        private void RunFluidSolver() {

            int kernelID = -1;
            int threadGroupsX = numParticles / THREAD_SIZE_X;

            // Density
            kernelID = fluidCS.FindKernel("DensityCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferWrite", particleDensitiesBuffer);
            fluidCS.Dispatch(kernelID, threadGroupsX, 1, 1);

            // Force
            kernelID = fluidCS.FindKernel("ForceCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particleDensitiesBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferWrite", particleForcesBuffer);
            fluidCS.Dispatch(kernelID, threadGroupsX, 1, 1);

            // Integrate
            kernelID = fluidCS.FindKernel("IntegrateCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferRead", particleForcesBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferWrite", particlesBufferWrite);
            fluidCS.Dispatch(kernelID, threadGroupsX, 1, 1);

            SwapComputeBuffer(ref particlesBufferRead, ref particlesBufferWrite);
        }

        protected abstract void AdditionalCSParams(ComputeShader shader);

        protected abstract void InitParticleData(ref T[] particles);

        private void InitBuffers() {
            particlesBufferRead = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticle)));
            var particles = new T[numParticles];
            InitParticleData(ref particles);
            particlesBufferRead.SetData(particles);
            particles = null;

            particlesBufferWrite = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(T)));
            particleForcesBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticleForces)));
            particleDensitiesBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticleDensity)));
        }

        private void OnDestroy() {
            DeleteBuffer(particlesBufferRead);
            DeleteBuffer(particlesBufferWrite);
            DeleteBuffer(particleDensitiesBuffer);
            DeleteBuffer(particleForcesBuffer);
        }

        private void SwapComputeBuffer(ref ComputeBuffer ping, ref ComputeBuffer pong) {
            ComputeBuffer temp = ping;
            ping = pong;
            pong = temp;
        }

        private void DeleteBuffer(ComputeBuffer buffer) {
            if (buffer != null) {
                buffer.Release();
                buffer = null;
            }
        }
    }
}