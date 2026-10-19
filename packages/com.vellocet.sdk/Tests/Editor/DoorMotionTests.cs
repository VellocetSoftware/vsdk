using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Doors;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests.Editor
{
    public sealed class DoorMotionTests
    {
        private GameObject _root;
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Door test");
            _root.SetActive(false);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);
        private static void Set(object target, string name, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(name);
        }
        [Test]
        public void SlidingUsesGeometryExtentAndSignedLip()
        {
            var door = _root.AddComponent<DoorMarker>();
            Set(door, "Lip", -.5f);
            var motion = new DoorMotion(door, new DoorPose(Vector3.zero, Quaternion.identity, Vector3.one), new Bounds(Vector3.zero, new Vector3(2, 3, 4)));
            Assert.That(motion.Evaluate(1).Position.z, Is.EqualTo(4.5f).Within(.0001f));
            Assert.That(motion.Duration, Is.EqualTo(.75f).Within(.0001f));
        }
        [Test]
        public void RotationUsesTheAuthoredHingeAndRestoresClosedPose()
        {
            var door = _root.AddComponent<RotatingDoorMarker>();
            Set(door, "Pivot", Vector3.right);
            var closed = new DoorPose(new Vector3(2, 0, 0), Quaternion.identity, Vector3.one);
            var motion = new DoorMotion(door, closed, new Bounds(Vector3.zero, Vector3.one));
            var open = motion.Evaluate(1);
            Assert.That(Vector3.Distance(open.Position + open.Rotation * Vector3.right, new Vector3(3, 0, 0)), Is.LessThan(.0001f));
            Assert.That(motion.Evaluate(0).Position, Is.EqualTo(closed.Position));
            Assert.That(Quaternion.Angle(open.Rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.001f));
        }
        [Test]
        public void ScalingPreservesOtherAxesAndCanReachZero()
        {
            var door = _root.AddComponent<ScalingDoorMarker>();
            var motion = new DoorMotion(door, new DoorPose(Vector3.zero, Quaternion.identity, new Vector3(2, 3, 4)), new Bounds(Vector3.zero, Vector3.one));
            Assert.That(motion.Evaluate(1).Scale, Is.EqualTo(new Vector3(2, 0, 4)));
            Assert.That(motion.Evaluate(0).Scale, Is.EqualTo(new Vector3(2, 3, 4)));
        }
        [Test]
        public void ReversalStartsAtTheCurrentCoordinateAndZeroDurationSnaps()
        {
            Assert.That(DoorMotion.Progress(.7f, false, 0, 1, DoorEase.InOutSine), Is.EqualTo(.7f).Within(.0001f));
            Assert.That(DoorMotion.Progress(.7f, false, .7, 1, DoorEase.Linear), Is.EqualTo(0));
            Assert.That(DoorMotion.Progress(.2f, true, 0, 0, DoorEase.Linear), Is.EqualTo(1));
        }
        [Test]
        public void MultipartBoundsIncludeTransformedModelsAndIgnoreOtherEntities()
        {
            var door = _root.AddComponent<DoorMarker>();
            var first = GameObject.CreatePrimitive(PrimitiveType.Cube);
            first.transform.SetParent(_root.transform, false);
            first.transform.localPosition = Vector3.left * 2;
            var second = GameObject.CreatePrimitive(PrimitiveType.Cube);
            second.transform.SetParent(_root.transform, false);
            second.transform.localPosition = Vector3.right * 2;
            second.transform.localScale = new Vector3(2, 3, 4);
            var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
            other.transform.SetParent(_root.transform, false);
            other.transform.localPosition = Vector3.right * 100;
            other.AddComponent<DoorMarker>();
            Assert.That(DoorGeometry.TryGetLocalBounds(_root.transform, _root.transform, door, out var bounds), Is.True);
            Assert.That(bounds.min.x, Is.EqualTo(-2.5f).Within(.001f));
            Assert.That(bounds.max.x, Is.EqualTo(3).Within(.001f));
            Assert.That(bounds.size.z, Is.EqualTo(4).Within(.001f));
        }
        [Test]
        public void RotatedAndScaledModelUsesItsProjectedTravelExtent()
        {
            var door = _root.AddComponent<DoorMarker>();
            Set(door, "MoveDirection", DoorMarker.LocalDirection.Right);
            var body = new GameObject("Model").transform;
            body.SetParent(_root.transform, false);
            door.SetBody(body);
            var pose = new DoorPose(Vector3.zero, Quaternion.Euler(0, 90, 0), new Vector3(2, 3, 4));
            var motion = new DoorMotion(door, pose, new Bounds(Vector3.zero, new Vector3(1, 2, 3)));
            Assert.That(motion.Evaluate(1).Position.x, Is.EqualTo(12).Within(.001f));
        }
        [Test]
        public void EvaluatingDoorPosesDoesNotAllocatePerFrame()
        {
            var door = _root.AddComponent<RotatingDoorMarker>();
            var motion = new DoorMotion(door, new DoorPose(Vector3.zero, Quaternion.identity, Vector3.one), new Bounds(Vector3.zero, Vector3.one));
            for (var i = 0; i < 100; i++)
                motion.Evaluate(i / 100f);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++)
                motion.Evaluate((i % 100) / 100f);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
        [Test]
        public void SoundOverridesCanSilenceAnActionAndReturnToTheReusableSet()
        {
            var door = _root.AddComponent<DoorMarker>();
            var set = ScriptableObject.CreateInstance<DoorSoundSet>();
            typeof(DoorSoundSet).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(set, new[] { new DoorSoundSet.ActionSounds { Action = DoorSoundAction.Opening, Sounds = new[] { new DoorSoundReference { Runtime = "native", Event = "FromSet" } } } });
            Set(door, "Sounds", set);
            var sound = new DoorSoundReference { Runtime = "native", Event = "Open", Duration = 1 };
            door.SetSoundOverride(DoorSoundAction.Opening, sound);
            Set(door, "OverrideSounds", true);
            Assert.That(door.SelectSound(DoorSoundAction.Opening).Event, Is.EqualTo("Open"));
            door.SetSoundOverride(DoorSoundAction.Opening, default);
            Assert.That(door.SelectSound(DoorSoundAction.Opening).IsEmpty, Is.True);
            door.RemoveSoundOverride(DoorSoundAction.Opening);
            Assert.That(door.TryGetSoundOverride(DoorSoundAction.Opening, out _), Is.False);
            Assert.That(door.SelectSound(DoorSoundAction.Opening).Event, Is.EqualTo("FromSet"));
            Object.DestroyImmediate(set);
            door.SetSoundOverride(DoorSoundAction.Closed, new DoorSoundReference { Duration = float.NaN });
            Assert.Throws<InvalidDataException>(() => door.ValidateDoor());
        }
        [Test]
        public void PortalPaddingStaysInWorldMetresUnderScaledModels()
        {
            _root.transform.localScale = new Vector3(2, 3, 4);
            _root.transform.rotation = Quaternion.Euler(0, 35, 0);
            var padded = DoorGeometry.PaddedSize(new Bounds(Vector3.zero, new Vector3(1, 2, 3)), _root.transform, .05f);
            Assert.That(padded.x, Is.EqualTo(1.05f).Within(.00001f));
            Assert.That(padded.y, Is.EqualTo(2 + .1f / 3).Within(.00001f));
            Assert.That(padded.z, Is.EqualTo(3.025f).Within(.00001f));
        }
        [Test]
        public void InvalidMotionIsRejectedBeforeActivation()
        {
            var door = _root.AddComponent<RotatingDoorMarker>();
            Set(door, "RotationAxis", Vector3.zero);
            Assert.Throws<InvalidDataException>(() => door.ValidateDoor());
        }
        [Test]
        public void ThreeTypesShareTheStableDoorSignalContract()
        {
            foreach (var type in new[] { typeof(DoorMarker), typeof(RotatingDoorMarker), typeof(ScalingDoorMarker) })
            {
                var host = new GameObject(type.Name);
                host.SetActive(false);
                try
                {
                    var door = (DoorMarker)host.AddComponent(type);
                    Assert.That(door.SignalContract.TryFindInput("open", out _), Is.True);
                    Assert.That(door.SignalContract.TryFindOutput("opening", out _), Is.True);
                    Assert.That(door.SignalContract.TryFindOutput("open", out _), Is.True);
                    Assert.That(door.SignalContract.TryFindOutput("locked_attempt", out _), Is.True);
                }
                finally { Object.DestroyImmediate(host); }
            }
        }
    }
}
