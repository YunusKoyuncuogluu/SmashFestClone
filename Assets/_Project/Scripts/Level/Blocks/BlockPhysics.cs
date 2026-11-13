using UnityEngine;

namespace SmashFest.Level
{
    public class BlockPhysics
    {
        private const float LinearDamping = 0.1f;
        private const float AngularDamping = 1.2f;
        private const float MaxDepenetrationVelocity = 1.5f;
        private const float SleepThreshold = 0.01f;
        private const int SolverIterations = 12;
        private const int SolverVelocityIterations = 4;

        private readonly Rigidbody body;

        public BlockPhysics(Rigidbody body)
        {
            this.body = body;
        }

        public bool IsDormant { get; private set; }
        public float Mass => body != null ? body.mass : 0f;

        public void Configure(float mass, bool isStatic)
        {
            if (body == null)
                return;

            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = !isStatic;
            body.detectCollisions = true;
            body.linearDamping = LinearDamping;
            body.angularDamping = AngularDamping;
            body.maxDepenetrationVelocity = MaxDepenetrationVelocity;
            body.sleepThreshold = SleepThreshold;
            body.solverIterations = SolverIterations;
            body.solverVelocityIterations = SolverVelocityIterations;
            IsDormant = !isStatic;
        }

        public bool Wake()
        {
            if (body == null)
                return false;

            if (!IsDormant)
            {
                if (!body.isKinematic)
                    body.WakeUp();
                return false;
            }

            IsDormant = false;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            return true;
        }

        public void Push(Vector3 impulse, Vector3 point)
        {
            if (body != null && !body.isKinematic)
                body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }

        public void Freeze()
        {
            if (body == null)
                return;

            IsDormant = false;
            body.detectCollisions = false;
            body.isKinematic = true;
        }

        public void GetVelocity(out Vector3 linear, out Vector3 angular)
        {
            bool isMoving = body != null && !body.isKinematic;
            linear = isMoving ? body.linearVelocity : Vector3.zero;
            angular = isMoving ? body.angularVelocity : Vector3.zero;
        }
    }
}
