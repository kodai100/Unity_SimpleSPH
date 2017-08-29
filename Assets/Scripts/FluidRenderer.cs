using UnityEngine;
using System.Collections;

namespace Kodai.Fluid.SPH {

    [RequireComponent(typeof(Fluid2D))]
    public class FluidRenderer : MonoBehaviour {

        public Fluid2D solver;
        public Material RenderParticleMat;
        public Color WaterColor;

        void OnRenderObject() {
            DrawParticle();
        }

        void DrawParticle() {
            
            Material m = RenderParticleMat;

            var inverseViewMatrix = Camera.main.worldToCameraMatrix.inverse;

            m.SetPass(0);
            m.SetMatrix("_InverseMatrix", inverseViewMatrix);
            m.SetColor("_WaterColor", WaterColor);
            m.SetBuffer("_ParticlesBuffer", solver.ParticlesBufferRead);
            Graphics.DrawProcedural(MeshTopology.Points, solver.NumParticles);
        }
    }
}