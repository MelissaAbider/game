using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoShift
{
    public enum PlanResult { Running, Success, Failed }

    /// <summary>Runs Gemini's action list frame by frame on scaled time (frozen time = frozen plan).</summary>
    public sealed class CommandExecutor
    {
        abstract class Plan
        {
            public abstract PlanResult Tick(float dt);
        }

        readonly Subject subject;
        readonly GameDirector director;
        readonly List<GameAction> queue = new List<GameAction>();
        int index;
        Plan active;

        public event Action Changed;
        public IReadOnlyList<GameAction> Queue => queue;
        public bool Busy => index < queue.Count;

        public CommandExecutor(Subject subject, GameDirector director)
        {
            this.subject = subject;
            this.director = director;
        }

        public void Enqueue(List<GameAction> actions)
        {
            CancelAll(false);
            queue.Clear();
            queue.AddRange(actions);
            index = 0;
            Changed?.Invoke();
        }

        public void CancelAll(bool notify = true)
        {
            foreach (var a in queue)
                if (a.Status == ActionStatus.Pending || a.Status == ActionStatus.Running) a.Status = ActionStatus.Cancelled;
            index = queue.Count;
            active = null;
            subject.Stop();
            subject.Rig.Reaching = false;
            if (!director.UnderOverhead()) subject.Stand();
            if (notify) Changed?.Invoke();
        }

        public void Tick(float dt)
        {
            if (index >= queue.Count) return;
            var action = queue[index];
            if (active == null)
            {
                action.Status = ActionStatus.Running;
                active = Create(action);
                Changed?.Invoke();
            }
            var result = active.Tick(dt);
            if (result == PlanResult.Running) return;

            action.Status = result == PlanResult.Success ? ActionStatus.Success : ActionStatus.Failed;
            active = null;
            index++;
            if (result == PlanResult.Failed)
            {
                // A failed step invalidates the rest of the spoken plan.
                for (int i = index; i < queue.Count; i++) queue[i].Status = ActionStatus.Cancelled;
                index = queue.Count;
                subject.Stop();
            }
            Changed?.Invoke();
        }

        Plan Create(GameAction a)
        {
            var t = subject.transform;
            var pos = t.position;
            float meters(float fallback) => a.Distance > 0f ? Mathf.Clamp(a.Distance / 100f, 0.3f, 80f) : fallback;
            float speed = a.Speed == "run" ? Subject.RunSpeed : Subject.WalkSpeed;

            switch (a.Type)
            {
                case "MOVE_FORWARD":
                    return new MovePlan(this, pos + t.forward * meters(80f), speed, 0.2f);
                case "MOVE_BACK":
                    return new MovePlan(this, pos - t.forward * meters(3f), speed, 0.2f);
                // Side view: "left"/"right" mean screen directions along the corridor.
                case "MOVE_LEFT":
                    return SideView.Enabled ? new MovePlan(this, pos + Vector3.back * meters(80f), speed, 0.2f) : new MovePlan(this, pos - t.right * meters(3f), speed, 0.2f);
                case "MOVE_RIGHT":
                    return SideView.Enabled ? new MovePlan(this, pos + Vector3.forward * meters(80f), speed, 0.2f) : new MovePlan(this, pos + t.right * meters(3f), speed, 0.2f);
                case "TURN_LEFT":
                    return new TurnPlan(subject, SideView.Enabled ? 180f : subject.Yaw - 90f);
                case "TURN_RIGHT":
                    return new TurnPlan(subject, SideView.Enabled ? 0f : subject.Yaw + 90f);
                case "TURN_AROUND":
                    return new TurnPlan(subject, subject.Yaw + 180f);
                case "MOVE_TO":
                case "FOLLOW":
                {
                    var e = WorldEntity.Find(a.Target);
                    if (!e) return Fail("I can't see that from here.");
                    return new MovePlan(this, e.Position, speed, e.ApproachDistance);
                }
                case "JUMP":
                case "JUMP_OVER":
                {
                    var barrier = FindBarrier(a.Target) ?? NearestBarrierAhead(3.5f);
                    if (barrier == null)
                    {
                        subject.Jump();
                        return new WaitPlan(0.8f);
                    }
                    if (barrier.Kind == BarrierKind.Crouch)
                        return director.WrongMove(barrier, false) ? new InstantPlan(PlanResult.Failed) : Fail("Too high to jump. Say \"crouch under the beam\".");
                    if (barrier.Kind != BarrierKind.Jump) return Fail(barrier.Hint);
                    return new JumpOverPlan(this, barrier);
                }
                case "CROUCH":
                {
                    var barrier = FindBarrier(a.Target) ?? NearestBarrierAhead(3.5f);
                    if (barrier != null && barrier.Kind == BarrierKind.Crouch) return new CrouchUnderPlan(this, barrier);
                    // Ducking in front of a floor laser: the subject stops and asks what on earth you're doing.
                    if (barrier != null && barrier.Kind == BarrierKind.Jump && director.WrongMove(barrier, true)) return new InstantPlan(PlanResult.Failed);
                    return new CrouchPlan(subject, a.DurationMs > 0 ? a.DurationMs / 1000f : 1.4f);
                }
                case "STAND":
                    if (!director.UnderOverhead()) subject.Stand();
                    return Done();
                case "WAIT":
                    return new WaitPlan(a.DurationMs > 0 ? a.DurationMs / 1000f : 1f);
                case "INTERACT":
                case "USE":
                {
                    var e = WorldEntity.Find(a.Target) ?? director.NearestInteractable();
                    if (!e) return Fail("There is nothing to use here.");
                    return new InteractPlan(this, e);
                }
                case "BUILD":
                    return director.Forge.Build(a) ? Done() : new InstantPlan(PlanResult.Failed);
                case "ECHO":
                    return director.Echoes.Trigger() ? Done() : new InstantPlan(PlanResult.Failed);
                case "RECYCLE":
                    director.Forge.Recycle();
                    return Done();
                default:
                    return Done();
            }
        }

        Plan Done() => new InstantPlan(PlanResult.Success);

        Plan Fail(string line)
        {
            director.Say(line, Hud.Warning);
            return new InstantPlan(PlanResult.Failed);
        }

        static Barrier FindBarrier(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var b in Barrier.All) if (b.Id == id) return b;
            return null;
        }

        Barrier NearestBarrierAhead(float range, BarrierKind? kind = null)
        {
            float dir = Mathf.Sign(subject.transform.forward.z);
            float z = subject.transform.position.z;
            Barrier best = null;
            float bestAhead = float.MaxValue;
            foreach (var b in Barrier.All)
            {
                if (kind.HasValue && b.Kind != kind.Value) continue;
                if (b.Kind != BarrierKind.Jump && b.Kind != BarrierKind.Crouch && !b.Stops) continue;
                float ahead = (b.Z - z) * dir;
                if (ahead > -0.6f && ahead < range && ahead < bestAhead) { best = b; bestAhead = ahead; }
            }
            return best;
        }

        float Gait(float requested)
        {
            if (!subject.Crouching) return requested;
            if (director.UnderOverhead()) return Subject.SneakSpeed;
            subject.Stand();
            return requested;
        }

        // ── Plans ───────────────────────────────────────────────────────────

        sealed class InstantPlan : Plan
        {
            readonly PlanResult result;
            public InstantPlan(PlanResult result) => this.result = result;
            public override PlanResult Tick(float dt) => result;
        }

        sealed class WaitPlan : Plan
        {
            float left;
            public WaitPlan(float seconds) => left = seconds;
            public override PlanResult Tick(float dt) => (left -= dt) <= 0f ? PlanResult.Success : PlanResult.Running;
        }

        sealed class CrouchPlan : Plan
        {
            readonly Subject s;
            float left;
            public CrouchPlan(Subject s, float seconds) { this.s = s; left = seconds; }
            public override PlanResult Tick(float dt)
            {
                s.Stop();
                s.Crouch();
                if ((left -= dt) > 0f) return PlanResult.Running;
                s.Stand();
                return PlanResult.Success;
            }
        }

        sealed class TurnPlan : Plan
        {
            readonly Subject s;
            readonly float yaw;
            float left = 1.5f;
            public TurnPlan(Subject s, float yaw) { this.s = s; this.yaw = yaw; }
            public override PlanResult Tick(float dt)
            {
                s.Stop();
                s.FaceYaw(yaw);
                left -= dt;
                return s.Facing(yaw) || left <= 0f ? PlanResult.Success : PlanResult.Running;
            }
        }

        sealed class MovePlan : Plan
        {
            readonly CommandExecutor x;
            readonly Vector3 target;
            readonly float speed, stopDistance;
            float timeLeft;

            public MovePlan(CommandExecutor x, Vector3 target, float speed, float stopDistance)
            {
                this.x = x;
                this.target = target;
                this.speed = speed;
                this.stopDistance = stopDistance;
                var d = target - x.subject.transform.position;
                d.y = 0f;
                timeLeft = d.magnitude / Mathf.Max(0.5f, speed) * 1.6f + 3f;
            }

            public override PlanResult Tick(float dt)
            {
                var s = x.subject;
                var pos = s.transform.position;
                var flat = target - pos;
                flat.y = 0f;
                if (flat.magnitude <= Mathf.Max(0.15f, stopDistance)) { s.Stop(); return PlanResult.Success; }

                var barrier = x.director.BarrierAhead(pos, target);
                if (barrier != null)
                {
                    s.Stop();
                    x.director.Warn(barrier);
                    return PlanResult.Failed;
                }
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    s.Stop();
                    return flat.magnitude < 1.2f ? PlanResult.Success : PlanResult.Failed;
                }
                s.Move(x.director.Steer(pos, target) - pos, x.Gait(speed));
                return PlanResult.Running;
            }
        }

        sealed class InteractPlan : Plan
        {
            readonly CommandExecutor x;
            readonly WorldEntity entity;
            readonly MovePlan approach;
            float reachTime = -1f;

            public InteractPlan(CommandExecutor x, WorldEntity entity)
            {
                this.x = x;
                this.entity = entity;
                approach = new MovePlan(x, entity.Position, Subject.WalkSpeed, entity.ApproachDistance + 0.1f);
            }

            public override PlanResult Tick(float dt)
            {
                var s = x.subject;
                if (reachTime < 0f)
                {
                    var r = approach.Tick(dt);
                    if (r == PlanResult.Failed) return PlanResult.Failed;
                    if (r == PlanResult.Running) return PlanResult.Running;
                    s.FaceDirection(entity.Position - s.transform.position);
                    s.Rig.Reaching = true;
                    reachTime = 0.55f;
                    return PlanResult.Running;
                }
                reachTime -= dt;
                if (reachTime > 0f) return PlanResult.Running;
                s.Rig.Reaching = false;
                entity.Interact?.Invoke();
                return PlanResult.Success;
            }
        }

        sealed class JumpOverPlan : Plan
        {
            readonly CommandExecutor x;
            readonly Barrier barrier;
            readonly float dir;
            int phase;
            float left = 6f;

            public JumpOverPlan(CommandExecutor x, Barrier barrier)
            {
                this.x = x;
                this.barrier = barrier;
                float dz = barrier.Z - x.subject.transform.position.z;
                dir = Mathf.Abs(dz) < 0.05f ? Mathf.Sign(x.subject.transform.forward.z) : Mathf.Sign(dz);
            }

            public override PlanResult Tick(float dt)
            {
                var s = x.subject;
                var pos = s.transform.position;
                var forward = new Vector3(0, 0, dir);
                if ((left -= dt) <= 0f) { s.Stop(); return PlanResult.Failed; }
                switch (phase)
                {
                    case 0:
                        float approachZ = barrier.Z - dir * 1.05f;
                        if ((approachZ - pos.z) * dir > 0.1f)
                        {
                            s.Move(forward, Subject.WalkSpeed);
                            return PlanResult.Running;
                        }
                        s.FaceDirection(forward);
                        if (s.Jump()) phase = 1;
                        return PlanResult.Running;
                    case 1:
                        s.Move(forward, Subject.RunSpeed);
                        if ((pos.z - (barrier.Z + dir * 1.0f)) * dir >= 0f) phase = 2;
                        return PlanResult.Running;
                    default:
                        // Keep momentum until touchdown so the subject never drops back onto the beam.
                        if (!s.Grounded)
                        {
                            s.Move(forward, Subject.WalkSpeed);
                            return PlanResult.Running;
                        }
                        s.Stop();
                        return PlanResult.Success;
                }
            }
        }

        sealed class CrouchUnderPlan : Plan
        {
            readonly CommandExecutor x;
            readonly Barrier barrier;
            readonly float dir;
            float left = 9f;

            public CrouchUnderPlan(CommandExecutor x, Barrier barrier)
            {
                this.x = x;
                this.barrier = barrier;
                float dz = barrier.Z - x.subject.transform.position.z;
                dir = Mathf.Abs(dz) < 0.05f ? Mathf.Sign(x.subject.transform.forward.z) : Mathf.Sign(dz);
            }

            public override PlanResult Tick(float dt)
            {
                var s = x.subject;
                if ((left -= dt) <= 0f) { s.Stop(); return PlanResult.Failed; }
                s.Crouch();
                if ((s.transform.position.z - (barrier.Z + dir * 0.9f)) * dir >= 0f)
                {
                    s.Stop();
                    if (!x.director.UnderOverhead()) s.Stand();
                    return PlanResult.Success;
                }
                s.Move(new Vector3(0, 0, dir), Subject.SneakSpeed);
                return PlanResult.Running;
            }
        }
    }
}
