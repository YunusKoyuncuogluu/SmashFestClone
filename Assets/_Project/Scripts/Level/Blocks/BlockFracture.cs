using System.Threading;
using Cysharp.Threading.Tasks;
using SmashFest.Core.Effects;
using UnityEngine;

namespace SmashFest.Level
{
    public struct FragmentMotion
    {
        public float Burst;
        public float Push;
        public float Lift;
        public float Spin;
    }

    public class BlockFracture
    {
        private const int PieceSolverIterations = 2;
        private const float PieceSleepThreshold = 0.05f;

        private readonly GameObject intactVisual;
        private readonly GameObject fracturedVisual;
        private readonly Transform owner;
        private readonly Rigidbody[] pieces;
        private readonly float[] pieceMasses;
        private readonly Vector3[] piecePositions;
        private readonly Quaternion[] pieceRotations;
        private readonly VanishEffect fade;

        public BlockFracture(GameObject intactVisual, GameObject fracturedVisual, Transform owner)
        {
            this.intactVisual = intactVisual;
            this.fracturedVisual = fracturedVisual;
            this.owner = owner;
            if (fracturedVisual == null)
                return;

            pieces = fracturedVisual.GetComponentsInChildren<Rigidbody>(true);
            pieceMasses = new float[pieces.Length];
            piecePositions = new Vector3[pieces.Length];
            pieceRotations = new Quaternion[pieces.Length];
            Transform[] pieceTransforms = new Transform[pieces.Length];

            for (int i = 0; i < pieces.Length; i++)
            {
                Transform piece = pieces[i].transform;
                pieceTransforms[i] = piece;
                pieceMasses[i] = pieces[i].mass;
                piecePositions[i] = piece.localPosition;
                pieceRotations[i] = piece.localRotation;
                MakeLightweight(pieces[i]);
            }

            fade = new VanishEffect(pieceTransforms, fracturedVisual.GetComponentsInChildren<Renderer>(true));
        }

        public bool HasPieces => fracturedVisual != null;

        private void MakeLightweight(Rigidbody piece)
        {
            piece.interpolation = RigidbodyInterpolation.None;
            piece.collisionDetectionMode = CollisionDetectionMode.Discrete;
            piece.solverIterations = PieceSolverIterations;
            piece.solverVelocityIterations = 1;
            piece.sleepThreshold = PieceSleepThreshold;
        }

        public void ShowIntact()
        {
            fade?.Restore();
            if (intactVisual != null)
                intactVisual.SetActive(true);

            if (fracturedVisual != null)
                fracturedVisual.SetActive(false);
        }

        public void Shatter(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 pushDirection, float massMultiplier, FragmentMotion motion)
        {
            if (intactVisual != null)
                intactVisual.SetActive(false);

            if (fracturedVisual == null)
                return;

            fracturedVisual.SetActive(true);
            fade.Restore();
            for (int i = 0; i < pieces.Length; i++)
            {
                Rigidbody piece = pieces[i];
                piece.transform.SetLocalPositionAndRotation(piecePositions[i], pieceRotations[i]);
                piece.mass = pieceMasses[i] * massMultiplier;
                if (piece.isKinematic)
                    continue;

                Vector3 outward = piece.transform.position - owner.position;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                outward.y = 0f;

                piece.linearVelocity = linearVelocity
                    + outward * (motion.Burst * Random.Range(0.5f, 1.2f))
                    + pushDirection * (motion.Push * Random.Range(0.6f, 1.1f))
                    + Vector3.up * (motion.Lift * Random.Range(0.2f, 1f));
                piece.angularVelocity = angularVelocity + Random.insideUnitSphere * motion.Spin;
            }
        }

        public UniTask<bool> FadeOutAsync(float duration, CancellationToken token)
        {
            return fade != null ? fade.PlayAsync(duration, token) : UniTask.FromResult(false);
        }
    }
}
