using UnityEngine;
using System.Runtime.InteropServices;

namespace Kodai.Fluid.SPH {
    public struct FluidParticle {
        public Vector2 Position;
        public Vector2 Velocity;
    };

    public class Fluid2D : FluidBase<FluidParticle> {
        
        [SerializeField, Range(3f, 5f)] private float ballRadius = 0.1f;
        [SerializeField, Range(1f, 3f)] private float MouseInteractionRadius = 1f;
        
        private bool isMouseDown;
        private Vector3 screenToWorldPointPos;

        protected override void InitParticleData(ref FluidParticle[] particles) {
            for (int i = 0; i < NumParticles; i++) {
                particles[i].Velocity = Vector2.zero;
                particles[i].Position = range / 2f + Random.insideUnitCircle * ballRadius;
            }
        }

        protected override void AdditionalCSParams(ComputeShader cs) {

            if (Input.GetMouseButtonDown(0)) {
                isMouseDown = true;
            }

            if(Input.GetMouseButtonUp(0)) {
                isMouseDown = false;
            }

            if (isMouseDown) {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = 10f;
                screenToWorldPointPos = Camera.main.ScreenToWorldPoint(mousePos);
            }

            cs.SetVector("_MousePos", screenToWorldPointPos);
            cs.SetFloat("_Radius", MouseInteractionRadius);
            cs.SetBool("_MouseDown", isMouseDown);
        }

    }
}