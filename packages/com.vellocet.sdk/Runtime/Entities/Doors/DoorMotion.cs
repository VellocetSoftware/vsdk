using System;
using System.IO;
using UnityEngine;

namespace Vellocet.Sdk.Entities.Doors
{
    public enum DoorMotionKind
    {
        Sliding, Rotating, Scaling
    }
    public enum DoorEase
    {
        Linear, InOutSine, InCubic
    }

    public readonly struct DoorPose
    {
        public readonly Vector3 Position, Scale;
        public readonly Quaternion Rotation;
        public DoorPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        public static DoorPose Capture(Transform body) => new(body.localPosition, body.localRotation, body.localScale);
        public void Apply(Transform body)
        {
            body.SetLocalPositionAndRotation(Position, Rotation);
            body.localScale = Scale;
        }
    }

    // One pose evaluator for authoring previews and both native game adapters.
    public readonly struct DoorMotion
    {
        private readonly Vertex.Contracts.DoorKinematics _kinematics;
        public readonly float Duration;

        public DoorMotion(DoorMarker marker, DoorPose closed, Bounds bounds)
        {
            marker.ValidateDoor();
            var kind = marker.MotionKind;
            var direction = marker.BrushTransform == marker.transform ? closed.Rotation * marker.GetLocalOpenDirection() : marker.GetLocalOpenDirection();
            var axis = marker is RotatingDoorMarker rotating ? rotating.RotationAxis.normalized : Vector3.up;
            var angle = marker is RotatingDoorMarker rotation ? rotation.Angle : 0;
            var pivot = marker.Pivot;
            var scaleAxis = marker is ScalingDoorMarker scaling ? (int)scaling.Axis : 0;
            var targetScale = marker is ScalingDoorMarker scale ? scale.TargetScale : 1;
            var measureAxis = Quaternion.Inverse(closed.Rotation) * direction;
            var scaledExtents = Vector3.Scale(bounds.extents, new Vector3(Mathf.Abs(closed.Scale.x), Mathf.Abs(closed.Scale.y), Mathf.Abs(closed.Scale.z)));
            var travel = marker.UseTravelDistance ? marker.TravelDistance : Mathf.Max(0,
                2 * Vector3.Dot(scaledExtents, new Vector3(Mathf.Abs(measureAxis.x), Mathf.Abs(measureAxis.y), Mathf.Abs(measureAxis.z))) - marker.Lip);
            var distance = kind == DoorMotionKind.Sliding ? Mathf.Abs(travel) :
                kind == DoorMotionKind.Rotating ? Mathf.Abs(angle) : Mathf.Abs(targetScale - closed.Scale[scaleAxis]);
            Duration = marker.UseDuration ? marker.Duration : distance / marker.Speed;
            _kinematics = new Vertex.Contracts.DoorKinematics((int)kind, N(closed.Position), Q(closed.Rotation), N(closed.Scale), N(direction), travel, N(axis), angle, N(pivot), scaleAxis, targetScale);
            if (!float.IsFinite(Duration) || Duration < 0)
                throw new InvalidDataException("Door motion has an invalid duration.");
        }

        private static System.Numerics.Vector3 N(Vector3 v) => new(v.x, v.y, v.z);
        private static System.Numerics.Quaternion Q(Quaternion q) => new(q.x, q.y, q.z, q.w);
        public DoorPose Evaluate(float progress)
        {
            var pose = _kinematics.Evaluate(progress);
            return new DoorPose(new Vector3(pose.Position.X, pose.Position.Y, pose.Position.Z), new Quaternion(pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z, pose.Rotation.W), new Vector3(pose.Scale.X, pose.Scale.Y, pose.Scale.Z));
        }
        public static float Curve(float t, DoorEase ease) => Vertex.Contracts.DoorKinematics.Curve(t, (int)ease);
        public static float Progress(float from, bool open, double elapsed, float duration, DoorEase ease) =>
            Vertex.Contracts.DoorKinematics.Progress(from, open, elapsed, duration, (int)ease);
    }
}
